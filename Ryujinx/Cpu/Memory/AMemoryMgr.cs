using ChocolArm64.Exceptions;
using System;
using System.Runtime.CompilerServices;

namespace ChocolArm64.Memory
{
    // 负责虚拟内存到物理内存的映射，维护映射表，使用AMemoryAlloc进行内存分配
    public class AMemoryMgr
    {
        public const long AddrSize = 1L << 36;  //64g
        public const long RamSize  = 2L * 1024 * 1024 * 1024; // 内存一共这么大

        private const int  PTLvl0Bits = 11; //虚拟地址几位表示1级映射索引
        private const int  PTLvl1Bits = 13; //虚拟地址几位表示2级映射索引
        private const int  PTPageBits = 12;

        private const int  PTLvl0Size = 1 << PTLvl0Bits;  //虚拟内存映射表一级数量 2k
        private const int  PTLvl1Size = 1 << PTLvl1Bits; //虚拟内存映射表二级级数量 3k
        public  const int  PageSize   = 1 << PTPageBits;  //页大小4k

        private const int  PTLvl0Mask = PTLvl0Size - 1;  // 用来从虚拟地址中取到它对应的一级映射表
        private const int  PTLvl1Mask = PTLvl1Size - 1;  // 用来从虚拟地址中取到它对应的二级映射表
        public  const int  PageMask   = PageSize   - 1;  // 页面对齐用，这个这个mask &之后可以查看大小超出4k多少字节

        private const int  PTLvl0Bit  = PTPageBits + PTLvl0Bits;
        private const int  PTLvl1Bit  = PTPageBits;

        // 内存分配器
        private AMemoryAlloc Allocator;

        private enum PTMap
        {
            Unmapped, // 虚拟内存没有映射到物理内存
            Physical, //虚拟内存映射到物理内存
            Mirror    //虚拟内存映射到虚拟内存
        }

        private struct PTEntry //虚拟映射表项
        {
            public long Position; //物理起始地址
            public int  Type;  // 参考MemoryType

            public PTMap       Map; //哪种映射
            public AMemoryPerm Perm; //权限 读写可执行

            public PTEntry(long Position, int Type, PTMap Map, AMemoryPerm Perm)
            {
                this.Position = Position;
                this.Type     = Type;
                this.Map      = Map;
                this.Perm     = Perm;
            }
        }

        private PTEntry[][] PageTable;  //虚拟内存映射表

        private bool IsHeapInitialized;

        public long HeapAddr { get; private set; } // 堆地址指针
        public int  HeapSize { get; private set; }

        public AMemoryMgr(AMemoryAlloc Allocator)
        {
            this.Allocator = Allocator;

            PageTable = new PTEntry[PTLvl0Size][];
        }
        // 所有的物理内存，free+已用的
        public long GetTotalMemorySize()
        {
            return Allocator.GetFreeMem() + GetUsedMemorySize();
        }
        //已用的物理内存（除了unmapped的虚拟内存）
        public long GetUsedMemorySize()
        {
            long Size = 0;

            for (int L0 = 0; L0 < PageTable.Length; L0++)
            {
                if (PageTable[L0] == null)
                {
                    continue;
                }

                for (int L1 = 0; L1 < PageTable[L0].Length; L1++)
                {
                    Size += PageTable[L0][L1].Map != PTMap.Unmapped ? PageSize : 0;
                }
            }

            return Size;
        }
        // 初始化堆地址
        public bool SetHeapAddr(long Position)
        {
            if (!IsHeapInitialized)
            {
                HeapAddr = Position;

                IsHeapInitialized = true;

                return true;
            }

            return false;
        }

        public void SetHeapSize(int Size, int Type)
        {
            //TODO: Return error when theres no enough space to allocate heap.
            Size = (int)AMemoryHelper.PageRoundUp(Size);

            long Position = HeapAddr;

            if ((ulong)Size < (ulong)HeapSize)
            {
                //Try to free now free area if size is smaller than old size.
                Position += Size;

                while ((ulong)Size < (ulong)HeapSize)
                {
                    Allocator.Free(GetPhys(Position, AMemoryPerm.None));

                    Position += PageSize;
                }
            }
            else
            {
                //Allocate extra needed size.
                Position += HeapSize;
                Size     -= HeapSize;
                // 分配物理内存并建立映射表
                MapPhys(Position, Size, Type, AMemoryPerm.RW);
            }

            HeapSize = Size;
        }

        // 虚拟地址映射到物理地址(不分配内存)
        public bool MapPhys(long Src, long Dst, long Size, int Type, AMemoryPerm Perm)
        {
            Src = AMemoryHelper.PageRoundDown(Src);
            Dst = AMemoryHelper.PageRoundDown(Dst);

            Size = AMemoryHelper.PageRoundUp(Size);

            if (Dst < 0 || Dst + Size >= RamSize)
            {
                return false;
            }

            long PagesCount = Size / PageSize;

            while (PagesCount-- > 0)
            {
                SetPTEntry(Src, new PTEntry(Dst, Type, PTMap.Physical, Perm));

                Src += PageSize;
                Dst += PageSize;
            }

            return true;
        }
        // 虚拟地址映射到物理地址(会分配内存)
        public void MapPhys(long Position, long Size, int Type, AMemoryPerm Perm)
        {
            while (Size > 0)
            {
                if (!HasPTEntry(Position)) //虚拟页没有映射过
                {
                    long PhysPos = Allocator.Alloc(PageSize); // 分配一页，返回的物理地址               

                    SetPTEntry(Position, new PTEntry(PhysPos, Type, PTMap.Physical, Perm)); //建立映射表
                }

                long CPgSize = PageSize - (Position & PageMask); //页对齐使用的增量

                Position += CPgSize; //下个虚拟地址
                Size     -= CPgSize; // 剩余要分配的
            }
        }
        //让一块虚拟内存映射到另一块虚拟内存（猜测是想让两块内存一直用一个屋里页）
        public void MapMirror(long Src, long Dst, long Size, int Type)
        {
            Src = AMemoryHelper.PageRoundDown(Src);
            Dst = AMemoryHelper.PageRoundDown(Dst);

            Size = AMemoryHelper.PageRoundUp(Size);

            long PagesCount = Size / PageSize;

            while (PagesCount-- > 0)
            {
                PTEntry Entry = GetPTEntry(Src);

                Entry.Type     = Type;
                Entry.Map      = PTMap.Mirror;
                Entry.Position = Dst;
                //src映射到entry
                SetPTEntry(Src, Entry);

                Src += PageSize;
                Dst += PageSize;
            }
        }
        //修改虚拟映射页的权限
        public void Reprotect(long Position, long Size, AMemoryPerm Perm)
        {
            Position = AMemoryHelper.PageRoundDown(Position);

            Size = AMemoryHelper.PageRoundUp(Size);

            long PagesCount = Size / PageSize;

            while (PagesCount-- > 0)
            {
                PTEntry Entry = GetPTEntry(Position);

                Entry.Perm = Perm;

                SetPTEntry(Position, Entry);

                Position += PageSize;
            }
        }
        //获取Position所在段（segment）的信息，比如代码段
        public AMemoryMapInfo GetMapInfo(long Position)
        {
            Position = AMemoryHelper.PageRoundDown(Position);

            PTEntry BaseEntry = GetPTEntry(Position);

            bool IsSameSegment(long Pos)
            {
                PTEntry Entry = GetPTEntry(Pos);
                // 每个段的权限和类型都是一样的
                return Entry.Type == BaseEntry.Type &&
                       Entry.Map  == BaseEntry.Map  &&
                       Entry.Perm == BaseEntry.Perm;
            }

            long Start = Position;
            long End   = Position + PageSize;
            //找到段起始位置
            while (Start > 0 && IsSameSegment(Start - PageSize))
            {
                Start -= PageSize;
            }
            //找到段结束位置
            while (End < AddrSize && IsSameSegment(End))
            {
                End += PageSize;
            }

            long Size = End - Start;

            return new AMemoryMapInfo(Start, Size, BaseEntry.Type, BaseEntry.Perm);
        }
        //找到虚拟地址对应的物理地址
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetPhys(long Position, AMemoryPerm Perm)
        {
            if (!HasPTEntry(Position))
            {
                if (Position < 0x08000000)
                {
                    Console.WriteLine($"HACK: Ignoring bad access at {Position:x16}");

                    return 0;
                }

                throw new VmmPageFaultException(Position);
            }

            PTEntry Entry = GetPTEntry(Position);

            long AbsPos = Entry.Position + (Position & PageMask);

            if (Entry.Map == PTMap.Mirror)
            {
                return GetPhys(AbsPos, Perm);
            }

            if (Entry.Map == PTMap.Unmapped)
            {
                throw new VmmPageFaultException(Position);
            }

            return AbsPos;
        }
        // 这个位置是否已经做了虚拟内存映射
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasPTEntry(long Position)
        {
            if (Position >> PTLvl0Bits + PTLvl1Bits + PTPageBits != 0)
            {
                return false;
            }

            long L0 = (Position >> PTLvl0Bit) & PTLvl0Mask; // 映射表的一级索引
            long L1 = (Position >> PTLvl1Bit) & PTLvl1Mask; // 映射表的二级索引

            if (PageTable[L0] == null)
            {
                return false;
            }

            return PageTable[L0][L1].Map != PTMap.Unmapped;
        }
        //获取虚拟地址对应的映射表表项
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PTEntry GetPTEntry(long Position)
        {
            long L0 = (Position >> PTLvl0Bit) & PTLvl0Mask;
            long L1 = (Position >> PTLvl1Bit) & PTLvl1Mask;

            if (PageTable[L0] == null)
            {
                return default(PTEntry);
            }

            return PageTable[L0][L1];
        }

        //把虚拟地址对应的表项放到映射表中
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetPTEntry(long Position, PTEntry Entry)
        {
            long L0 = (Position >> PTLvl0Bit) & PTLvl0Mask;
            long L1 = (Position >> PTLvl1Bit) & PTLvl1Mask;

            if (PageTable[L0] == null)
            {
                PageTable[L0] = new PTEntry[PTLvl1Size];
            }

            PageTable[L0][L1] = Entry;
        }
    }
}
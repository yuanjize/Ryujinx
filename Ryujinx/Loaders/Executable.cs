using ChocolArm64.Memory;
using Ryujinx.Loaders.Executables;
using Ryujinx.OsHle;
using System.Collections.Generic;
//把可执行文件加载到内存中
namespace Ryujinx.Loaders
{
    class Executable
    {
        private IElf    NsoData;//可执行文件数据
        private AMemory Memory; //内存管理器

        private ElfDyn[] Dynamic; //Dynamic段
        
        public long ImageBase { get; private set; } //可执行文件在文件中的基虚拟地址
        public long ImageEnd  { get; private set; } //可执行文件在文件中的结束虚拟地址
        // 解析各种段然后加载到内存
        public Executable(IElf NsoData, AMemory Memory, long ImageBase)
        {
            this.NsoData   = NsoData;
            this.Memory    = Memory;
            this.ImageBase = ImageBase;
            this.ImageEnd  = ImageBase;
            //把各段的数据写到对应的虚拟地址
            WriteData(ImageBase + NsoData.TextOffset, NsoData.Text, MemoryType.CodeStatic, AMemoryPerm.RX);
            WriteData(ImageBase + NsoData.ROOffset,   NsoData.RO,   MemoryType.Normal,     AMemoryPerm.Read);
            WriteData(ImageBase + NsoData.DataOffset, NsoData.Data, MemoryType.Normal,     AMemoryPerm.RW);

            if (NsoData.Text.Count == 0)
            {
                return;
            }

            long Mod0Offset = ImageBase + NsoData.Mod0Offset;

            int  Mod0Magic        = Memory.ReadInt32(Mod0Offset + 0x0);
            long DynamicOffset    = Memory.ReadInt32(Mod0Offset + 0x4)  + Mod0Offset;
            long BssStartOffset   = Memory.ReadInt32(Mod0Offset + 0x8)  + Mod0Offset;
            long BssEndOffset     = Memory.ReadInt32(Mod0Offset + 0xc)  + Mod0Offset;
            long EhHdrStartOffset = Memory.ReadInt32(Mod0Offset + 0x10) + Mod0Offset;
            long EhHdrEndOffset   = Memory.ReadInt32(Mod0Offset + 0x14) + Mod0Offset;
            long ModObjOffset     = Memory.ReadInt32(Mod0Offset + 0x18) + Mod0Offset;

            long BssSize = BssEndOffset - BssStartOffset;
            // bss段加载到内存 
            Memory.Manager.MapPhys(BssStartOffset, BssSize, (int)MemoryType.Normal, AMemoryPerm.RW);

            ImageEnd = BssEndOffset;

            List<ElfDyn> Dynamic = new List<ElfDyn>();
            // 解析dynamic段，看起来是动态链接用的
            while (true)
            {   
                long TagVal = Memory.ReadInt64(DynamicOffset + 0);
                long Value  = Memory.ReadInt64(DynamicOffset + 8);

                DynamicOffset += 0x10;

                ElfDynTag Tag = (ElfDynTag)TagVal;

                if (Tag == ElfDynTag.DT_NULL)
                {
                    break;
                }

                Dynamic.Add(new ElfDyn(Tag, Value));
            }

            this.Dynamic = Dynamic.ToArray();
        }

        //把数据加载到内存相应的地址
        private void WriteData(
            long        Position,//虚拟地址
            IList<byte> Data, //数据
            MemoryType  Type, //段落类型
            AMemoryPerm Perm) //段落rwx权限
        {   
            //分配物理内存并映射
            Memory.Manager.MapPhys(Position, Data.Count, (int)Type, Perm);
            //写数据
            for (int Index = 0; Index < Data.Count; Index++)
            {
                Memory.WriteByte(Position + Index, Data[Index]);
            }
        }
        //获取rel.dyn中的重定位信息 https://www.jianshu.com/p/2055bd794e58
        private ElfRel GetRelocation(long Position)
        {
            long Offset = Memory.ReadInt64(Position + 0);
            long Info   = Memory.ReadInt64(Position + 8);
            long Addend = Memory.ReadInt64(Position + 16);

            int RelType = (int)(Info >> 0);
            int SymIdx  = (int)(Info >> 32);

            ElfSym Symbol = GetSymbol(SymIdx);

            return new ElfRel(Offset, Addend, Symbol, (ElfRelType)RelType);
        }

        //读取指定位置的符号
        private ElfSym GetSymbol(int Index)
        {
            long StrTblAddr = ImageBase + GetFirstValue(ElfDynTag.DT_STRTAB);
            long SymTblAddr = ImageBase + GetFirstValue(ElfDynTag.DT_SYMTAB);// 符号表位置 

            long SymEntSize = GetFirstValue(ElfDynTag.DT_SYMENT); //表项大小

            long Position = SymTblAddr + Index * SymEntSize; //符号位置

            return GetSymbol(Position, StrTblAddr);
        }
        //读取指定位置的符号
        private ElfSym GetSymbol(long Position, long StrTblAddr)
        {
            int  NameIndex = Memory.ReadInt32(Position + 0);
            int  Info      = Memory.ReadByte(Position + 4);
            int  Other     = Memory.ReadByte(Position + 5);
            int  SHIdx     = Memory.ReadInt16(Position + 6);
            long Value     = Memory.ReadInt64(Position + 8);
            long Size      = Memory.ReadInt64(Position + 16);

            string Name = string.Empty;

            for (int Chr; (Chr = Memory.ReadByte(StrTblAddr + NameIndex++)) != 0;)
            {
                Name += (char)Chr;
            }

            return new ElfSym(Name, Info, Other, SHIdx, ImageBase, Value, Size);
        }
        //找到Dynamic tag对应的值
        private long GetFirstValue(ElfDynTag Tag)
        {
            foreach (ElfDyn Entry in Dynamic)
            {
                if (Entry.Tag == Tag)
                {
                    return Entry.Value;
                }
            }

            return 0;
        }
    }
}
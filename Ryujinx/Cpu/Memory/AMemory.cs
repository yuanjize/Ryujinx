using ChocolArm64.State;
using System;
using System.Collections.Generic;

namespace ChocolArm64.Memory
{   //内存管理器，负责分配和读写指定的内存
    public unsafe class AMemory
    {
        public AMemoryMgr Manager { get; private set; } //内存管理器

        private struct ExMonitor
        {
            public long Position { get; private set; }

            private bool ExState;

            public ExMonitor(long Position, bool ExState)
            {
                this.Position = Position;
                this.ExState  = ExState;
            }

            public bool HasExclusiveAccess(long Position)
            {
                return this.Position == Position && ExState;
            }

            public void Reset()
            {
                ExState = false;
            }
        }

        private Dictionary<int, ExMonitor> Monitors; //看起来是内存互斥锁

        private HashSet<long> ExAddrs;

        private byte* RamPtr;  // 虚拟机物理内存初始地址

        public AMemory(IntPtr Ram, AMemoryAlloc Allocator)
        {
            Manager = new AMemoryMgr(Allocator);

            Monitors = new Dictionary<int, ExMonitor>();

            ExAddrs = new HashSet<long>();

            RamPtr = (byte*)Ram;
        }

        public void RemoveMonitor(int ThreadId)
        {
            lock (Monitors)
            {
                Monitors.Remove(ThreadId);
            }
        }

        public void SetExclusive(ARegisters Registers, long Position)
        {
            lock (Monitors)
            {
                bool ExState = !ExAddrs.Contains(Position);

                if (ExState)
                {
                    ExAddrs.Add(Position);
                }

                ExMonitor Monitor = new ExMonitor(Position, ExState);

                if (!Monitors.TryAdd(Registers.ThreadId, Monitor))
                {
                    Monitors[Registers.ThreadId] = Monitor;
                }
            }
        }

        public bool TestExclusive(ARegisters Registers, long Position)
        {
            lock (Monitors)
            {
                if (!Monitors.TryGetValue(Registers.ThreadId, out ExMonitor Monitor))
                {
                    return false;
                }

                return Monitor.HasExclusiveAccess(Position);
            }
        }

        public void ClearExclusive(ARegisters Registers)
        {
            lock (Monitors)
            {
                if (Monitors.TryGetValue(Registers.ThreadId, out ExMonitor Monitor))
                {
                    Monitor.Reset();
                    ExAddrs.Remove(Monitor.Position);
                }
            }
        }
        // 下面函数都是从物理内存读写数据
        public sbyte ReadSByte(long Position) => (sbyte)ReadByte  (Position);
        public short ReadInt16(long Position) => (short)ReadUInt16(Position);
        public int   ReadInt32(long Position) =>   (int)ReadUInt32(Position);
        public long  ReadInt64(long Position) =>  (long)ReadUInt64(Position);

        public byte ReadByte(long Position)
        {
            return *((byte*)(RamPtr + Manager.GetPhys(Position, AMemoryPerm.Read)));
        }

        public ushort ReadUInt16(long Position)
        {
            long PhysPos = Manager.GetPhys(Position, AMemoryPerm.Read);

            if (BitConverter.IsLittleEndian && !IsPageCrossed(Position, 2))
            {
                return *((ushort*)(RamPtr + PhysPos));
            }
            else
            {
                return (ushort)(
                    ReadByte(Position + 0) << 0 |
                    ReadByte(Position + 1) << 8);
            }
        }

        public uint ReadUInt32(long Position)
        {
            long PhysPos = Manager.GetPhys(Position, AMemoryPerm.Read);

            if (BitConverter.IsLittleEndian && !IsPageCrossed(Position, 4))
            {
                return *((uint*)(RamPtr + PhysPos));
            }
            else
            {
                return (uint)(
                    ReadUInt16(Position + 0) << 0 |
                    ReadUInt16(Position + 2) << 16);
            }
        }

        public ulong ReadUInt64(long Position)
        {
            long PhysPos = Manager.GetPhys(Position, AMemoryPerm.Read);

            if (BitConverter.IsLittleEndian && !IsPageCrossed(Position, 8))
            {
                return *((ulong*)(RamPtr + PhysPos));
            }
            else
            {
                return
                    (ulong)ReadUInt32(Position + 0) << 0 |
                    (ulong)ReadUInt32(Position + 4) << 32;
            }
        }

        public AVec ReadVector128(long Position)
        {
            return new AVec()
            {
                X0 = ReadUInt64(Position + 0),
                X1 = ReadUInt64(Position + 8)
            };
        }

        public void WriteSByte(long Position, sbyte Value) => WriteByte  (Position,   (byte)Value);
        public void WriteInt16(long Position, short Value) => WriteUInt16(Position, (ushort)Value);
        public void WriteInt32(long Position, int   Value) => WriteUInt32(Position,   (uint)Value);
        public void WriteInt64(long Position, long  Value) => WriteUInt64(Position,  (ulong)Value);

        public void WriteByte(long Position, byte Value)
        {
            *((byte*)(RamPtr + Manager.GetPhys(Position, AMemoryPerm.Write))) = Value;
        }

        public void WriteUInt16(long Position, ushort Value)
        {
            long PhysPos = Manager.GetPhys(Position, AMemoryPerm.Write);

            if (BitConverter.IsLittleEndian && !IsPageCrossed(Position, 2))
            {
                *((ushort*)(RamPtr + PhysPos)) = Value;
            }
            else
            {
                WriteByte(Position + 0, (byte)(Value >> 0));
                WriteByte(Position + 1, (byte)(Value >> 8));
            }
        }

        public void WriteUInt32(long Position, uint Value)
        {
            long PhysPos = Manager.GetPhys(Position, AMemoryPerm.Write);

            if (BitConverter.IsLittleEndian && !IsPageCrossed(Position, 4))
            {
                *((uint*)(RamPtr + PhysPos)) = Value;
            }
            else
            {
                WriteUInt16(Position + 0, (ushort)(Value >> 0));
                WriteUInt16(Position + 2, (ushort)(Value >> 16));
            }
        }

        public void WriteUInt64(long Position, ulong Value)
        {
            long PhysPos = Manager.GetPhys(Position, AMemoryPerm.Write);

            if (BitConverter.IsLittleEndian && !IsPageCrossed(Position, 8))
            {
                *((ulong*)(RamPtr + PhysPos)) = Value;
            }
            else
            {
                WriteUInt32(Position + 0, (uint)(Value >> 0));
                WriteUInt32(Position + 4, (uint)(Value >> 32));
            }
        }

        public void WriteVector128(long Position, AVec Value)
        {
            WriteUInt64(Position + 0, Value.X0);
            WriteUInt64(Position + 8, Value.X1);
        }
        //在对应的位置写入N个字节是都会跨页面
        private bool IsPageCrossed(long Position, int Size)
        {
            return (Position & AMemoryMgr.PageMask) + Size > AMemoryMgr.PageSize;
        }
    }
}
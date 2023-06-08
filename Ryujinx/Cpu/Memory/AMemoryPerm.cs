using System;

namespace ChocolArm64.Memory
{
    // 内存区域的权限
    [Flags]
    public enum AMemoryPerm
    {
        None    = 0,
        Read    = 1 << 0,
        Write   = 1 << 1,
        Execute = 1 << 2,
        RW      = Read | Write,
        RX      = Read | Execute
    }
}
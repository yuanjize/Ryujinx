namespace Ryujinx.OsHle
{
    enum MemoryType
    {
        Unmapped               = 0,
        Io                     = 1,
        Normal                 = 2, //RO段，数据段，bss段，栈
        CodeStatic             = 3, //代码段
        CodeMutable            = 4,
        Heap                   = 5,
        SharedMemory           = 6,
        ModCodeStatic          = 8,
        ModCodeMutable         = 9,
        IpcBuffer0             = 10,
        MappedMemory           = 11,
        ThreadLocal            = 12,  // 放thread local slot的
        TransferMemoryIsolated = 13,
        TransferMemory         = 14,
        ProcessMemory          = 15,
        Reserved               = 16,
        IpcBuffer1             = 17,
        IpcBuffer3             = 18,
        KernelStack            = 19
    }
}
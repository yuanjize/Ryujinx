using ChocolArm64;
using ChocolArm64.Memory;
using Ryujinx.Loaders;
using Ryujinx.Loaders.Executables;
using Ryujinx.OsHle.Handles;
using Ryujinx.OsHle.Svc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
//进程，可以启动主线程然后执行指令翻译
namespace Ryujinx.OsHle
{
    class Process
    {
        private const int  MaxStackSize  = 8 * 1024 * 1024; //栈最大8m

        private const int  TlsSize       = 0x200; //一个tls slot的大小512bytes
        private const int  TotalTlsSlots = 32;  //tls slot个数
        private const int  TlsTotalSize  = TotalTlsSlots * TlsSize; // 所有tls slot一共占用的内存大小
        private const long TlsPageAddr   = (AMemoryMgr.AddrSize - TlsTotalSize) & ~AMemoryMgr.PageMask; // tls起始地址

        private Switch Ns;

        public int ProcessId { get; private set; } //进程id

        public AMemory Memory { get; private set; } //内存管理器

        private SvcHandler SvcHandler;  //负责系统调用

        private AThread MainThread; //主线程

        private ConcurrentDictionary<int, AThread> TlsSlots; //已经分配给线程的slot

        private List<Executable> Executables; //elf文件列表

        private long ImageBase; //基地址

        public Process(Switch Ns, AMemoryAlloc Allocator, int ProcessId)
        {
            this.Ns        = Ns;
            this.ProcessId = ProcessId;

            Memory      = new AMemory(Ns.Ram, Allocator);
            SvcHandler  = new SvcHandler(Ns, Memory);
            TlsSlots    = new ConcurrentDictionary<int, AThread>();
            Executables = new List<Executable>();

            ImageBase = 0x8000000; //128MB，这个值是可以变得，可以认为是下一个elf文件要加载的基地址
            // tls(thread local slot)页面映射到内存
            Memory.Manager.MapPhys(
                TlsPageAddr,
                TlsTotalSize,
                (int)MemoryType.ThreadLocal,
                AMemoryPerm.RW);
        }
        //可执行文件加载到内存中
        public void LoadProgram(IElf Program)
        {
            Executable Executable = new Executable(Program, Memory, ImageBase);

            Executables.Add(Executable);

            ImageBase = AMemoryHelper.PageRoundUp(Executable.ImageEnd);
        }

        public void SetEmptyArgs()
        {
            ImageBase += AMemoryMgr.PageSize;
        }

        public void InitializeHeap()
        {
            Memory.Manager.SetHeapAddr((ImageBase + 0x3fffffff) & ~0x3fffffff);
        }
        //创建并运行主线程，执行可执行文件
        public bool Run()
        {
            if (Executables.Count == 0)
            {
                return false;
            }

            long StackBot = TlsPageAddr - MaxStackSize;

            Memory.Manager.MapPhys(StackBot, MaxStackSize, (int)MemoryType.Normal, AMemoryPerm.RW);
            //创建虚拟线程
            int Handle = MakeThread(Executables[0].ImageBase, TlsPageAddr, 0, 48, 0);

            if (Handle == -1)
            {
                return false;
            }

            MainThread = Ns.Os.Handles.GetData<HThread>(Handle).Thread; //根据句柄拿到对应的虚拟线程对象

            MainThread.Execute(); //启动虚拟线程，翻译/执行指令

            return true;
        }
        // 终止所有线程
        public void StopAllThreads()
        {
            if (MainThread != null)
            {
                while (MainThread.IsAlive)
                {
                    MainThread.StopExecution();
                }
            }

            foreach (AThread Thread in TlsSlots.Values)
            {
                while (Thread.IsAlive)
                {
                    Thread.StopExecution();
                }
            }
        }
        //创建虚拟线程，配置对应的寄存器等资源
        public int MakeThread(
            long EntryPoint,
            long StackTop,
            long ArgsPtr,
            int  Priority,
            int  ProcessorId)
        {
            //创建物理线程（物理线程用来执行虚拟线程）
            AThread Thread = new AThread(Memory, EntryPoint, Priority);

            int TlsSlot = GetFreeTlsSlot(Thread); //给线程分配slot

            int Handle = Ns.Os.Handles.GenerateId(new HThread(Thread));// 线程句柄

            if (TlsSlot == -1 || Handle  == -1)
            {
                return -1;
            }

            Thread.Registers.SvcCall  += SvcHandler.SvcCall; //系统调用
            Thread.Registers.ProcessId = ProcessId; //进程id
            Thread.Registers.ThreadId  = Ns.Os.IdGen.GenerateId(); //线程id
            Thread.Registers.TlsAddr   = TlsPageAddr + TlsSlot * TlsSize;// 分配给该线程的thread slot 地址
            Thread.Registers.X0        = (ulong)ArgsPtr; //参数指针地址
            Thread.Registers.X1        = (ulong)Handle;  // 线程句柄
            Thread.Registers.X31       = (ulong)StackTop; // sp寄存器

            Thread.WorkFinished += ThreadFinished; //注册线程析构函数

            return Handle;
        }
        //找到空闲的slot分配给这个线程
        private int GetFreeTlsSlot(AThread Thread)
        {
            for (int Index = 1; Index < TotalTlsSlots; Index++)
            {
                if (TlsSlots.TryAdd(Index, Thread))
                {
                    return Index;
                }
            }

            return -1;
        }
        //线程退出，释放slot
        private void ThreadFinished(object sender, EventArgs e)
        {
            if (sender is AThread Thread)
            {
                TlsSlots.TryRemove(GetTlsSlot(Thread.Registers.TlsAddr), out _);

                Ns.Os.IdGen.DeleteId(Thread.ThreadId);
            }
        }
        //获取指定位置的tls
        private int GetTlsSlot(long Position)
        {
            return (int)((Position - TlsPageAddr) / TlsSize);
        }
    }
}
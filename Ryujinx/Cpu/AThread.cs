using ChocolArm64.Memory;
using ChocolArm64.State;
using System;
using System.Threading;
// AThread就是代表游戏机的虚拟线程，这个类会先创建物理线程，然后指令翻译器开始执行指令
namespace ChocolArm64
{
    class AThread
    {
        public ARegisters  Registers { get; private set; } //寄存器管理器
        public AMemory     Memory    { get; private set; }//内存管理器

        private ATranslator Translator; //指令翻译器？
        private Thread      Work;  //真正的系统线程，就是c#线程

        public event EventHandler WorkFinished; //用来在线程退出的时候释放资源

        public int ThreadId => Registers.ThreadId; //虚拟线程id1

        public bool IsAlive => Work.IsAlive; //线程是否还活着

        public long EntryPoint { get; private set; } //程序入口点
        public int  Priority   { get; private set; } //线程优先级

        public AThread(AMemory Memory, long EntryPoint = 0, int Priority = 0)
        {
            this.Memory     = Memory;
            this.EntryPoint = EntryPoint;
            this.Priority   = Priority;

            Registers  = new ARegisters();
            Translator = new ATranslator(this);
        }

        public void StopExecution() => Translator.StopExecution(); //停止执行

        public void Execute() => Execute(EntryPoint); //从入口点开始执行

        public void Execute(long EntryPoint) //从给定的位置开始执行
        {
            Work = new Thread(delegate()
            {
                Translator.ExecuteSubroutine(EntryPoint); //开始执行指令(模拟器模拟)

                Memory.RemoveMonitor(ThreadId);

                WorkFinished?.Invoke(this, EventArgs.Empty); //调用析构
            });
            //设置优先级
            if (Priority < 12)
            {
                Work.Priority = ThreadPriority.Highest;
            }
            else if (Priority < 24)
            {
                Work.Priority = ThreadPriority.AboveNormal;
            }
            else if (Priority < 36)
            {
                Work.Priority = ThreadPriority.Normal;
            }
            else if (Priority < 48)
            {
                Work.Priority = ThreadPriority.BelowNormal;
            }
            else
            {
                Work.Priority = ThreadPriority.Lowest;
            }
            //启动线程
            Work.Start();
        }
    }
}
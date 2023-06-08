using ChocolArm64;

namespace Ryujinx.OsHle.Handles
{   // 线程套壳
    class HThread
    {
        public AThread Thread { get; private set; }

        public HThread(AThread Thread)
        {
            this.Thread = Thread;
        }
    }
}
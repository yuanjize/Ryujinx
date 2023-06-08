using ChocolArm64.Exceptions;

namespace ChocolArm64.Memory
{
    // 负责内存分配和释放
    public class AMemoryAlloc
    {
        // 已经分配了的内存
        private long PhysPos;

        // 分配内存，参数是需要的内存大小(会自动放大到页的整数倍)，返回值是内存的模拟指针
        public long Alloc(long Size)
        {
            // 这次分配的内存的起始地址
            long Position = PhysPos;
            //页面对齐，内存分配必须是页的整数倍
            Size = AMemoryHelper.PageRoundUp(Size);

            PhysPos += Size;

            // 内存oom了
            if (PhysPos > AMemoryMgr.RamSize || PhysPos < 0)
            {
                throw new VmmOutOfMemoryException(Size);
            }

            return Position;
        }

        public void Free(long Position)
        {
            //TODO
        }

        // 还剩下多少内存
        public long GetFreeMem()
        {
            return AMemoryMgr.RamSize - PhysPos;
        }
    }
}
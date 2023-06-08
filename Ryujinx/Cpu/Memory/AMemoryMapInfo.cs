namespace ChocolArm64.Memory
{
    //一块（段）内存区域的信息和属性
    public struct AMemoryMapInfo
    {
        //内存起始位置
        public long Position { get; private set; }
        // 内存大小
        public long Size     { get; private set; }
        // 参考MemoryType,属于啥内存区域，例如堆内存
        public int  Type     { get; private set; }
        // 内存权限 读写执行啥的
        public AMemoryPerm Perm { get; private set; }

        public AMemoryMapInfo(long Position, long Size, int Type, AMemoryPerm Perm)
        {
            this.Position = Position;
            this.Size     = Size;
            this.Type     = Type;
            this.Perm     = Perm;
        }
    }
}
using System.Collections.Generic;

namespace ChocolArm64.Decoder
{   //一个代码块
    class ABlock
    {
        public long Position    { get; set; } //代码块起始地址
        public long EndPosition { get; set; } //代码块结束地址      

        public ABlock Next   { get; set; } // 没有跳转，或者bl跳转执行结束，要执行的代码块
        public ABlock Branch { get; set; } // 跳转的目的代码块

        public List<AOpCode> OpCodes { get; private set; } //代码块里面的opcode

        public ABlock()
        {
            OpCodes = new List<AOpCode>();
        }

        public ABlock(long Position) : this()
        {
            this.Position = Position;
        }
        //获取代码块的最后一个指令
        public AOpCode GetLastOp()
        {
            if (OpCodes.Count > 0)
            {
                return OpCodes[OpCodes.Count - 1];
            }

            return null;
        }
    }
}
using ChocolArm64.Instruction;

namespace ChocolArm64.Decoder
{   // 各种变种https://www.cnblogs.com/electronic/p/11019442.html
    class AOpCodeBImm : AOpCode
    {
        public long Imm { get; protected set; }

        public AOpCodeBImm(AInst Inst, long Position) : base(Inst, Position) { }
    }
}
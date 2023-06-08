using System;

namespace ChocolArm64.Instruction
{
    struct AInst
    {
        public AInstEmitter Emitter { get; private set; }
        public Type         Type    { get; private set; } //用来解析的指令的类，解析之后把指令的各个部分存储在这里面

        public static AInst Undefined => new AInst(AInstEmit.Und, null);

        public AInst(AInstEmitter Emitter, Type Type)
        {
            this.Emitter = Emitter;
            this.Type    = Type;
        }
    }
}
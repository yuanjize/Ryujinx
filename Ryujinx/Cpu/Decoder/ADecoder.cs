using ChocolArm64.Instruction;
using ChocolArm64.Memory;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ChocolArm64.Decoder
{
    static class ADecoder
    {
        public static (ABlock[] Graph, ABlock Root) DecodeSubroutine(ATranslator Translator, long Start)
        {
            Dictionary<long, ABlock> Visited    = new Dictionary<long, ABlock>(); //遍历过的块
            Dictionary<long, ABlock> VisitedEnd = new Dictionary<long, ABlock>(); //遍历过的块的尾地址

            Queue<ABlock> Blocks = new Queue<ABlock>();//广度优先遍历的队列
            //创建一个代码块对象，并进入广度优先遍历序列
            ABlock Enqueue(long Position)
            {
                if (!Visited.TryGetValue(Position, out ABlock Output))
                {
                    Output = new ABlock(Position);

                    Blocks.Enqueue(Output);

                    Visited.Add(Position, Output);
                }

                return Output;
            }

            ABlock Root = Enqueue(Start);
            //广度优先遍历，把所有的块按照树状组织起来
            while (Blocks.Count > 0)
            {   //从队列中拿来一个代码块填充
                ABlock Current = Blocks.Dequeue();
                // 填充代码块
                FillBlock(Translator.Thread.Memory, Current);

                //Set child blocks. "Branch" is the block the branch instruction
                //points to (when taken), "Next" is the block at the next address,
                //executed when the branch is not taken. For Unconditional Branches
                //(except BL/BLR that are sub calls) or end of executable, Next is null.
                // next就是跳转失败，或者bl执行完跳回来要继续执行的地址，或者没有块就是下一条指令的地址
                // branch是要跳转到的目的地址
                if (Current.OpCodes.Count > 0)
                {
                    bool HasCachedSub = false;
                    // 代码块的最后一个指令
                    AOpCode LastOp = Current.GetLastOp();

                    if (LastOp is AOpCodeBImm Op)
                    {
                        if (Op.Emitter == AInstEmit.Bl)
                        {
                            HasCachedSub = Translator.HasCachedSub(Op.Imm);
                        }
                        else
                        {
                            Current.Branch = Enqueue(Op.Imm);
                        }
                    }

                    if ((!(LastOp is AOpCodeBImmAl) &&
                         !(LastOp is AOpCodeBReg)) || HasCachedSub)
                    {
                        Current.Next = Enqueue(Current.EndPosition);
                    }
                }

                //If we have on the tree two blocks with the same end position,
                //then we need to split the bigger block and have two small blocks,
                //the end position of the bigger "Current" block should then be == to
                //the position of the "Smaller" block.
                //两个块如果结束地址一样，那么把大块缩小为新的一个小块
                while (VisitedEnd.TryGetValue(Current.EndPosition, out ABlock Smaller))
                {
                    if (Current.Position > Smaller.Position)
                    {
                        ABlock Temp = Smaller;

                        Smaller = Current;
                        Current = Temp;
                    }

                    Current.EndPosition = Smaller.Position;
                    Current.Next        = Smaller;
                    Current.Branch      = null;

                    Current.OpCodes.RemoveRange(  //拆分大块
                        Current.OpCodes.Count - Smaller.OpCodes.Count,
                        Smaller.OpCodes.Count);

                    VisitedEnd[Smaller.EndPosition] = Smaller;
                }

                VisitedEnd.Add(Current.EndPosition, Current);
            }

            //Make and sort Graph blocks array by position.
            ABlock[] Graph = new ABlock[Visited.Count];

            while (Visited.Count > 0)
            {
                ulong FirstPos = ulong.MaxValue;

                foreach (ABlock Block in Visited.Values)
                {
                    if (FirstPos > (ulong)Block.Position)
                        FirstPos = (ulong)Block.Position;
                }

                ABlock Current = Visited[(long)FirstPos];

                do
                {
                    Graph[Graph.Length - Visited.Count] = Current;

                    Visited.Remove(Current.Position);

                    Current = Current.Next;
                }
                while (Current != null);
            }

            return (Graph, Root);
        }
        //组成一个代码块的指令。从Position开始把一个块的代码放到Block.OpCodes中（遇到跳转指令或者svc指令为块末尾）
        private static void FillBlock(AMemory Memory, ABlock Block)
        {
            long Position = Block.Position;

            AOpCode OpCode;

            do
            {
                //拿到Position内存对应的指令的opCode，放到block中
                OpCode = DecodeOpCode(Memory, Position);

                Block.OpCodes.Add(OpCode);

                Position += 4;
            }
            while (!(IsBranch(OpCode) || IsException(OpCode)));

            Block.EndPosition = Position;
        }
        //是否是分支指令
        private static bool IsBranch(AOpCode OpCode)
        {
            return OpCode is AOpCodeBImm ||
                   OpCode is AOpCodeBReg;
        }
        // 异常处理指令
        private static bool IsException(AOpCode OpCode)
        {
            return OpCode.Emitter == AInstEmit.Svc ||
                   OpCode.Emitter == AInstEmit.Und;
        }
        /*
        从内存读出来指令，然后根据掩码得到对应的opcode，然后创建对应的opcode解码对象
        */
        public static AOpCode DecodeOpCode(AMemory Memory, long Position)
        {
            int OpCode = Memory.ReadInt32(Position); //获取四字节指令

            AInst Inst = AOpCodeTable.GetInst(OpCode); //获取对应的解码器

            AOpCode DecodedOpCode = new AOpCode(AInst.Undefined, Position);

            if (Inst.Type != null)
            {   //创建对应的opCode解码对象
                DecodedOpCode = CreateOpCode(Inst.Type, Inst, Position, OpCode);
            }

            return DecodedOpCode;
        }

        private delegate object OpActivator(AInst Inst, long Position, int OpCode); // AOpCode子类的构造函数

        private static Dictionary<Type, OpActivator> Activators = new Dictionary<Type, OpActivator>();// 存储AOpCode子类的构造函数
        // 创建Type对应的类
        private static AOpCode CreateOpCode(Type Type, AInst Inst, long Position, int OpCode)
        {
            if (Type == null)
            {
                throw new ArgumentNullException(nameof(Type));
            }

            if (!Activators.TryGetValue(Type, out OpActivator CreateInstance))
            {
                Type[] ArgTypes = new Type[] { typeof(AInst), typeof(long), typeof(int) };

                DynamicMethod Mthd = new DynamicMethod($"{Type.Name}_Create", Type, ArgTypes);

                ILGenerator Generator = Mthd.GetILGenerator();

                Generator.Emit(OpCodes.Ldarg_0);
                Generator.Emit(OpCodes.Ldarg_1);
                Generator.Emit(OpCodes.Ldarg_2);
                Generator.Emit(OpCodes.Newobj, Type.GetConstructor(ArgTypes));
                Generator.Emit(OpCodes.Ret);

                CreateInstance = (OpActivator)Mthd.CreateDelegate(typeof(OpActivator));

                Activators.Add(Type, CreateInstance);
            }

            return (AOpCode)CreateInstance(Inst, Position, OpCode);
        }
    }
}
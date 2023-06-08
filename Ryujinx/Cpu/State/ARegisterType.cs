namespace ChocolArm64.State
{   //寄存器的种类
    enum ARegisterType
    {
        Flag,// 状态寄存器，里面一堆flag那种
        Int, //通用寄存器
        Vector //SIMD寄存器
    }
}
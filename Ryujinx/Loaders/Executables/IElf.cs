using System.Collections.ObjectModel;

namespace Ryujinx.Loaders.Executables
{
    interface IElf
    {   // 代码段数据
        ReadOnlyCollection<byte> Text { get; }
        // 只读数据段
        ReadOnlyCollection<byte> RO   { get; }
        // 数据段
        ReadOnlyCollection<byte> Data { get; }

        int Mod0Offset { get; } //mod0段在虚拟内存中的偏移位置（相对于基地址）
        int TextOffset { get; } //代码段在虚拟内存中的偏移位置（相对于基地址）
        int ROOffset   { get; } //代码段在虚拟内存中的偏移位置（相对于基地址）
        int DataOffset { get; } //数据段在虚拟内存中的偏移位置（相对于基地址）
        int BssSize    { get; } //bss段在虚拟内存中的偏移位置（相对于基地址）
    }
}
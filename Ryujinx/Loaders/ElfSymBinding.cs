namespace Ryujinx.Loaders
{   // 符号绑定 https://docs.oracle.com/cd/E19253-01/819-7050/chapter6-79797/index.html
    enum ElfSymBinding
    {
        STB_LOCAL  = 0,
        STB_GLOBAL = 1,
        STB_WEAK   = 2
    }
}
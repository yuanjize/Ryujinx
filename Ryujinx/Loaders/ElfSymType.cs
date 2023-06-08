namespace Ryujinx.Loaders
{   // 符号类型 https://docs.oracle.com/cd/E19253-01/819-7050/chapter6-79797/index.html
    enum ElfSymType
    {
        STT_NOTYPE  = 0,
        STT_OBJECT  = 1,
        STT_FUNC    = 2,
        STT_SECTION = 3,
        STT_FILE    = 4,
        STT_COMMON  = 5,
        STT_TLS     = 6
    }
}
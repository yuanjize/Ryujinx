namespace Ryujinx.Loaders
{
    //符号可见性https://docs.oracle.com/cd/E19253-01/819-7050/chapter6-79797/index.html
    enum ElfSymVisibility
    {
        STV_DEFAULT   = 0,
        STV_INTERNAL  = 1,
        STV_HIDDEN    = 2,
        STV_PROTECTED = 3 
    }
}
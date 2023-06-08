namespace Ryujinx.Loaders
{   //dynamic段 https://ctf-wiki.org/executable/elf/structure/dynamic-sections/
    struct ElfDyn
    {
        public ElfDynTag Tag { get; private set; }

        public long Value { get; private set; }

        public ElfDyn(ElfDynTag Tag, long Value)
        {
            this.Tag   = Tag;
            this.Value = Value;
        }
    }
}
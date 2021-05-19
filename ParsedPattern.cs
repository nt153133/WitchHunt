namespace WitchHunt
{
    using System;

    public ref struct ParsedPattern
    {
        public Span<byte> BytesToSearch;

        public Span<byte> Mask;

        public Span<string> PostPattern;
    }
}
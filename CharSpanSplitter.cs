﻿namespace WitchHunt
{
    using System;

    public readonly ref struct CharSpanSplitter
    {
        private readonly ReadOnlySpan<char> _input;

        public CharSpanSplitter(ReadOnlySpan<char> input) => _input = input;

        public Enumerator GetEnumerator() => new Enumerator(_input);

        public ref struct Enumerator
        {
            public readonly ReadOnlySpan<char> Input;
            public int WordPos;

            public Enumerator(ReadOnlySpan<char> input)
            {
                Input = input;
                WordPos = 0;
                Current = default;
            }

            public ReadOnlySpan<char> Current { get; private set; }

            public bool MoveNext()
            {
                for (var i = WordPos; i <= Input.Length; i++)
                {
                    if (i != Input.Length && !char.IsWhiteSpace(Input[i]))
                    {
                        continue;
                    }

                    Current = Input[WordPos..i];
                    WordPos = i + 1;
                    return true;
                }

                return false;
            }
        }
    }

    public static class CharSpanExtensions
    {
        public static CharSpanSplitter Split(this ReadOnlySpan<char> input)
            => new(input);

        public static CharSpanSplitter Split(this Span<char> input)
            => new(input);
    }
}
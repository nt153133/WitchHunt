namespace WitchHunt
{
    using System;
    using System.Collections.Generic;

    public class WitchHunt : ISearcher
    {
        // ReSharper disable once SA1401
        // ReSharper disable once MemberCanBePrivate.Global
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Optimization")]
        public readonly ReadOnlyMemory<byte> Data;

        public WitchHunt(byte[] assemblyData, IntPtr imageBase)
        {
            Data = assemblyData;
            ImageBase = imageBase;
        }

        public WitchHunt(Span<byte> assemblyData, IntPtr imageBase)
        {
            Data = assemblyData.ToArray();
            ImageBase = imageBase;
        }

        public WitchHunt(ref ReadOnlySpan<byte> assemblyData, IntPtr imageBase)
        {
            Data = assemblyData.ToArray();
            ImageBase = imageBase;
        }

        public WitchHunt(ReadOnlySpan<byte> assemblyData, IntPtr imageBase)
        {
            Data = assemblyData.ToArray();
            ImageBase = imageBase;
        }

        public WitchHunt(Memory<byte> assemblyData, IntPtr imageBase)
        {
            Data = assemblyData;
            ImageBase = imageBase;
        }

        private enum Keywords
        {
            Add,
            Sub,
            Read8,
            Read16,
            Read32,
            Read64,
            Tracerelative,
            Tracecall,
        }

        public IntPtr ImageBase { get; }

        public IntPtr Search(string pattern)
        {
            var patternCorrect = GetPatternBytes(pattern, out var parsedPattern);
            return !patternCorrect ? IntPtr.Zero : FindSingle(parsedPattern, IntPtr.Zero, Data.Length);
        }

        public IntPtr Search(string pattern, IntPtr start, int maxSearchLength)
        {
            var patternCorrect = GetPatternBytes(pattern, out var parsedPattern);
            return !patternCorrect ? IntPtr.Zero : FindSingle(parsedPattern, start, maxSearchLength);
        }

        public IntPtr[] SearchMany(string pattern)
        {
            return FindMany(pattern);
        }

        public ReadOnlySpan<byte> GetSlice(int start, int length)
        {
            return Data.Span.Slice(start, length);
        }

        private static bool GetPatternBytes(string pattern, out ParsedPattern parsedPattern)
        {
            var enumerator = pattern.AsMemory(0).Span.Split().GetEnumerator();

            var bytes = new Span<byte>(new byte[pattern.Length]);
            var mask = new Span<byte>(new byte[pattern.Length]);
            List<string> post = null;
            parsedPattern = default;
            var length = 0;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Length > 2)
                {
                    post = new List<string>(3);
                    break;
                }

                if (enumerator.Current.Length == 0)
                {
                    continue;
                }

                if (!enumerator.Current.IsValidHex())
                {
                    return false;
                }

                bytes[length] = enumerator.Current.GetByte();
                mask[length] = enumerator.Current.GetMask();
                length++;
            }

            if (post != null && enumerator.WordPos <= enumerator.Input.Length + 1)
            {
                post.Add(new string(enumerator.Current));
                while (enumerator.MoveNext())
                {
                    post.Add(new string(enumerator.Current));
                }
            }

            parsedPattern.BytesToSearch = bytes[..length];
            parsedPattern.Mask = mask[..length];
            if (post != null)
            {
                parsedPattern.PostPattern = new Span<string>(post.ToArray());
            }

            return true;
        }

        private IntPtr[] FindMany(string pattern)
        {
            var final = new List<IntPtr>();
            IntPtr result;
            var start = ImageBase;

            var max = Data.Length;
            var patternCorrect = GetPatternBytes(pattern, out var parsedPattern);

            if (!patternCorrect)
            {
                return final.ToArray();
            }

            do
            {
                result = FindSingle(parsedPattern, start - ImageBase.ToInt32(), max);
                if (result == IntPtr.Zero)
                {
                    continue;
                }

                start = result + parsedPattern.BytesToSearch.Length;
                final.Add(result);
            }
            while (result != IntPtr.Zero);

            return final.ToArray();
        }

        private IntPtr FindSingle(ParsedPattern parsedPattern, IntPtr start, int max)
        {
            var matchingPtr = IntPtr.Zero;
            var index = start.ToInt32();
            var bytesToSearchLength = parsedPattern.BytesToSearch.Length;
            while (index + bytesToSearchLength <= Data.Length && (index - start.ToInt32() < max))
            {
                var match = Match(index, parsedPattern.BytesToSearch, parsedPattern.Mask);
                switch (match)
                {
                    case < 0:
                        index += -match; // partial match
                        continue;
                    case 0:
                        index += bytesToSearchLength; // no partial matches. Skip this section...
                        continue;
                }

                matchingPtr = new IntPtr(index);
                break;
            }

            if (matchingPtr == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var resultPointer = matchingPtr;
            if (parsedPattern.PostPattern.Length == 0)
            {
                return new IntPtr(matchingPtr.ToInt64() + ImageBase.ToInt64());
            }

            for (var i = 0; i < parsedPattern.PostPattern.Length; i++)
            {
                if (parsedPattern.PostPattern[i].Length <= 2)
                {
                    continue;
                }

                var foundKeyword = Enum.TryParse<Keywords>(parsedPattern.PostPattern[i], true, out var keyword);

                if (foundKeyword)
                {
                    switch (keyword)
                    {
                        case Keywords.Add:
                        {
                            var idx = int.Parse(parsedPattern.PostPattern[i + 1]);
                            i++;
                            resultPointer += idx;
                            break;
                        }

                        case Keywords.Sub:
                        {
                            var idx = int.Parse(parsedPattern.PostPattern[i + 1]);
                            i++;
                            resultPointer -= idx;
                            break;
                        }

                        case Keywords.Read8:
                            return new IntPtr(Data.Span[resultPointer.ToInt32()]);

                        case Keywords.Read16:
                            return new IntPtr(BitConverter.ToInt16(Data.Span.Slice(resultPointer.ToInt32(), 2)));

                        case Keywords.Read32:
                            return new IntPtr(BitConverter.ToInt32(Data.Span.Slice(resultPointer.ToInt32(), 4)));

                        case Keywords.Read64:
                            return new IntPtr(BitConverter.ToInt32(Data.Span.Slice(resultPointer.ToInt32(), 8)));

                        case Keywords.Tracerelative:
                            return new IntPtr(resultPointer.ToInt32() + 4 + BitConverter.ToInt32(Data.Span.Slice(resultPointer.ToInt32(), 4)) + ImageBase.ToInt64());

                        case Keywords.Tracecall:
                            return new IntPtr(resultPointer.ToInt32() + 5 + BitConverter.ToInt32(Data.Span.Slice(resultPointer.ToInt32() + 1, 4)) + ImageBase.ToInt64());

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return new IntPtr(resultPointer.ToInt32() + ImageBase.ToInt64());
        }

        /// <summary>
        /// Tests if the memory contains a sequence of contiguous bytes that match the
        /// given byte array at all bit positions where the mask contains an "on" bit.
        ///
        /// 1 if there is a match
        /// 0 if there is no match
        /// -i if no match is found, this is the number of bytes that can be safely skipped.
        /// </summary>
        private int Match(int index, ReadOnlySpan<byte> bytesToMatch, ReadOnlySpan<byte> masks)
        {
            try
            {
                if (index + bytesToMatch.Length > Data.Length)
                {
                    return 0;
                }

                // basically byte[] of the data buffer, chunk of the .text/rdata bytes
                var dataBuffer = Data.Span.Slice(index, bytesToMatch.Length);

                // first check if the pattern entirely matches the bytes
                int i;
                for (i = 0; i < bytesToMatch.Length; i++)
                {
                    if ((dataBuffer[i] & masks[i]) != (bytesToMatch[i] & masks[i]))
                    {
                        break;
                    }
                }

                // Full pattern of bytes matched
                if (i == bytesToMatch.Length)
                {
                    return 1;
                }

                var indexOf = dataBuffer[1..].IndexOf(bytesToMatch[0]);
                if (indexOf == -1)
                {
                    return -bytesToMatch.Length;
                }

                return -(indexOf + 1);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
namespace WitchHunt
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

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
                if (enumerator.Current.Length == 6)
                {
                    if (enumerator.Current[0] == 'S' || enumerator.Current[0] == 's')
                    {
                        //bytes = new Span<byte>(new byte[pattern.Length-7]);
                       // mask = new Span<byte>(new byte[pattern.Length-7]);
                        continue;
                    }
                }

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
            var startInt = start.ToInt32();
            var index = startInt;
            var bytesToSearchLength = parsedPattern.BytesToSearch.Length;
            var data = Data.Span; // cache to avoid repeated ReadOnlyMemory<byte>.Span property access
            while (index + bytesToSearchLength <= data.Length && (index - startInt < max))
            {
                var match = Match(data, index, parsedPattern.BytesToSearch, parsedPattern.Mask);
                if (match < 0)
                {
                    index += -match; // partial match
                    continue;
                }

                if (match == 0)
                {
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Match(ReadOnlySpan<byte> data, int index, ReadOnlySpan<byte> bytesToMatch, ReadOnlySpan<byte> masks)
        {
            if (index + bytesToMatch.Length > data.Length)
            {
                return 0;
            }

            // basically byte[] of the data buffer, chunk of the .text/rdata bytes
            var dataBuffer = data.Slice(index, bytesToMatch.Length);

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

            // Find the next byte that matches our starting byte.
            var mask = masks[0];
            var bmo = bytesToMatch[0] & mask;

            // When the first byte has no wildcard, use IndexOf for SIMD-accelerated search.
            if (mask == 0xFF)
            {
                var remaining = dataBuffer.Slice(1);
                var nextIdx = remaining.IndexOf((byte)bmo);
                return nextIdx < 0 ? -dataBuffer.Length : -(nextIdx + 1);
            }

            var indexOf = 1;
            for (; indexOf < dataBuffer.Length; indexOf++)
            {
                if ((dataBuffer[indexOf] & mask) != bmo)
                {
                    continue;
                }

                break;
            }

            return -indexOf;
        }
    }
}

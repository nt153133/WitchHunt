namespace WitchHunt
{
    using System;
    using System.Runtime.CompilerServices;

    public static class Utilities
    {
        private const string Wildcards = "?";
        private const string Hexchars = "0123456789abcdefABCDEF" + Wildcards;

        public static byte GetMask(this ReadOnlySpan<char> tok)
        {
            if (tok.Length <= 1)
            {
                return 0;
            }

            var index1 = Wildcards.IndexOf(tok[0]);
            var index2 = Wildcards.IndexOf(tok[1]);
            if (index1 >= 0 && index2 >= 0)
            {
                return 0x00;
            }

            if (index1 >= 0 && true)
            {
                return 0x0F;
            }

            if (index1 < 0 && index2 >= 0)
            {
                return 0xF0;
            }

            return 0xFF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidHex(this ReadOnlySpan<char> str)
        {
            if (str.Length > 16)
            {
                return false;
            }

            foreach (var t in str)
            {
                if (!Hexchars.Contains(t))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the value of the given hex digit character.
        /// </summary>
        /// <returns>Value of char as hex.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HexValueOf(this char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }

            return 0;
        }

        /// <summary>
        /// Returns the byte value to be used for the given hex bytes.  Handles wildcard
        /// characters by return treating them as 0s.
        /// </summary>
        /// <returns>Byte value of the string.</returns>
        public static byte GetByte(this ReadOnlySpan<char> tok)
        {
            if (tok.Length <= 1)
            {
                return 0;
            }

            var c1 = tok[0] == '?' ? 0 : HexValueOf(tok[0]);
            var c2 = tok[1] == '?' ? 0 : HexValueOf(tok[1]);

            return (byte)((c1 * 16) + c2);
        }
    }
}
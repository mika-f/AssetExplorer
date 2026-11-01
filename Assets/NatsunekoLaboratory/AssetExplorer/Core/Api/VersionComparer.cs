// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;

namespace NatsunekoLaboratory.AssetExplorer.Api
{
    /// <summary>
    ///     Natural ordering for version labels: runs of digits compare as numbers, everything else ordinally.
    /// </summary>
    internal static class VersionComparer
    {
        public static int Compare(string a, string b)
        {
            if (a == null || b == null)
                return a == null ? b == null ? 0 : -1 : 1;

            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    var x = ReadRun(a, ref i, true);
                    var y = ReadRun(b, ref j, true);

                    var cmp = CompareNumbers(x, y);
                    if (cmp != 0)
                        return cmp;
                }
                else
                {
                    var x = ReadRun(a, ref i, false);
                    var y = ReadRun(b, ref j, false);

                    var cmp = string.CompareOrdinal(x, y);
                    if (cmp != 0)
                        return Math.Sign(cmp);
                }
            }

            return (a.Length - i).CompareTo(b.Length - j);
        }

        private static string ReadRun(string s, ref int index, bool digits)
        {
            var start = index;
            while (index < s.Length && char.IsDigit(s[index]) == digits)
                index++;

            return s.Substring(start, index - start);
        }

        private static int CompareNumbers(string x, string y)
        {
            x = x.TrimStart('0');
            y = y.TrimStart('0');

            return x.Length != y.Length ? x.Length.CompareTo(y.Length) : Math.Sign(string.CompareOrdinal(x, y));
        }
    }
}

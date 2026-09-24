using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProjectBrowserPlus.Core
{
    /// <summary>"Sheet 2" &lt; "Sheet 10" style comparison (as Windows Explorer does).</summary>
    public sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new NaturalComparer();

        public int Compare(string a, string b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            int ia = 0, ib = 0;
            while (ia < a.Length && ib < b.Length)
            {
                if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
                {
                    int sa = ia, sb = ib;
                    while (ia < a.Length && char.IsDigit(a[ia])) ia++;
                    while (ib < b.Length && char.IsDigit(b[ib])) ib++;
                    var na = a.Substring(sa, ia - sa).TrimStart('0');
                    var nb = b.Substring(sb, ib - sb).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                    var c = string.CompareOrdinal(na, nb);
                    if (c != 0) return c;
                }
                else
                {
                    var c = CultureInfo.CurrentCulture.CompareInfo.Compare(a[ia].ToString(), b[ib].ToString(), CompareOptions.IgnoreCase);
                    if (c != 0) return c;
                    ia++; ib++;
                }
            }
            return (a.Length - ia).CompareTo(b.Length - ib);
        }
    }
}

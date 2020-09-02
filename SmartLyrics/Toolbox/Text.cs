using System;
using System.Linq;
// ReSharper disable EmptyEmbeddedStatement

namespace SmartLyrics.Toolbox
{
    internal static class Text
    {
        public static int Distance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                if (string.IsNullOrEmpty(t))
                    return 0;
                return t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // initialize the top and right of the table to 0, 1, 2, ...
            for (int i = 0; i <= n; d[i, 0] = i++);
            for (int j = 1; j <= m; d[0, j] = j++);

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    int min1 = d[i - 1, j] + 1;
                    int min2 = d[i, j - 1] + 1;
                    int min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }
            return d[n, m];
        }

        public static bool ContainsAll(this string toSearch, params string[] substrings)
        {
            bool matches = substrings.All(sub => toSearch.Contains(sub));
            return matches;
        }

        public static bool ContainsAny(this string toSearch, params string[] substrings)
        {
            foreach (string substring in substrings)
            {
                if (toSearch.Contains(substring))
                    return true;
            }

            return false;
        }
    }
}
﻿using Android.Util;

using System.Text.RegularExpressions;
using System.Threading.Tasks;

using WanaKanaNet;

namespace SmartLyrics.Toolbox
{
    internal static class JapaneseTools
    {
        public static async Task<string> StripJapanese(this string input)
        {
            Log.WriteLine(LogPriority.Verbose, "JapaneseTools", $"StripJapanese: Processing string '{input}'");

            if (WanaKana.IsJapanese(input))
            {
                string converted = await HTTPRequests.PostRequest(Globals.romanizeConvertURL + "?to=romaji&mode=spaced&useHTML=false", input);
                Log.WriteLine(LogPriority.Info, "JapaneseTools", "StripJapanese: Converted string from API is " + converted);

                input = converted;
            }
            else if (WanaKana.IsMixed(input))
            {
                //checks if title follows "romaji (japanese)" format
                //and keeps only romaji.
                if (input.Contains("("))
                {
                    string inside = Regex.Match(input, @"(?<=\()(.*?)(?=\))").Value;
                    string outside = Regex.Replace(input, @" ?\(.*?\)", "");

                    if (ContainsJapanese(inside))
                    {
                        input = outside;
                    }
                    else if (ContainsJapanese(outside))
                    {
                        input = inside;
                    }
                }
            }

            //returns unchaged input if it doesn't follow format
            return input;
        }

        public static bool ContainsJapanese(this string text) => WanaKana.IsMixed(text) || WanaKana.IsJapanese(text);
    }
}
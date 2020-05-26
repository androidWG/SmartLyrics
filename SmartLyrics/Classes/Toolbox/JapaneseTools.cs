using Android.Util;

using System.Text.RegularExpressions;
using System.Threading.Tasks;

using WanaKanaNet;

namespace SmartLyrics.Toolbox
{
    static class JapaneseTools
    {
        public static async Task<string> StripJapanese(this string input)
        {
            if (WanaKana.IsJapanese(input))
            {
                string converted = await HTTPRequests.PostRequest(Globals.romanizeConvertURL + "?to=romaji&mode=spaced&useHTML=false", input);
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "file_name_here.cs: Converted string is " + converted);

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
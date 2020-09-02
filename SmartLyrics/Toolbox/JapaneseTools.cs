using static SmartLyrics.Common.Logging;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using WanaKanaNet;
using Microsoft.AppCenter.Analytics;
using System.Collections.Generic;
using SmartLyrics.Common;

namespace SmartLyrics.Toolbox
{
    internal static class JapaneseTools
    {
        #region Enumerators

        private enum TargetSyllabary
        {
            Romaji,
            Hiragana,
            Katakana
        }

        private enum Mode
        {
            Normal,
            Spaced,
            Okurigana,
            Furigana
        }

        private enum RomajiSystem
        {
            Nippon,
            Passport,
            Hepburn
        }
        #endregion

        //TODO: Add clearing up of romanized text, to eliminate spaces before and after //will be done on RomanizationService
        private static async Task<string> GetTransliteration(string text,
                                                            bool useHtml,
                                                            TargetSyllabary to = TargetSyllabary.Romaji,
                                                            Mode mode = Mode.Spaced,
                                                            RomajiSystem system = RomajiSystem.Hepburn)
        {
            if (!text.ContainsJapanese() || string.IsNullOrEmpty(text)) return text;

            string logString = text.Truncate(30);
            Log(Type.Processing, $"Getting transliteration for '{logString}'");
            Analytics.TrackEvent("Getting transliteration from RomanizationService", new Dictionary<string, string> {
                { "Input", logString },
                { "TargetSyllabary", to.ToString("G") }
            });

            // ReSharper disable InconsistentNaming
            string _to = to switch
            {
                TargetSyllabary.Romaji => "romaji",
                TargetSyllabary.Hiragana => "hiragana",
                TargetSyllabary.Katakana => "katakana",
                _ => ""
            };

            string _mode = mode switch
            {
                Mode.Normal => "normal",
                Mode.Spaced => "spaced",
                Mode.Okurigana => "okurigana",
                Mode.Furigana => "furigana",
                _ => ""
            };

            string _system = system switch
            {
                RomajiSystem.Nippon => "nippon",
                RomajiSystem.Passport => "passport",
                RomajiSystem.Hepburn => "hepburn",
                _ => ""
            };
            // ReSharper restore InconsistentNaming

            string queryParams = $"?to={_to}&mode={_mode}&romajiSystem={_system}&useHTML={useHtml.ToString().ToLowerInvariant()}";
            string transliterated = await HttpRequests.PostRequest(Globals.RomanizeConvertUrl + queryParams, text);

            return transliterated;
        }
        
        public static async Task<RomanizedSong> RomanizeSong(Song song, bool useHtml)
        {
            //TODO: Add furigana/okurigana support across app
            Task<string> awaitLyrics = GetTransliteration(song.Lyrics, useHtml);
            Task<string> awaitTitle = song.Title.StripJapanese();
            Task<string> awaitArtist = song.Artist.StripJapanese();
            Task<string> awaitFeat = song.FeaturedArtist.StripJapanese();
            Task<string> awaitAlbum = song.Album.StripJapanese();

            await Task.WhenAll(awaitLyrics, awaitTitle, awaitArtist, awaitFeat, awaitAlbum);

            RomanizedSong romanized = new RomanizedSong
            {
                Title = await awaitTitle,
                Artist = await awaitArtist,
                Album = await awaitAlbum,
                Lyrics = await awaitLyrics,
                Id = song.Id
            };

            // Log(Type.Event, "Romanized song with ID " + song.Id);
            // Analytics.TrackEvent("Romanized song info", new Dictionary<string, string> {
            //     { "SongID", song.Id.ToString() }
            // });
            return romanized;
        }
        
        public static async Task<string> StripJapanese(this string input)
        {
            //TODO: Add support for titles like Bakamitai ばかみたい (Romanized)

            if (WanaKana.IsJapanese(input))
            {
                string converted = await GetTransliteration(input, false);

                input = converted;
            }
            //TODO: Fix romanization issues, like with the string "潜潜話 (Hisohiso Banashi)", which only contains kanji and is not detected as mixed
            else if (WanaKana.IsMixed(input) || WanaKana.IsKanji(input))
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

            //Returns unchanged input if it doesn't follow format
            return input;
        }

        public static bool ContainsJapanese(this string text) => WanaKana.IsMixed(text) || WanaKana.IsJapanese(text);
    }
}
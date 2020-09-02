using SmartLyrics.Common;
using static SmartLyrics.Common.Logging;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
// ReSharper disable All

namespace SmartLyrics.Toolbox
{
    internal class SongParsing
    {
        //returns as index 0 the title of the notification and as index 1 the artist
        public static Song GetTitleAndArtistFromExtras(string extras)
        {
            string _title = Regex.Match(extras, @"(?<=android\.title=)(.*?)(?=, android\.)").ToString();
            if (_title.Contains("Remix") || _title.Contains("remix") || _title.Contains("Mix"))
            {
                _title = Regex.Replace(_title, @"\(feat\..*?\)", "");
            }
            else
            {
                _title = Regex.Replace(_title, @"\(.*?\)", "");
                _title.Trim();
            }

            string _artist = Regex.Match(extras, @"(?<=android\.text=)(.*?)(?=, android\.)").ToString();

            Song _output = new Song() { Title = _title, Artist = _artist };

            return _output;
        }

        public static Song StripSongForSearch(Song input)
        {
            /* Strips artist and title strings for remixes, collabs and edits.
             * Made to work with stiuations like the ones below:
             * - Song Name (Artist's Remix)
             * - Song Name (feat. Artist) [Other Artist's Remix]
             * - Artist 1 & Artist 2
             * And any other variation of these. Since most services only
             * use brackets and parenthesis, we separate everything inside
             * them to parse those strings.
             * 
             * The objective is to search for the original song in case
             * of remixes so if a remixed version isn't on Genius, the original
             * will be shown to the user. Also, featuring 'tags' are
             * never used in Genius, so we should always remove those.
             */

            string _strippedTitle = input.Title;
            string _strippedArtist = input.Artist;

            //removes any Remix, Edit, or Featuring info encapsulated
            //in parenthesis or brackets
            if (input.Title.Contains("(") || input.Title.Contains("["))
            {
                List<Match> _inside = Regex.Matches(input.Title, @"\(.*?\)").ToList();
                List<Match> _insideBrk = Regex.Matches(input.Title, @"\[.*?\]").ToList();
                _inside = _inside.Concat(_insideBrk).ToList();

                Log(Type.Error, $"{_inside.Count()}");

                foreach (Match _s in _inside)
                {
                    if (_s.Value.ToLowerInvariant().ContainsAny("feat", "ft", "featuring", "edit", "mix"))
                    {
                        _strippedTitle = input.Title.Replace(_s.Value, "");
                    }
                }
            }

            _strippedTitle.Replace("🅴", ""); //remove "🅴" used by Apple Music for explicit songs

            if (input.Artist.Contains(" & "))
            {
                _strippedArtist = Regex.Replace(input.Artist, @" & .*$", "");
            }

            _strippedTitle.Trim();
            _strippedArtist.Trim();

            Song _output = new Song() { Title = _strippedTitle, Artist = _strippedArtist };
            Log(Type.Processing, $"Stripped title from {input} to {_output.Title}");
            return _output;
        }

        public static async Task<int> CalculateLikeness(Song result, Song notification, int index)
        {
            /* This method is supposed to accurately measure how much the detected song
             * is like the song from a search result. It's based on the Text Distance concept.
             * 
             * It's made to work with titles and artists like:
             * - "Around the World" by "Daft Punk" | Standard title
             * - "Mine All Day" by "PewDiePie & BoyInABand" | Collabs
             * - "さまよいよい咽　(Samayoi Yoi Ondo)" by "ずとまよ中でいいのに　(ZUTOMAYO)" | Titles and/or artists with romanization included
             * 
             * And any combination of such. Works in conjunction with a search method that includes
             * StripSongForSearch, so that titles with (Remix), (Club Mix) and such can be
             * found if they exist and still match if they don't.
             * 
             * For example, "Despacito (Remix)" will match exactly with a Genius search since they have a
             * remixed and non-remixed version. "Daddy Like (Diveo Remix)" will match the standard
             * song, "Daddy Like", since Genius doesn't have the remixed version.
            */

            string _title = result.Title.ToLowerInvariant();
            string _artist = result.Artist.ToLowerInvariant();

            string _ntfTitle = notification.Title.ToLowerInvariant();
            _ntfTitle.Replace("🅴", ""); //remove "🅴" used by Apple Music for explicit songs
            //remove anything inside brackets since almost everytime
            //it's not relevant info
            _ntfTitle = Regex.Replace(_ntfTitle, @"\[.*?\]", "").Trim();
            string _ntfArtist = notification.Artist.ToLowerInvariant();

            _title = await _title.StripJapanese();
            _artist = await _artist.StripJapanese();

            int _titleDist = Text.Distance(_title, _ntfTitle);
            int _artistDist = Text.Distance(_artist, _ntfArtist);

            //add likeness points if title or artist is incomplete.
            //more points are given to the artist since it's more common to have
            //something like "pewdiepie" vs. "pewdiepie & boyinaband"
            if (_ntfTitle.Contains(_title)) { _titleDist -= 3; }
            if (_ntfArtist.Contains(_artist)) { _artistDist -= 4; }

            int _likeness = _titleDist + _artistDist + index;
            if (_likeness < 0) { _likeness = 0; }

            Log(Type.Info, $"SmartLyrics", $"Title - {_title} vs {_ntfTitle}\nArtist - {_artist} vs {_ntfArtist}\nLikeness - {_likeness}");
            return _likeness;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AppCenter.Analytics;
using Newtonsoft.Json.Linq;
using SmartLyrics.Common;
using SmartLyrics.Toolbox;
using static SmartLyrics.Globals;
using static SmartLyrics.Common.Logging;

namespace SmartLyrics.WebRequests
{
    public static class Genius
    {
        public static async Task<List<SongBundle>> GetSearchResults(string query)
        {
            string results = await HttpRequests.GetRequest(GeniusSearchUrl + Uri.EscapeUriString(query), GeniusAuthHeader);
            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]?["hits"]?.Children().ToList();

            List<SongBundle> resultsList = new List<SongBundle>();
            if (parsedList != null && parsedList.Count != 0)
            {
                foreach (JToken result in parsedList)
                {
                    Song song = new Song
                    {
                        Id = (int)result["result"]?["id"],
                        Title = (string)result["result"]?["title"],
                        Artist = (string)result["result"]?["primary_artist"]?["name"],
                        Cover = (string)result["result"]?["song_art_image_thumbnail_url"],
                        Header = (string)result["result"]?["header_image_url"],
                        ApiPath = (string)result["result"]?["api_path"],
                        Path = (string)result["result"]?["path"]
                    };
        
                    RomanizedSong rSong = new RomanizedSong();
                    if (Prefs.GetBoolean("romanize_search", false))
                    {
                        rSong = await JapaneseTools.RomanizeSong(song, false);
                    }

                    resultsList.Add(new SongBundle(song, rSong));
                }
                
                return resultsList;
            }

            resultsList = new List<SongBundle>();
            return resultsList;
        }

        public static async Task<string> GetSongLyrics(SongBundle song)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = await web.LoadFromWebAsync("https://genius.com" + song.Normal.Path);
            
            Log(Logging.Type.Processing, "Parsing HTML...");
            return await HtmlParsing.ParseHtml(doc);
        }
        
        public static async Task<SongBundle> GetSongDetails(SongBundle song)
        {
            Log(Logging.Type.Info, "Starting GetSongDetails operation");
            string results = await HttpRequests.GetRequest(GeniusApiUrl + song.Normal.ApiPath, GeniusAuthHeader);

            JObject parsed = JObject.Parse(results);
            parsed = (JObject)parsed["response"]?["song"]; //Change root to song

            Song fromJson = new Song
            {
                Title = (string) parsed?.SelectToken("title") ?? "",
                Artist = (string) parsed?.SelectToken("primary_artist.name") ?? "",
                Album = (string) parsed?.SelectToken("album.name") ?? "",
                Header = (string) parsed?.SelectToken("header_image_url") ?? "",
                Cover = (string) parsed?.SelectToken("song_art_image_url") ?? "",
                ApiPath = (string) parsed?.SelectToken("api_path") ?? "",
                Path = (string) parsed?.SelectToken("path") ?? ""
            };

            song.Normal = fromJson;

            if (parsed != null && parsed["featured_artists"].HasValues)
            {
                IList<JToken> parsedList = parsed["featured_artists"].Children().ToList();

                song.Normal.FeaturedArtist = "feat. ";
                foreach (JToken artist in parsedList)
                {
                    if (song.Normal.FeaturedArtist == "feat. ")
                    { song.Normal.FeaturedArtist += artist["name"]?.ToString(); }
                    else
                    { song.Normal.FeaturedArtist += ", " + artist["name"]; }
                }

                Log(Logging.Type.Processing, "Added featured artists to song");
            }
            else
            {
                song.Normal.FeaturedArtist = "";
            }

            //Execute all Japanese transliteration tasks at once
            if (Prefs.GetBoolean("auto_romanize_details", true) && song.Normal.Title.ContainsJapanese() || song.Normal.Artist.ContainsJapanese() || song.Normal.Album.ContainsJapanese())
            {
                Task<string> awaitTitle = song.Normal.Title.StripJapanese();
                Task<string> awaitArtist = song.Normal.Artist.StripJapanese();
                Task<string> awaitAlbum = song.Normal.Album.StripJapanese();

                await Task.WhenAll(awaitTitle, awaitArtist, awaitAlbum);

                RomanizedSong romanized = new RomanizedSong();
                // This snippet is the same in GetAndShowLyrics
                song.Romanized ??= romanized;

                romanized.Title = await awaitTitle;
                romanized.Artist = await awaitArtist;
                romanized.Album = await awaitAlbum;

                romanized.Id = song.Normal.Id;
                song.Romanized = romanized;
                song.Normal.Romanized = true;

                Log(Logging.Type.Event, "Romanized song info with ID " + song.Normal.Id);
                Analytics.TrackEvent("Romanized song info", new Dictionary<string, string> {
                    { "SongID", song.Normal.Id.ToString() }
                });
            }
            else 
            {
                song.Romanized = null;
            }

            return song;
        }
    }
}
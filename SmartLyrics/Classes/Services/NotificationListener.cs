using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Service.Notification;
using Android.Support.V4.App;
using Android.Util;

using Newtonsoft.Json.Linq;
using WanaKanaNet;
using SmartLyrics.Common;
using SmartLyrics.Toolbox;
using TaskStackBuilder = Android.Support.V4.App.TaskStackBuilder;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;

namespace SmartLyrics.Services
{
    [Service(Label = "SmartLyrics", Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE")]
    [IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
    class NLService : NotificationListenerService
    {
        static readonly int NOTIFICATION_ID = 1000;
        static readonly string CHANNEL_ID = "auto_lyrics_detect_sl";
        internal static readonly string COUNT_KEY = "count";

        ISharedPreferences prefs;

        Song previousSong = new Song() { title = "", artist = "" };
        Song detectedSong;

        //max string distance
        int maxLikeness = 6;


        #region Standard Activity Shit
        public async override void OnCreate()
        {
            base.OnCreate();
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnCreate (NLService): Service created");

            prefs = PreferenceManager.GetDefaultSharedPreferences(this);

            await CreateNotificationChannel();
        }

        public async override void OnListenerConnected()
        {
            base.OnListenerConnected();

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnListenerConnected (NLService): Listener connected");
        }
        #endregion


        //returns as index 0 the title of the notification and as index 1 the artist
        internal Song GetTitleAndArtistFromExtras(string extras)
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

            Song output = new Song() { title = _title, artist = _artist };

            return output;
        }

        internal static async Task<string> StripJapanese(string input)
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

        internal static bool ContainsJapanese(string text) => WanaKana.IsMixed(text) || WanaKana.IsJapanese(text);

        internal async Task<int> CalculateLikeness(Song result, Song notification, int index)
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
            
            string title = result.title.ToLowerInvariant();
            string artist = result.artist.ToLowerInvariant();

            string ntfTitle = notification.title.ToLowerInvariant();
            ntfTitle.Replace("🅴", ""); //remove "🅴" used by Apple Music for explicit songs
            string ntfArtist = notification.artist.ToLowerInvariant();

            title = await StripJapanese(title);
            artist = await StripJapanese(artist);

            int titleDist = Text.Distance(title, ntfTitle);
            int artistDist = Text.Distance(artist, ntfArtist);

            //add likeness points if title or artist is incomplete.
            //more points are given to the artist since it's more common to have
            //something like "pewdiepie" vs. "pewdiepie & boyinaband"
            if (ntfTitle.Contains(title)) { titleDist -= 3; }
            if (ntfArtist.Contains(artist)) { artistDist -= 4; }

            int likeness = titleDist + artistDist + index;
            if (likeness < 0) { likeness = 0; }

            Log.WriteLine(LogPriority.Verbose, $"SmartLyrics", $"file_name_here.cs: Title - {title} vs {ntfTitle}\nArtist - {artist} vs {ntfArtist}\nLikeness - {likeness}");
            return likeness;
        }

        //strips artist and title strings for remixes and collabs
        internal Song StripSongForSearch(Song input)
        {
            string strippedTitle = input.title;
            string strippedArtist = input.artist;

            //removes any Remix, Edit, or Featuring info encapsulated
            //in parenthesis or brackets
            if (input.title.ContainsAll("(", ")", "[", "]"))
            {
                MatchCollection inside = Regex.Matches(input.title, @"\(.*?\)");
                inside.Concat(Regex.Matches(input.title, @"\[.*?\]").ToArray());

                foreach (Match s in inside)
                {
                    if (s.Value.ToLowerInvariant().ContainsAny("feat.", "ft.", "featuring ", "edit", "mix"))
                        { strippedTitle.Replace(s.Value, ""); }
                }
            }

            if (input.artist.Contains(" & "))
            {
                strippedArtist = Regex.Replace(input.artist, @" & .*$", "");
            }

            strippedTitle.Trim();
            strippedArtist.Trim();

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "file_name_here.cs: Stripped title");
            Song output = new Song() { title = strippedTitle, artist = strippedArtist };
            return output;
        }

        public async override void OnNotificationPosted(StatusBarNotification sbn)
        {
            base.OnNotificationPosted(sbn);

            if (prefs.GetBoolean("detect_song", true))
            {
                if (sbn.Notification.Category == "transport")
                {
                    Song notificationSong = GetTitleAndArtistFromExtras(sbn.Notification.Extras.ToString());

                    if (previousSong.title != notificationSong.title && previousSong.artist != notificationSong.artist && !string.IsNullOrEmpty(notificationSong.title))
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnNotificationPosted (NLService): Previous song is different and not empty, getting search results...");
                        await GetAndCompareResults(notificationSong, sbn.PackageName);
                    }
                }
            }
        }


        private async Task GetAndCompareResults(Song ntfSong, string packageName)
        {
            //set previous song variable now so that it won't be called later
            previousSong = ntfSong;

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Starting async GetSearchResults operation");

            // strip song for things that interfere search. making a separate
            // Song object makes sure that one search is as broad as possible, so
            // a song with (Remix) on the title would still appear if we searched
            // without (Remix) tag
            Song stripped = StripSongForSearch(ntfSong);
            string results = await HTTPRequests.GetRequest(Globals.geniusSearchURL + stripped.artist + " - " + stripped.title, Globals.geniusAuthHeader); //search on genius

            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", $"getAndCompareResults (NLService): Parsed results into list with size {parsedList.Count}");

            List<Song> likenessRanking = new List<Song>();
            Song mostLikely = new Song();

            //calculate likeness and add to list, which will be sorted by ascending likeness
            if (parsedList.Count == 0)
            {
                mostLikely = new Song();
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "file_name_here.cs: No search results!");
            }
            else if (parsedList.Count == 1)
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "file_name_here.cs: Search returned 1 result");
                mostLikely = new Song()
                {
                    id = (int)parsedList[0]["result"]["id"],
                    title = (string)parsedList[0]["result"]["title"],
                    artist = (string)parsedList[0]["result"]["primary_artist"]["name"],
                    cover = (string)parsedList[0]["result"]["song_art_image_thumbnail_url"],
                    header = (string)parsedList[0]["result"]["header_image_url"],
                    APIPath = (string)parsedList[0]["result"]["api_path"],
                    path = (string)parsedList[0]["result"]["path"]
                };

                mostLikely.likeness = await CalculateLikeness(mostLikely, ntfSong, 0); //index is 0 since this is the only result
            }
            else
            {
                foreach (JToken result in parsedList)
                {
                    Song resultSong = new Song()
                    {
                        title = (string)result["result"]["title"],
                        artist = (string)result["result"]["primary_artist"]["name"]
                    };

                    Log.WriteLine(LogPriority.Info, "SmartLyrics", $"file_name_here.cs: Evaluating song {resultSong.title} by {resultSong.artist}");

                    int index = parsedList.IndexOf(result);
                    resultSong.likeness = await CalculateLikeness(resultSong, ntfSong, index);

                    likenessRanking.Add(resultSong);
                }

                likenessRanking = likenessRanking.OrderBy(o => o.likeness).ToList();
                mostLikely = likenessRanking.First();
            }

            if (mostLikely.likeness > maxLikeness)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", $"file_name_here.cs: Selected song {mostLikely.title} by {mostLikely.artist} with likeness {mostLikely.likeness} is too unlikely.\n Song not found.");
            }
            else if (string.IsNullOrEmpty(mostLikely.title))
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "file_name_here.cs: Song not found!");
            }
            else
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", $"file_name_here.cs: Selected song is {mostLikely.title} by {mostLikely.artist} with likeness {mostLikely.likeness}.");
            }
            //if (!songFound)
            //{
            //    Log.WriteLine(LogPriority.Warn, "SmartLyrics", "getAndCompareResults (NLService): Common.Song not found, trying to search again...");
            //    results = await APIRequests.Genius.GetSearchResults(title, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            //    parsed = JObject.Parse(results);
            //    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Results parsed into JObject");

            //    parsedList = parsed["response"]["hits"].Children().ToList();
            //    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Parsed results into list");

            //    foreach (JToken result in parsedList)
            //    {
            //        if (await Text.Distance((string)result["result"]["title"], artist + " - " + title) <= maxLikeness + 10)
            //        {
            //            MainActivity.notificationSong.title = (string)result["result"]["title"];
            //            MainActivity.notificationSong.artist = (string)result["result"]["primary_artist"]["name"];
            //            MainActivity.notificationSong.cover = (string)result["result"]["song_art_image_thumbnail_url"];
            //            MainActivity.notificationSong.header = (string)result["result"]["header_image_url"];
            //            MainActivity.notificationSong.APIPath = (string)result["result"]["api_path"];
            //            MainActivity.notificationSong.path = (string)result["result"]["path"];

            //            songFound = true;

            //            if (!MiscTools.IsInForeground())
            //            {
            //                CreateNotification(artist, title);
            //                MainActivity.checkOnStart = true;
            //                Log.WriteLine(LogPriority.Info, "SmartLyrics", "getAndCompareResults (NLService): Found song match, creating notification...");
            //            }
            //            else
            //            {
            //                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "getAndCompareResults (NLService): Application is in foreground");
            //            }

            //            break;
            //        }
            //    }
            //}
        }

        private async Task CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var name = Resources.GetString(Resource.String.channelName);
                var description = GetString(Resource.String.channelDescription);
                var channel = new NotificationChannel(CHANNEL_ID, name, NotificationImportance.Low)
                {
                    Description = description
                };

                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }
        }

        private void CreateNotification(string artist, string title)
        {
            MainActivity.fromNotification = true;

            TaskStackBuilder stackBuilder = TaskStackBuilder.Create(this);
            stackBuilder.AddParentStack(Java.Lang.Class.FromType(typeof(MainActivity)));
            stackBuilder.AddNextIntent(new Intent(this, typeof(MainActivity)));

            var resultIntent = stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetAutoCancel(true)
                .SetContentTitle("SmartLyrics")
                .SetContentText(artist + " - " + title)
                .SetSmallIcon(Resource.Drawable.ic_stat_name)
                .SetContentIntent(resultIntent)
                .SetPriority(-1);

            var notificationManager = NotificationManagerCompat.From(this);
            notificationManager.Notify(NOTIFICATION_ID, builder.Build());
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "createNotification (NLService): Notification made!");
        }
    }
}
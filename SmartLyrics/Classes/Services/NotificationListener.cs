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
using static SmartLyrics.Toolbox.MiscTools;
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

        Song previousSong;
        Song detectedSong;

        //max string distance
        int maxDistance = 4;


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

        internal static string StripJapanese()
        {

        }

        internal static string StripRomaji()
        {

        }

        internal static bool ContainsJapanese(string text) => WanaKana.IsMixed(text) || WanaKana.IsJapanese(text);

        internal async Task<int> CalculateLikenessJPN(Song result, Song notification, int index, string packageName = "")
        {
            string title = result.title;
            string artist = result.artist;

            string ntfTitle = notification.title;
            string ntfArtist = notification.artist;

            //if title is completely in Japanese
            if (WanaKana.IsJapanese(ntfTitle))
            {

            }
        }

        internal async Task<int> CalculateLikeness(Song result, Song notification, int index, string packageName = "")
        {
            int titleDist = Distance(result.title, notification.title);
            int artistDist = Distance(result.artist, notification.artist);

            return titleDist + artistDist + index;
        }

        public async override void OnNotificationPosted(StatusBarNotification sbn)
        {
            base.OnNotificationPosted(sbn);

            if (prefs.GetBoolean("detect_song", true))
            {
                if (sbn.Notification.Category == "transport")
                {
                    Song notificationSong = GetTitleAndArtistFromExtras(sbn.Notification.Extras.ToString());

                    if (previousSong.title != notificationSong.title && previousSong.artist != notificationSong.artist)
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnNotificationPosted (NLService): Previous song is different, getting search results...");
                        await GetAndCompareResults(notificationSong, sbn.PackageName);
                    }
                }
            }
        }

        private async Task GetAndCompareResults(Song ntfSong, string packageName)
        {
            bool songFound = false;

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Starting async GetSearchResults operation");
            string results = await APIRequests.Genius.GetSearchResults(ntfSong.artist + " - " + ntfSong.title, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Parsed results into list");

            List<Song> likenessRanking = new List<Song>();
            foreach (JToken result in parsedList)
            {
                Song resultSong = new Song() { title = (string)result["result"]["title"], artist = (string)result["result"]["primary_artist"]["name"] };
                int index = parsedList.IndexOf(result);

                if (ContainsJapanese(ntfSong.title))
                {
                    resultSong.likeness = await CalculateLikenessJPN(resultSong, ntfSong, index);
                }
                else
                {
                    resultSong.likeness = await CalculateLikeness(resultSong, ntfSong, index);
                }

                likenessRanking.Add(resultSong);

                if (true)
                {
                    detectedSong.title = (string)result["result"]["title"];
                    detectedSong.artist = (string)result["result"]["primary_artist"]["name"];
                    detectedSong.cover = (string)result["result"]["song_art_image_thumbnail_url"];
                    detectedSong.header = (string)result["result"]["header_image_url"];
                    detectedSong.APIPath = (string)result["result"]["api_path"];
                    detectedSong.path = (string)result["result"]["path"];

                    songFound = true;

                    if (!IsInForeground())
                    {
                        CreateNotification(artist, title);
                        MainActivity.checkOnStart = true;
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "getAndCompareResults (NLService): Found song match, creating notification...");
                    }
                    else
                    {
                        Log.WriteLine(LogPriority.Warn, "SmartLyrics", "getAndCompareResults (NLService): Application is in foreground");
                    }

                    break;
                }
            }

            if (!songFound)
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "getAndCompareResults (NLService): Common.Song not found, trying to search again...");
                results = await APIRequests.Genius.GetSearchResults(title, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
                parsed = JObject.Parse(results);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Results parsed into JObject");

                parsedList = parsed["response"]["hits"].Children().ToList();
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Parsed results into list");

                foreach (JToken result in parsedList)
                {
                    if (await Distance((string)result["result"]["title"], artist + " - " + title) <= maxDistance + 10)
                    {
                        MainActivity.notificationSong.title = (string)result["result"]["title"];
                        MainActivity.notificationSong.artist = (string)result["result"]["primary_artist"]["name"];
                        MainActivity.notificationSong.cover = (string)result["result"]["song_art_image_thumbnail_url"];
                        MainActivity.notificationSong.header = (string)result["result"]["header_image_url"];
                        MainActivity.notificationSong.APIPath = (string)result["result"]["api_path"];
                        MainActivity.notificationSong.path = (string)result["result"]["path"];

                        songFound = true;

                        if (!IsInForeground())
                        {
                            CreateNotification(artist, title);
                            MainActivity.checkOnStart = true;
                            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getAndCompareResults (NLService): Found song match, creating notification...");
                        }
                        else
                        {
                            Log.WriteLine(LogPriority.Warn, "SmartLyrics", "getAndCompareResults (NLService): Application is in foreground");
                        }

                        break;
                    }
                }
            }
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
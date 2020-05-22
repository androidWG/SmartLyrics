using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Util;
using Android.Service.Notification;
using Android.OS;
using Android.Support.V4.App;
using TaskStackBuilder = Android.Support.V4.App.TaskStackBuilder;

using Newtonsoft.Json.Linq;
using static SmartLyrics.Toolbox.MiscTools;
using Android.Preferences;

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

        int maxDistance = 4;
        string previousSong;

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

        public async override void OnNotificationPosted(StatusBarNotification sbn)
        {
            base.OnNotificationPosted(sbn);

            if (prefs.GetBoolean("detect_song", true))
            {
                if (sbn.Notification.Category == "transport")
                {
                    string extras = sbn.Notification.Extras.ToString();

                    string title = Regex.Match(extras, @"(?<=android\.title=)(.*?)(?=, android\.)").ToString();
                    if (title.Contains("Remix") || title.Contains("remix") || title.Contains("Mix"))
                    {
                        title = Regex.Replace(title, @"\(feat\..*?\)", "");
                    }
                    else
                    {
                        title = Regex.Replace(title, @"\(.*?\)", "");
                        title.Trim();
                    }

                    if (previousSong != title + sbn.PackageName)
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnNotificationPosted (NLService): Previous song is different, getting search results...");
                        await GetAndCompareResults(extras, sbn.PackageName);
                    }
                }
            }
        }

        private async Task GetAndCompareResults(string extras, string packageName)
        {
            string title = Regex.Match(extras, @"(?<=android\.title=)(.*?)(?=, android\.)").ToString();
            if (title.Contains("Remix") || title.Contains("remix") || title.Contains("Mix"))
            {
                title = Regex.Replace(title, @"\(feat\..*?\)", "");
            }
            else
            {
                title = Regex.Replace(title, @"\(.*?\)", "");
                title.Trim();
            }

            string artist = Regex.Match(extras, @"(?<=android\.text=)(.*?)(?=, android\.)").ToString();

            bool songFound = false;
            previousSong = title + packageName;

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Starting async GetSearchResults operation");
            string results = await APIRequests.Genius.GetSearchResults(artist + " - " + title, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Results parsed into JObject");

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Parsed results into list");

            foreach (JToken result in parsedList)
            {
                string resultTitle = (string)result["result"]["title"];
                string resultArtist = (string)result["result"]["primary_artist"]["name"];

                if (await Distance(resultTitle, title) <= maxDistance && await Distance(resultArtist, artist) <= maxDistance || resultTitle.Contains(title) && resultArtist.Contains(artist))
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
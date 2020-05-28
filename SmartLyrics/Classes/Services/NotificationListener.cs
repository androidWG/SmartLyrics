using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Android.Support.V4.App;
using Android.Util;
using TaskStackBuilder = Android.Support.V4.App.TaskStackBuilder;

using Newtonsoft.Json.Linq;
using SmartLyrics.Common;
using SmartLyrics.Toolbox;
using static SmartLyrics.Globals;
using static SmartLyrics.Toolbox.SongParsing;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartLyrics.Services
{
    [Service(Label = "SmartLyrics", Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE")]
    [IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
    internal class NLService : NotificationListenerService
    {
        internal static readonly int NOTIFICATION_ID = 1000;
        internal static readonly string CHANNEL_ID = "auto_lyrics_detect_sl";
        internal static readonly string COUNT_KEY = "count";
        private ISharedPreferences prefs;
        private Song previousSong = new Song() { Title = "", Artist = "" };

        //max string distance
        private readonly int maxLikeness = 12;


        #region Standard Activity Shit
        public async override void OnCreate()
        {
            base.OnCreate();
            Log.WriteLine(LogPriority.Info, "NLService", "OnCreate (NLService): Service created");

            prefs = AndroidX.Preference.PreferenceManager.GetDefaultSharedPreferences(this);

            await CreateNotificationChannel();
        }
        #endregion


        #region Notification Handling
        public async override void OnListenerConnected()
        {
            base.OnListenerConnected();
            Log.WriteLine(LogPriority.Info, "NLService", "OnListenerConnected (NLService): Listener connected");

            StatusBarNotification[] notifications = GetActiveNotifications();
            foreach (StatusBarNotification n in notifications)
            {
                if (n.Notification.Category == "transport")
                {
                    Song notificationSong = GetTitleAndArtistFromExtras(n.Notification.Extras.ToString());
                    if (!string.IsNullOrEmpty(notificationSong.Title))
                    {
                        Log.WriteLine(LogPriority.Info, "NLService", "OnListenerConnected: Found song, starting search...");
                        await GetAndCompareResults(notificationSong);
                    }
                }
            }
        }

        public async override void OnNotificationPosted(StatusBarNotification sbn)
        {
            base.OnNotificationPosted(sbn);

            if (prefs.GetBoolean("detect_song", true))
            {
                if (sbn.Notification.Category == "transport")
                {
                    Log.WriteLine(LogPriority.Info, "NLService", "OnNotificationPosted: Recieved OnNotificationPosted");
                    Song notificationSong = GetTitleAndArtistFromExtras(sbn.Notification.Extras.ToString());

                    if (previousSong.Title != notificationSong.Title && !string.IsNullOrEmpty(notificationSong.Title))
                    {
                        Log.WriteLine(LogPriority.Info, "NLService", "OnNotificationPosted: Previous song is different and not empty, getting search results...");
                        await GetAndCompareResults(notificationSong);
                    }
                }
            }
        }
        #endregion


        private async Task GetAndCompareResults(Song ntfSong)
        {
            //set previous song variable now so that it won't be called again in a short period of time
            previousSong = ntfSong;

            Log.WriteLine(LogPriority.Verbose, "NLService", "GetAndCompareResults (NLService): Starting async GetSearchResults operation");

            // strip song for things that interfere search. making a separate
            // Song object makes sure that one search is as broad as possible, so
            // a song with (Remix) on the title would still appear if we searched
            // without (Remix) tag
            Song stripped = StripSongForSearch(ntfSong);
            string results = await HTTPRequests.GetRequest(geniusSearchURL + stripped.Artist + " - " + stripped.Title, geniusAuthHeader); //search on genius

            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log.WriteLine(LogPriority.Verbose, "NLService", $"getAndCompareResults (NLService): Parsed results into list with size {parsedList.Count}");

            List<Song> likenessRanking = new List<Song>();
            Song mostLikely = new Song();

            //calculate likeness and add to list, which will be sorted by ascending likeness
            if (parsedList.Count == 0)
            {
                mostLikely = new Song();
                Log.WriteLine(LogPriority.Warn, "NLService", "GetAndCompareResults: No search results!");
            }
            else if (parsedList.Count == 1)
            {
                Log.WriteLine(LogPriority.Warn, "NLService", "GetAndCompareResults: Search returned 1 result");
                mostLikely = new Song()
                {
                    Id = (int)parsedList[0]["result"]["id"],
                    Title = (string)parsedList[0]["result"]["title"],
                    Artist = (string)parsedList[0]["result"]["primary_artist"]["name"],
                    Cover = (string)parsedList[0]["result"]["song_art_image_thumbnail_url"],
                    Header = (string)parsedList[0]["result"]["header_image_url"],
                    APIPath = (string)parsedList[0]["result"]["api_path"],
                    Path = (string)parsedList[0]["result"]["path"]
                };

                mostLikely.Likeness = await CalculateLikeness(mostLikely, ntfSong, 0); //index is 0 since this is the only result
            }
            else
            {
                foreach (JToken result in parsedList)
                {
                    Song resultSong = new Song()
                    {
                        Id = (int)result["result"]["id"],
                        Title = (string)result["result"]["title"],
                        Artist = (string)result["result"]["primary_artist"]["name"],
                        Cover = (string)result["result"]["song_art_image_thumbnail_url"],
                        Header = (string)result["result"]["header_image_url"],
                        APIPath = (string)result["result"]["api_path"],
                        Path = (string)result["result"]["path"]
                    };

                    Log.WriteLine(LogPriority.Info, "NLService", $"GetAndCompareResults: Evaluating song {resultSong.Title} by {resultSong.Artist}");

                    int index = parsedList.IndexOf(result);
                    resultSong.Likeness = await CalculateLikeness(resultSong, ntfSong, index);

                    likenessRanking.Add(resultSong);
                }

                likenessRanking = likenessRanking.OrderBy(o => o.Likeness).ToList();
                mostLikely = likenessRanking.First();
            }

            //separated this to keep this method shorter
            HandleChosenSong(mostLikely);
        }

        private void HandleChosenSong(Song chosen)
        {
            if (chosen.Likeness >= maxLikeness)
            {
                Log.WriteLine(LogPriority.Error, "NLService", $"HandleChosenSong: Selected song {chosen.Title} by {chosen.Artist} with likeness {chosen.Likeness} is too unlikely.\n Song not found.");
            }
            else if (string.IsNullOrEmpty(chosen.Title))
            {
                Log.WriteLine(LogPriority.Error, "NLService", "HandleChosenSong: Song not found!");
            }
            else if (chosen.Likeness <= maxLikeness)
            {
                Log.WriteLine(LogPriority.Warn, "NLService", $"HandleChosenSong: Selected song is {chosen.Title} by {chosen.Artist} with likeness {chosen.Likeness}.");

                MainActivity.notificationSong = chosen;
                MainActivity.fromNotification = true;

                if (!MiscTools.IsInForeground())
                {
                    CreateNotification(chosen.Title, chosen.Artist);
                }
            }
        }

        private async Task CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                string name = Resources.GetString(Resource.String.channelName);
                string description = GetString(Resource.String.channelDescription);
                NotificationChannel channel = new NotificationChannel(CHANNEL_ID, name, NotificationImportance.Low)
                {
                    Description = description
                };

                NotificationManager notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }
        }

        private void CreateNotification(string title, string artist)
        {
            Log.WriteLine(LogPriority.Verbose, "NLService", "CreateNotification: Creating notification");
            MainActivity.fromNotification = true;

            TaskStackBuilder stackBuilder = TaskStackBuilder.Create(this);
            stackBuilder.AddParentStack(Java.Lang.Class.FromType(typeof(MainActivity)));
            stackBuilder.AddNextIntent(new Intent(this, typeof(MainActivity)));

            PendingIntent resultIntent = stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetAutoCancel(true)
                .SetContentTitle("SmartLyrics")
                .SetContentText(artist + " - " + title)
                .SetSmallIcon(Resource.Drawable.ic_stat_name)
                .SetContentIntent(resultIntent)
                .SetPriority(-1);

            NotificationManagerCompat notificationManager = NotificationManagerCompat.From(this);
            notificationManager.Notify(NOTIFICATION_ID, builder.Build());
            Log.WriteLine(LogPriority.Info, "NLService", "CreateNotification (NLService): Notification made!");
        }
    }
}
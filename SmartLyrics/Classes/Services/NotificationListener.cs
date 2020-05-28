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
    class NLService : NotificationListenerService
    {
        internal static readonly int NOTIFICATION_ID = 1000;
        internal static readonly string CHANNEL_ID = "auto_lyrics_detect_sl";
        internal static readonly string COUNT_KEY = "count";

        ISharedPreferences prefs;
        private Song previousSong = new Song() { title = "", artist = "" };

        //max string distance
        private readonly int maxLikeness = 12;


        #region Standard Activity Shit
        public async override void OnCreate()
        {
            base.OnCreate();
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnCreate (NLService): Service created");

            prefs = AndroidX.Preference.PreferenceManager.GetDefaultSharedPreferences(this);

            await CreateNotificationChannel();
        }
        #endregion


        #region Notification Handling
        public async override void OnListenerConnected()
        {
            base.OnListenerConnected();
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnListenerConnected (NLService): Listener connected");

            StatusBarNotification[] notifications = GetActiveNotifications();
            foreach (StatusBarNotification n in notifications)
            {
                if (n.Notification.Category == "transport")
                {
                    Song notificationSong = GetTitleAndArtistFromExtras(n.Notification.Extras.ToString());
                    if (!string.IsNullOrEmpty(notificationSong.title))
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "file_name_here.cs: Found song, starting search...");
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
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "file_name_here.cs: Recieved OnNotificationPosted");
                    Song notificationSong = GetTitleAndArtistFromExtras(sbn.Notification.Extras.ToString());

                    if (previousSong.title != notificationSong.title && !string.IsNullOrEmpty(notificationSong.title))
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnNotificationPosted (NLService): Previous song is different and not empty, getting search results...");
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

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndCompareResults (NLService): Starting async GetSearchResults operation");

            // strip song for things that interfere search. making a separate
            // Song object makes sure that one search is as broad as possible, so
            // a song with (Remix) on the title would still appear if we searched
            // without (Remix) tag
            Song stripped = StripSongForSearch(ntfSong);
            string results = await HTTPRequests.GetRequest(geniusSearchURL + stripped.artist + " - " + stripped.title, geniusAuthHeader); //search on genius

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
                        id = (int)result["result"]["id"],
                        title = (string)result["result"]["title"],
                        artist = (string)result["result"]["primary_artist"]["name"],
                        cover = (string)result["result"]["song_art_image_thumbnail_url"],
                        header = (string)result["result"]["header_image_url"],
                        APIPath = (string)result["result"]["api_path"],
                        path = (string)result["result"]["path"]
                    };

                    Log.WriteLine(LogPriority.Info, "SmartLyrics", $"file_name_here.cs: Evaluating song {resultSong.title} by {resultSong.artist}");

                    int index = parsedList.IndexOf(result);
                    resultSong.likeness = await CalculateLikeness(resultSong, ntfSong, index);

                    likenessRanking.Add(resultSong);
                }

                likenessRanking = likenessRanking.OrderBy(o => o.likeness).ToList();
                mostLikely = likenessRanking.First();
            }

            //separated this to keep this method shorter
            HandleChosenSong(mostLikely);
        }

        private void HandleChosenSong(Song chosen)
        {
            if (chosen.likeness >= maxLikeness)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", $"file_name_here.cs: Selected song {chosen.title} by {chosen.artist} with likeness {chosen.likeness} is too unlikely.\n Song not found.");
            }
            else if (string.IsNullOrEmpty(chosen.title))
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "file_name_here.cs: Song not found!");
            }
            else if (chosen.likeness <= maxLikeness)
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", $"file_name_here.cs: Selected song is {chosen.title} by {chosen.artist} with likeness {chosen.likeness}.");

                MainActivity.notificationSong = chosen;
                MainActivity.fromNotification = true;

                if (!MiscTools.IsInForeground())
                {
                    CreateNotification(chosen.title, chosen.artist);
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
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "file_name_here.cs: Creating notification");
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
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "CreateNotification (NLService): Notification made!");
        }
    }
}
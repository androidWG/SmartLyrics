using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Android.Support.V4.App;
using Newtonsoft.Json.Linq;
using SmartLyrics.Common;
using SmartLyrics.Toolbox;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SmartLyrics.Globals;
using static SmartLyrics.Toolbox.SongParsing;
using static SmartLyrics.Common.Logging;
using TaskStackBuilder = Android.Support.V4.App.TaskStackBuilder;
using Newtonsoft.Json;

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

        NotificationManagerCompat ntfManager;

        //max string distance
        private readonly int maxLikeness = 12;


        #region Standard Activity Shit
        public override async void OnCreate()
        {
            base.OnCreate();
            Log(Type.Event, "Service created");

            prefs = AndroidX.Preference.PreferenceManager.GetDefaultSharedPreferences(this);

            await CreateNotificationChannel();
        }
        #endregion


        #region Notification Handling
        public override async void OnListenerConnected()
        {
            base.OnListenerConnected();
            Log(Type.Event, "Listener connected");

            StatusBarNotification[] notifications = GetActiveNotifications();
            foreach (StatusBarNotification n in notifications)
            {
                if (n.Notification.Category == "transport")
                {
                    Song notificationSong = GetTitleAndArtistFromExtras(n.Notification.Extras.ToString());
                    if (!string.IsNullOrEmpty(notificationSong.Title))
                    {
                        Log(Type.Processing, "Found song, starting search...");
                        await GetAndCompareResults(notificationSong);
                    }
                }
            }
        }

        public override async void OnNotificationPosted(StatusBarNotification sbn)
        {
            base.OnNotificationPosted(sbn);

            if (prefs.GetBoolean("detect_song", true))
            {
                if (sbn.Notification.Category == "transport")
                {
                    Song notificationSong = GetTitleAndArtistFromExtras(sbn.Notification.Extras.ToString());

                    if (previousSong.Title != notificationSong.Title && !string.IsNullOrEmpty(notificationSong.Title))
                    {
                        Log(Type.Event, "Previous song is different and not empty, getting search results...");
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

            Log(Type.Info, "Starting async GetSearchResults operation");

            // strip song for things that interfere search. making a separate
            // Song object makes sure that one search is as broad as possible, so
            // a song with (Remix) on the title would still appear if we searched
            // without (Remix) tag
            Song stripped = StripSongForSearch(ntfSong);
            string results = await HTTPRequests.GetRequest(geniusSearchURL + stripped.Artist + " - " + stripped.Title, geniusAuthHeader); //search on genius

            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log(Type.Info, $"Parsed results into list with size {parsedList.Count}");

            List<Song> likenessRanking = new List<Song>();
            Song mostLikely = new Song();

            //calculate likeness and add to list, which will be sorted by ascending likeness
            if (parsedList.Count == 0)
            {
                mostLikely = new Song();
                Log(Type.Info, "No search results!");
            }
            else if (parsedList.Count == 1)
            {
                Log(Type.Info, "Search returned 1 result");
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

                    Log(Type.Processing, $"Evaluating song {resultSong.Title} by {resultSong.Artist}");

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

        private async void HandleChosenSong(Song chosen)
        {
            if (chosen.Likeness >= maxLikeness)
            {
                Log(Type.Info, $"Selected song {chosen.Title} by {chosen.Artist} with likeness {chosen.Likeness} is too unlikely.\n Song not found.");
                ntfManager.Cancel(NOTIFICATION_ID);
            }
            else if (string.IsNullOrEmpty(chosen.Title))
            {
                Log(Type.Info, "Song not found!");
                ntfManager.Cancel(NOTIFICATION_ID);
            }
            else if (chosen.Likeness <= maxLikeness)
            {
                Log(Type.Event, $"Selected song is {chosen.Title} by {chosen.Artist} with likeness {chosen.Likeness}.");

                if (!MiscTools.IsInForeground())
                {
                    CreateNotification(new SongBundle(chosen, new RomanizedSong()));
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

        private void CreateNotification(SongBundle song)
        {
            Log(Type.Info, "Creating notification");

            Intent info = new Intent(Application.Context, typeof(MainActivity));
            info.PutExtra("NotificationSong", JsonConvert.SerializeObject(song));

            TaskStackBuilder stackBuilder = TaskStackBuilder.Create(Application.Context);
            stackBuilder.AddParentStack(Java.Lang.Class.FromType(typeof(MainActivity)));
            stackBuilder.AddNextIntent(info);

            PendingIntent resultIntent = stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetAutoCancel(true)
                .SetContentTitle(Resources.GetString(Resource.String.app_name))
                .SetContentText(song.Normal.Artist + " - " + song.Normal.Title)
                .SetSmallIcon(Resource.Drawable.ic_stat_name)
                .SetContentIntent(resultIntent)
                .SetPriority(-1);

            ntfManager = NotificationManagerCompat.From(Application.Context);
            ntfManager.Notify(NOTIFICATION_ID, builder.Build());
            Log(Type.Event, "Notification made!");
        }
    }
}
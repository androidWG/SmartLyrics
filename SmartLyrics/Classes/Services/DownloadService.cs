using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
using Android.Util;

using FFImageLoading;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using SmartLyrics.Toolbox;
using static SmartLyrics.Globals;
using static SmartLyrics.Toolbox.MiscTools;

namespace SmartLyrics.Services
{
    [Service(Name = "com.SamuelR.SmartLyrics.DownloadService")]
    public class DownloadService : Service
    {
        List<Common.Song> savedTracks = new List<Common.Song>();
        int maxDistance = 4;
        float current = 0;
        int completedTasks = 0;
        int callsMade = 0;
        public static int progress = 0;
        bool isWorking = false;

        static readonly int NOTIFICATION_ID = 177013;
        static readonly string CHANNEL_ID = "download_lyrics_bg_sl";

        string savedLyricsLocation = "SmartLyrics/Saved Lyrics/Spotify/";
        string savedImagesLocation = "SmartLyrics/Saved Lyrics/Spotify/Image Cache/";
        string path = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath, "SmartLyrics/Saved Lyrics/Spotify/");
        string pathImg = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath, "SmartLyrics/Saved Lyrics/Spotify/Image Cache/");
        string savedSeparator = @"!@=-@!";

        public IBinder Binder { get; private set; }

        public override IBinder OnBind(Intent intent)
        {
            Binder = new ProgressBinder(this);
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnBind (DownloadService): Service bound!");

            createNotificationChannel();
            startWorking(intent);

            return Binder;
        }

        private async Task startWorking(Intent intent)
        {
            isWorking = true;
            updateProgress();

            await getSavedList(intent.GetStringExtra("AccessToken"));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await getGeniusSearchResults();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await getLyricsAndDetails();
        }

        private async Task updateProgress()
        {
            NotificationManagerCompat nm = NotificationManagerCompat.From(this);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle(GetString(Resource.String.notificationTitle))
                .SetContentText(GetString(Resource.String.notificationDesc))
                .SetSmallIcon(Resource.Drawable.ic_stat_name)
                .SetProgress(100, 0, false)
                .SetOngoing(true)
                .SetPriority(-1);

            nm.Notify(NOTIFICATION_ID, builder.Build());

            while (isWorking)
            {
                if (progress == 100)
                {
                    builder.SetProgress(100, 100, false);
                    nm.CancelAll();

                    Log.WriteLine(LogPriority.Warn, "SmartLyrics", "updateProgress (DownloadService): Finished work, stopping service...");
                    isWorking = false;
                    StopSelf();
                }

                builder.SetProgress(100, progress, false);

                nm.Notify(NOTIFICATION_ID, builder.Build());

                await Task.Delay(1000);
            }
        }

        private async Task createNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                string name = Resources.GetString(Resource.String.notificationChannelName);
                string description = GetString(Resource.String.notificationChannelDesc);
                NotificationChannel channel = new NotificationChannel(CHANNEL_ID, name, NotificationImportance.Low)
                {
                    Description = description
                };

                NotificationManager notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }
        }

        private async Task getSavedList(string accessToken)
        {
            string nextURL = "https://api.spotify.com/v1/me/tracks?limit=50";

            while (nextURL != "")
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getSavedList (DownloadService): nextURL = " + nextURL);
                string results = await APIRequests.Spotify.GetSavedSongs("Bearer " + accessToken, nextURL);
                JObject parsed = JObject.Parse(results);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getSavedList (DownloadService): Results parsed into JObject");

                float offset = Convert.ToSingle((string)parsed["offset"]);
                float total = Convert.ToSingle((string)parsed["total"]);
                progress = (int)ConvertRange(0, 100, 0, 25, (offset / total) * 100);

                IList<JToken> parsedList = parsed["items"].Children().ToList();
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getSavedList (DownloadService): Parsed results into list");

                foreach (JToken result in parsedList)
                {
                    Common.Song song = new Common.Song()
                    {
                        title = Regex.Replace((string)result["track"]["name"], @"\(feat\. .+", ""),
                        artist = (string)result["track"]["artists"][0]["name"]
                    };

                    savedTracks.Add(song);
                }

                nextURL = parsed["next"].ToString();
            }
        }

        private async Task getGeniusSearchResults()
        {
            List<Common.Song> geniusTemp = new List<Common.Song>();

            float total = savedTracks.Count;

            foreach (Common.Song s in savedTracks)
            {
                if (callsMade == 50)
                {
                    while (completedTasks < 50)
                    {
                        Log.WriteLine(LogPriority.Error, "SmartLyrics", "getGeniusSearchResults (DownloadService): geniusSearch tasks still running");
                        await Task.Delay(1000);
                    }

                    completedTasks = 0;
                    callsMade = 0;
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "getGeniusSearchResults (DownloadService): No tasks are running!");
                    geniusSearch(s, geniusTemp);
                }
                else
                {
                    geniusSearch(s, geniusTemp);
                }

                current++;
                progress = (int)ConvertRange(0, 100, 0, 25, (current / total) * 100) + 25;

                Log.WriteLine(LogPriority.Info, "SmartLyrics", "getGeniusSearchResults (DownloadService): foreach for index " + current + " completed.");
            }

            if (total % 25 != 0)
            {
                while (completedTasks < (total % 25))
                {
                    Log.WriteLine(LogPriority.Error, "SmartLyrics", "getGeniusSearchResults (DownloadService): geniusSearch tasks still running, can't finish task!");
                    await Task.Delay(1000);
                }
            }

            savedTracks = geniusTemp;
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getGeniusSearchResults (DownloadService): Changed savedTracks to Genius results");
        }

        private async Task geniusSearch(Common.Song s, List<Common.Song> geniusTemp)
        {
            callsMade++;

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "geniusSearch (DownloadService): Starting geniusSearch");
            string results = await HTTPRequests.GetRequest(geniusSearchURL + s.artist + " - " + s.title, geniusAuthHeader);
            if (results == null)
            {
                results = await HTTPRequests.GetRequest(geniusSearchURL + s.artist + " - " + s.title, geniusAuthHeader);
            }
            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            foreach (JToken result in parsedList)
            {
                string resultTitle = (string)result["result"]["title"];
                string resultArtist = (string)result["result"]["primary_artist"]["name"];

                if ((Text.Distance(resultTitle, s.title) <= maxDistance && Text.Distance(resultArtist, s.artist) <= maxDistance) || resultTitle.Contains(s.title) && resultArtist.Contains(s.artist))
                {
                    string path = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath, savedLyricsLocation, (string)result["result"]["primary_artist"]["name"] + savedSeparator + (string)result["result"]["title"] + ".txt");

                    if (!File.Exists(path))
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "geniusSearch (DownloadService): Common.Song found! Adding to geniusTemp");

                        Common.Song song = new Common.Song()
                        {
                            title = (string)result["result"]["title"],
                            artist = (string)result["result"]["primary_artist"]["name"],
                            cover = (string)result["result"]["song_art_image_thumbnail_url"],
                            header = (string)result["result"]["header_image_url"],
                            APIPath = (string)result["result"]["api_path"],
                            path = (string)result["result"]["path"]
                        };

                        geniusTemp.Add(song);

                        break;
                    }
                    else
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "geniusSearch (DownloadService): Common.Song found but already downloaded");
                        break;
                    }
                }
            }

            completedTasks++;
            Log.WriteLine(LogPriority.Warn, "SmartLyrics", "geniusSearch (DownloadService): completedTasks = " + completedTasks);
        }

        private async Task getLyricsAndDetails()
        {
            callsMade = 0;
            completedTasks = 0;
            current = 0;

            List<Common.Song> geniusTemp = new List<Common.Song>();

            float total = savedTracks.Count;

            if (savedTracks.Count != 0)
            {
                foreach (Common.Song s in savedTracks)
                {
                    if (callsMade == 10)
                    {
                        while (completedTasks < 10)
                        {
                            Log.WriteLine(LogPriority.Error, "SmartLyrics", "getLyricsAndDetails (DownloadService): getDetails tasks still running");
                            await Task.Delay(5000);
                        }

                        completedTasks = 0;
                        callsMade = 0;
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "getLyricsAndDetails (DownloadService): No tasks are running!");
                        getDetails(s.APIPath);
                    }
                    else
                    {
                        getDetails(s.APIPath);
                    }

                    current++;
                    progress = (int)ConvertRange(0, 100, 0, 50, (current / total) * 100) + 50;

                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "getLyricsAndDetails (DownloadService): foreach for index " + current + " completed.");
                }
            }
            else
            {
                progress = 100;
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "getLyricsAndDetails (DownloadService): savedTracks is empty!");
            }

            progress = 100;
        }

        private async Task getDetails(string APIPath)
        {
            callsMade++;

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Starting getDetails operation");
            string results = await HTTPRequests.GetRequest(geniusAPIURL + APIPath, geniusAuthHeader);
            if (results == null)
            {
                Log.WriteLine(LogPriority.Debug, "SmartLyrics", "getDetails (DownloadService): Returned null, calling API again...");
                results = await HTTPRequests.GetRequest(geniusAPIURL + APIPath, geniusAuthHeader);
            }
            JObject parsed = JObject.Parse(results);

            Common.Song song = new Common.Song()
            {
                title = (string)parsed["response"]["song"]["title"],
                artist = (string)parsed["response"]["song"]["primary_artist"]["name"],
                album = (string)parsed.SelectToken("response.song.album.name"),
                header = (string)parsed["response"]["song"]["header_image_url"],
                cover = (string)parsed["response"]["song"]["song_art_image_url"],
                APIPath = (string)parsed["response"]["song"]["api_path"],
                path = (string)parsed["response"]["song"]["path"]
            };

            Log.WriteLine(LogPriority.Debug, "SmartLyrics", "getDetails (DownloadService): Created new Common.Song variable");

            if (parsed["response"]["song"]["featured_artists"].HasValues)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Track has featured artists");
                IList<JToken> parsedList = parsed["response"]["song"]["featured_artists"].Children().ToList();
                song.featuredArtist = "feat. ";
                foreach (JToken artist in parsedList)
                {
                    if (song.featuredArtist == "feat. ")
                    {
                        song.featuredArtist += artist["name"].ToString();
                    }
                    else
                    {
                        song.featuredArtist += ", " + artist["name"].ToString();
                    }
                }
            }
            else
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Track does not have featured artists");
                song.featuredArtist = "";
            }

            string downloadedLyrics;

            HtmlWeb web = new HtmlWeb();
            Log.WriteLine(LogPriority.Debug, "SmartLyrics", "getDetails (DownloadService): Trying to load page");
            HtmlDocument doc = await web.LoadFromWebAsync("https://genius.com" + song.path);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getDetails (DownloadService): Loaded Genius page");
            HtmlNode lyricsBody = doc.DocumentNode.SelectSingleNode("//div[@class='lyrics']");

            downloadedLyrics = Regex.Replace(lyricsBody.InnerText, @"^\s*", "");
            downloadedLyrics = Regex.Replace(downloadedLyrics, @"[\s]+$", "");
            song.lyrics = downloadedLyrics;

            await saveSongLyrics(song);
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Finished saving!");

            completedTasks++;
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Completed getDetails task for " + song.APIPath);
        }

        private async Task saveSongLyrics(Common.Song song)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "saveSongLyrics (DownloadService): Started saveSongLyrics operation");

            path = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath, savedLyricsLocation);
            pathImg = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath, savedImagesLocation);

            try
            {
                path = Path.Combine(path, song.artist + savedSeparator + song.title + ".txt");
                string pathHeader = Path.Combine(pathImg, song.artist + savedSeparator + song.title + "-header.jpg");
                string pathCover = Path.Combine(pathImg, song.artist + savedSeparator + song.title + "-cover.jpg");

                if (!File.Exists(path))
                {
                    using (StreamWriter sw = File.CreateText(path))
                    {
                        Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "saveSongLyrics (DownloadService): File doesn't exist, creating" + path.ToString());
                        await sw.WriteAsync(song.lyrics);
                        await sw.WriteLineAsync("\n");
                        await sw.WriteLineAsync(@"!!@@/\/\-----00-----/\/\@@!!");
                        await sw.WriteLineAsync(song.title);
                        await sw.WriteLineAsync(song.artist);
                        await sw.WriteLineAsync(song.album);
                        await sw.WriteLineAsync(song.featuredArtist);
                        await sw.WriteLineAsync(song.header);
                        await sw.WriteLineAsync(song.cover);
                        await sw.WriteLineAsync(song.APIPath);
                        await sw.WriteLineAsync(song.path);
                    }

                    using (FileStream fileStream = File.Create(pathHeader))
                    {
                        Stream header = await ImageService.Instance.LoadUrl(song.header).AsJPGStreamAsync();
                        header.Seek(0, SeekOrigin.Begin);
                        header.CopyTo(fileStream);
                    }

                    using (FileStream fileStream = File.Create(pathCover))
                    {
                        Stream cover = await ImageService.Instance.LoadUrl(song.cover).AsJPGStreamAsync();
                        cover.Seek(0, SeekOrigin.Begin);
                        cover.CopyTo(fileStream);
                    }
                }
                else
                {
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "saveSongLyrics (DownloadService): File already exists, let's do nothing!");
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "saveSongLyrics (DownloadService): Exception caught while saving song in DownloadService!\n" + e.ToString());
            }

        }

        public int GetProgress() { return progress; }
    }
}
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
using Android.Util;
using FFImageLoading;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using SmartLyrics.Common;
using SmartLyrics.Toolbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SmartLyrics.Globals;
using static SmartLyrics.Toolbox.MiscTools;

namespace SmartLyrics.Services
{
    [Service(Name = "com.SamuelR.SmartLyrics.DownloadService")]
    public class DownloadService : Service
    {
        private List<Song> savedTracks = new List<Song>();
        private readonly int maxDistance = 4;
        private float current = 0;
        private int completedTasks = 0;
        private int callsMade = 0;
        public static int progress = 0;
        private bool isWorking = false;
        private static readonly int NOTIFICATION_ID = 177013;
        private static readonly string CHANNEL_ID = "download_lyrics_bg_sl";
        private readonly string savedLyricsLocation = "SmartLyrics/Saved Lyrics/Spotify/";
        private readonly string savedImagesLocation = "SmartLyrics/Saved Lyrics/Spotify/Image Cache/";
        private string path = Path.Combine(applicationPath, "SmartLyrics/Saved Lyrics/Spotify/");
        private string pathImg = Path.Combine(applicationPath, "SmartLyrics/Saved Lyrics/Spotify/Image Cache/");
        private readonly string savedSeparator = @"!@=-@!";

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
                    Song song = new Song()
                    {
                        Title = Regex.Replace((string)result["track"]["name"], @"\(feat\. .+", ""),
                        Artist = (string)result["track"]["artists"][0]["name"]
                    };

                    savedTracks.Add(song);
                }

                nextURL = parsed["next"].ToString();
            }
        }

        private async Task getGeniusSearchResults()
        {
            List<Song> geniusTemp = new List<Song>();

            float total = savedTracks.Count;

            foreach (Song s in savedTracks)
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

        private async Task geniusSearch(Song s, List<Song> geniusTemp)
        {
            callsMade++;

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "geniusSearch (DownloadService): Starting geniusSearch");
            string results = await HTTPRequests.GetRequest(geniusSearchURL + s.Artist + " - " + s.Title, geniusAuthHeader);
            if (results == null)
            {
                results = await HTTPRequests.GetRequest(geniusSearchURL + s.Artist + " - " + s.Title, geniusAuthHeader);
            }
            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            foreach (JToken result in parsedList)
            {
                string resultTitle = (string)result["result"]["title"];
                string resultArtist = (string)result["result"]["primary_artist"]["name"];

                if ((Text.Distance(resultTitle, s.Title) <= maxDistance && Text.Distance(resultArtist, s.Artist) <= maxDistance) || resultTitle.Contains(s.Title) && resultArtist.Contains(s.Artist))
                {
                    string path = Path.Combine(applicationPath, savedLyricsLocation, (string)result["result"]["primary_artist"]["name"] + savedSeparator + (string)result["result"]["title"] + ".txt");

                    if (!File.Exists(path))
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "geniusSearch (DownloadService): Song found! Adding to geniusTemp");

                        Song song = new Song()
                        {
                            Title = (string)result["result"]["title"],
                            Artist = (string)result["result"]["primary_artist"]["name"],
                            Cover = (string)result["result"]["song_art_image_thumbnail_url"],
                            Header = (string)result["result"]["header_image_url"],
                            APIPath = (string)result["result"]["api_path"],
                            Path = (string)result["result"]["path"]
                        };

                        geniusTemp.Add(song);

                        break;
                    }
                    else
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "geniusSearch (DownloadService): Song found but already downloaded");
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

            List<Song> geniusTemp = new List<Song>();

            float total = savedTracks.Count;

            if (savedTracks.Count != 0)
            {
                foreach (Song s in savedTracks)
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

            Song song = new Song()
            {
                Title = (string)parsed["response"]["song"]["title"],
                Artist = (string)parsed["response"]["song"]["primary_artist"]["name"],
                Album = (string)parsed.SelectToken("response.song.album.name"),
                Header = (string)parsed["response"]["song"]["header_image_url"],
                Cover = (string)parsed["response"]["song"]["song_art_image_url"],
                APIPath = (string)parsed["response"]["song"]["api_path"],
                Path = (string)parsed["response"]["song"]["path"]
            };

            Log.WriteLine(LogPriority.Debug, "SmartLyrics", "getDetails (DownloadService): Created new Song variable");

            if (parsed["response"]["song"]["featured_artists"].HasValues)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Track has featured artists");
                IList<JToken> parsedList = parsed["response"]["song"]["featured_artists"].Children().ToList();
                song.FeaturedArtist = "feat. ";
                foreach (JToken artist in parsedList)
                {
                    if (song.FeaturedArtist == "feat. ")
                    {
                        song.FeaturedArtist += artist["name"].ToString();
                    }
                    else
                    {
                        song.FeaturedArtist += ", " + artist["name"].ToString();
                    }
                }
            }
            else
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Track does not have featured artists");
                song.FeaturedArtist = "";
            }

            string downloadedLyrics;

            HtmlWeb web = new HtmlWeb();
            Log.WriteLine(LogPriority.Debug, "SmartLyrics", "getDetails (DownloadService): Trying to load page");
            HtmlDocument doc = await web.LoadFromWebAsync("https://genius.com" + song.Path);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getDetails (DownloadService): Loaded Genius page");
            HtmlNode lyricsBody = doc.DocumentNode.SelectSingleNode("//div[@class='lyrics']");

            downloadedLyrics = Regex.Replace(lyricsBody.InnerText, @"^\s*", "");
            downloadedLyrics = Regex.Replace(downloadedLyrics, @"[\s]+$", "");
            song.Lyrics = downloadedLyrics;

            await saveSongLyrics(song);
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Finished saving!");

            completedTasks++;
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getDetails (DownloadService): Completed getDetails task for " + song.APIPath);
        }

        private async Task saveSongLyrics(Song song)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "saveSongLyrics (DownloadService): Started saveSongLyrics operation");

            path = Path.Combine(applicationPath, savedLyricsLocation);
            pathImg = Path.Combine(applicationPath, savedImagesLocation);

            try
            {
                path = Path.Combine(path, song.Artist + savedSeparator + song.Title + ".txt");
                string pathHeader = Path.Combine(pathImg, song.Artist + savedSeparator + song.Title + headerSuffix);
                string pathCover = Path.Combine(pathImg, song.Artist + savedSeparator + song.Title + coverSuffix);

                if (!File.Exists(path))
                {
                    using (StreamWriter sw = File.CreateText(path))
                    {
                        Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "saveSongLyrics (DownloadService): File doesn't exist, creating" + path.ToString());
                        await sw.WriteAsync(song.Lyrics);
                        await sw.WriteLineAsync("\n");
                        await sw.WriteLineAsync(@"!!@@/\/\-----00-----/\/\@@!!");
                        await sw.WriteLineAsync(song.Title);
                        await sw.WriteLineAsync(song.Artist);
                        await sw.WriteLineAsync(song.Album);
                        await sw.WriteLineAsync(song.FeaturedArtist);
                        await sw.WriteLineAsync(song.Header);
                        await sw.WriteLineAsync(song.Cover);
                        await sw.WriteLineAsync(song.APIPath);
                        await sw.WriteLineAsync(song.Path);
                    }

                    using (FileStream fileStream = File.Create(pathHeader))
                    {
                        Stream header = await ImageService.Instance.LoadUrl(song.Header).AsJPGStreamAsync();
                        header.Seek(0, SeekOrigin.Begin);
                        header.CopyTo(fileStream);
                    }

                    using (FileStream fileStream = File.Create(pathCover))
                    {
                        Stream cover = await ImageService.Instance.LoadUrl(song.Cover).AsJPGStreamAsync();
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
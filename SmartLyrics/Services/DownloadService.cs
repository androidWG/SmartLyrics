using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
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
using static SmartLyrics.Common.Logging;
using Type = SmartLyrics.Common.Logging.Type;

namespace SmartLyrics.Services
{
    [Service(Name = "com.SamuelR.SmartLyrics.DownloadService")]
    public class DownloadService : Service
    {
        private List<Song> savedTracks = new List<Song>();
        private readonly int maxDistance = 4;
        private float current;
        private int completedTasks;
        private int callsMade;
        public static int Progress;
        private bool isWorking;
        private static readonly int NotificationId = 177013;
        private static readonly string ChannelId = "download_lyrics_bg_sl";
        private readonly string savedLyricsLocation = "SmartLyrics/Saved Lyrics/Spotify/";
        private readonly string savedImagesLocation = "SmartLyrics/Saved Lyrics/Spotify/Image Cache/";
        private string path = Path.Combine(ApplicationPath, "SmartLyrics/Saved Lyrics/Spotify/");
        private string pathImg = Path.Combine(ApplicationPath, "SmartLyrics/Saved Lyrics/Spotify/Image Cache/");
        private readonly string savedSeparator = @"!@=-@!";

        public IBinder Binder { get; private set; }

        public override IBinder OnBind(Intent intent)
        {
            Binder = new ProgressBinder(this);
            Log(Type.Info, "Service bound!");

            CreateNotificationChannel();
            StartWorking(intent);

            return Binder;
        }

        private async Task StartWorking(Intent intent)
        {
            isWorking = true;
            UpdateProgress();

            await GetSavedList(intent.GetStringExtra("AccessToken"));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await GetGeniusSearchResults();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await GetLyricsAndDetails();
        }

        private async Task UpdateProgress()
        {
            NotificationManagerCompat nm = NotificationManagerCompat.From(this);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle(GetString(Resource.String.notificationTitle))
                .SetContentText(GetString(Resource.String.notificationDesc))
                .SetSmallIcon(Resource.Drawable.ic_stat_name)
                .SetProgress(100, 0, false)
                .SetOngoing(true)
                .SetPriority(-1);

            nm.Notify(NotificationId, builder.Build());

            while (isWorking)
            {
                if (Progress == 100)
                {
                    builder.SetProgress(100, 100, false);
                    nm.CancelAll();

                    Log(Type.Event, "Finished work, stopping service...");
                    isWorking = false;
                    StopSelf();
                }

                builder.SetProgress(100, Progress, false);

                nm.Notify(NotificationId, builder.Build());

                await Task.Delay(1000);
            }
        }

        private async Task CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                string name = Resources.GetString(Resource.String.notificationChannelName);
                string description = GetString(Resource.String.notificationChannelDesc);
                NotificationChannel channel = new NotificationChannel(ChannelId, name, NotificationImportance.Low)
                {
                    Description = description
                };

                NotificationManager notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }
        }

        private async Task GetSavedList(string accessToken)
        {
            string nextUrl = "https://api.spotify.com/v1/me/tracks?limit=50";

            while (nextUrl != "")
            {
                Log(Type.Info, "nextURL = " + nextUrl);
                string results = await APIRequests.Spotify.GetSavedSongs("Bearer " + accessToken, nextUrl);
                JObject parsed = JObject.Parse(results);
                Log(Type.Info, "Results parsed into JObject");

                float offset = Convert.ToSingle((string)parsed["offset"]);
                float total = Convert.ToSingle((string)parsed["total"]);
                Progress = (int)ConvertRange(0, 100, 0, 25, (offset / total) * 100);

                IList<JToken> parsedList = parsed["items"].Children().ToList();
                Log(Type.Info, "Parsed results into list");

                foreach (JToken result in parsedList)
                {
                    Song song = new Song()
                    {
                        Title = Regex.Replace((string)result["track"]["name"], @"\(feat\. .+", ""),
                        Artist = (string)result["track"]["artists"][0]["name"]
                    };

                    savedTracks.Add(song);
                }

                nextUrl = parsed["next"].ToString();
            }
        }

        private async Task GetGeniusSearchResults()
        {
            List<Song> geniusTemp = new List<Song>();

            float total = savedTracks.Count;

            foreach (Song s in savedTracks)
            {
                if (callsMade == 50)
                {
                    while (completedTasks < 50)
                    {
                        Log(Type.Error, "geniusSearch tasks still running");
                        await Task.Delay(1000);
                    }

                    completedTasks = 0;
                    callsMade = 0;
                    Log(Type.Info, "No tasks are running!");
                    GeniusSearch(s, geniusTemp);
                }
                else
                {
                    GeniusSearch(s, geniusTemp);
                }

                current++;
                Progress = (int)ConvertRange(0, 100, 0, 25, (current / total) * 100) + 25;

                Log(Type.Info, "foreach for index " + current + " completed.");
            }

            if (total % 25 != 0)
            {
                while (completedTasks < (total % 25))
                {
                    Log(Type.Error, "geniusSearch tasks still running, can't finish task!");
                    await Task.Delay(1000);
                }
            }

            savedTracks = geniusTemp;
            Log(Type.Info, "Changed savedTracks to Genius results");
        }

        private async Task GeniusSearch(Song s, List<Song> geniusTemp)
        {
            callsMade++;

            Log(Type.Info, "Starting geniusSearch");
            string results = await HttpRequests.GetRequest(GeniusSearchUrl + s.Artist + " - " + s.Title, GeniusAuthHeader);
            if (results == null)
            {
                results = await HttpRequests.GetRequest(GeniusSearchUrl + s.Artist + " - " + s.Title, GeniusAuthHeader);
            }
            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            foreach (JToken result in parsedList)
            {
                string resultTitle = (string)result["result"]["title"];
                string resultArtist = (string)result["result"]["primary_artist"]["name"];

                if ((Text.Distance(resultTitle, s.Title) <= maxDistance && Text.Distance(resultArtist, s.Artist) <= maxDistance) || resultTitle.Contains(s.Title) && resultArtist.Contains(s.Artist))
                {
                    string path = Path.Combine(ApplicationPath, savedLyricsLocation, (string)result["result"]["primary_artist"]["name"] + savedSeparator + (string)result["result"]["title"] + ".txt");

                    if (!File.Exists(path))
                    {
                        Log(Type.Info, "Song found! Adding to geniusTemp");

                        Song song = new Song()
                        {
                            Title = (string)result["result"]["title"],
                            Artist = (string)result["result"]["primary_artist"]["name"],
                            Cover = (string)result["result"]["song_art_image_thumbnail_url"],
                            Header = (string)result["result"]["header_image_url"],
                            ApiPath = (string)result["result"]["api_path"],
                            Path = (string)result["result"]["path"]
                        };

                        geniusTemp.Add(song);

                        break;
                    }
                    else
                    {
                        Log(Type.Info, "Song found but already downloaded");
                        break;
                    }
                }
            }

            completedTasks++;
            Log(Type.Event, "completedTasks = " + completedTasks);
        }

        private async Task GetLyricsAndDetails()
        {
            callsMade = 0;
            completedTasks = 0;
            current = 0;

            float total = savedTracks.Count;

            if (savedTracks.Count != 0)
            {
                foreach (Song s in savedTracks)
                {
                    if (callsMade == 10)
                    {
                        while (completedTasks < 10)
                        {
                            Log(Type.Error, "getDetails tasks still running");
                            await Task.Delay(5000);
                        }

                        completedTasks = 0;
                        callsMade = 0;
                        Log(Type.Info, "No tasks are running!");
                        GetDetails(s.ApiPath);
                    }
                    else
                    {
                        GetDetails(s.ApiPath);
                    }

                    current++;
                    Progress = (int)ConvertRange(0, 100, 0, 50, (current / total) * 100) + 50;

                    Log(Type.Info, "foreach for index " + current + " completed.");
                }
            }
            else
            {
                Progress = 100;
                Log(Type.Event, "savedTracks is empty!");
            }

            Progress = 100;
        }

        private async Task GetDetails(string apiPath)
        {
            callsMade++;

            Log(Type.Info, "Starting getDetails operation");
            string results = await HttpRequests.GetRequest(GeniusApiurl + apiPath, GeniusAuthHeader);
            if (results == null)
            {
                Log(Type.Processing, "Returned null, calling API again...");
                results = await HttpRequests.GetRequest(GeniusApiurl + apiPath, GeniusAuthHeader);
            }
            JObject parsed = JObject.Parse(results);

            Song song = new Song()
            {
                Title = (string)parsed["response"]["song"]["title"],
                Artist = (string)parsed["response"]["song"]["primary_artist"]["name"],
                Album = (string)parsed.SelectToken("response.song.album.name"),
                Header = (string)parsed["response"]["song"]["header_image_url"],
                Cover = (string)parsed["response"]["song"]["song_art_image_url"],
                ApiPath = (string)parsed["response"]["song"]["api_path"],
                Path = (string)parsed["response"]["song"]["path"]
            };

            Log(Type.Processing, "Created new Song variable");

            if (parsed["response"]["song"]["featured_artists"].HasValues)
            {
                Log(Type.Info, "Track has featured artists");
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
                        song.FeaturedArtist += ", " + artist["name"];
                    }
                }
            }
            else
            {
                Log(Type.Info, "Track does not have featured artists");
                song.FeaturedArtist = "";
            }

            string downloadedLyrics;

            HtmlWeb web = new HtmlWeb();
            Log(Type.Processing, "Trying to load page");
            HtmlDocument doc = await web.LoadFromWebAsync("https://genius.com" + song.Path);
            Log(Type.Info, "Loaded Genius page");
            HtmlNode lyricsBody = doc.DocumentNode.SelectSingleNode("//div[@class='lyrics']");

            downloadedLyrics = Regex.Replace(lyricsBody.InnerText, @"^\s*", "");
            downloadedLyrics = Regex.Replace(downloadedLyrics, @"[\s]+$", "");
            song.Lyrics = downloadedLyrics;

            await SaveSongLyrics(song);
            Log(Type.Info, "Finished saving!");

            completedTasks++;
            Log(Type.Info, "Completed getDetails task for " + song.ApiPath);
        }

        private async Task SaveSongLyrics(Song song)
        {
            Log(Type.Info, "Started saveSongLyrics operation");

            path = Path.Combine(ApplicationPath, savedLyricsLocation);
            pathImg = Path.Combine(ApplicationPath, savedImagesLocation);

            try
            {
                path = Path.Combine(path, song.Artist + savedSeparator + song.Title + ".txt");
                string pathHeader = Path.Combine(pathImg, song.Artist + savedSeparator + song.Title + HeaderSuffix);
                string pathCover = Path.Combine(pathImg, song.Artist + savedSeparator + song.Title + CoverSuffix);

                if (!File.Exists(path))
                {
                    using (StreamWriter sw = File.CreateText(path))
                    {
                        Log(Type.Info, "File doesn't exist, creating" + path);
                        await sw.WriteAsync(song.Lyrics);
                        await sw.WriteLineAsync("\n");
                        await sw.WriteLineAsync(@"!!@@/\/\-----00-----/\/\@@!!");
                        await sw.WriteLineAsync(song.Title);
                        await sw.WriteLineAsync(song.Artist);
                        await sw.WriteLineAsync(song.Album);
                        await sw.WriteLineAsync(song.FeaturedArtist);
                        await sw.WriteLineAsync(song.Header);
                        await sw.WriteLineAsync(song.Cover);
                        await sw.WriteLineAsync(song.ApiPath);
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
                    Log(Type.Info, "File already exists, let's do nothing!");
                }
            }
            catch (Exception e)
            {
                Log(Type.Error, "Exception caught while saving song in DownloadService!\n" + e);
            }

        }

        public int GetProgress() { return Progress; }
    }
}
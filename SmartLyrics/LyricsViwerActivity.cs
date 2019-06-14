using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System;
using Android.Util;
using Android.Support.V4.App;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Graphics.Drawables;
using FFImageLoading;
using FFImageLoading.Transformations;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using Plugin.CurrentActivity;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;

namespace SmartLyrics
{
    [Activity(Label = "LyricsViwerActivity")]
    public class LyricsViwerActivity : AppCompatActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        string downloadedLyrics;
        string[] songExtraInfo;
        int coverRadius = 10;
        int headerBlur = 20;
        int searchBlur = 50;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.lyrics_viwer);

            CrossCurrentActivity.Current.Init(this, Bundle.Empty);

            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            ImageView coverView = FindViewById<ImageView>(Resource.Id.coverView);
            ImageView headerView = FindViewById<ImageView>(Resource.Id.headerView);
            ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);
            ImageButton saveBtn = FindViewById<ImageButton>(Resource.Id.saveBtn);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "LyricsViwerActivity.cs: Loaded view");

            if (Intent.GetStringExtra("FromFile") == "false")
            {
                if (File.Exists(Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, "SmartLyrics/Saved Lyrics/" + Intent.GetStringExtra("Artist") + "-" + Intent.GetStringExtra("Title") + ".txt")))
                {
                    if (await checkAndSetPermissions())
                    {
                        Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: File "+ Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, "SmartLyrics/Saved Lyrics/" + Intent.GetStringExtra("Artist") + "-" + Intent.GetStringExtra("Title") + ".txt").ToString() + " for song exists, loading...");
                        await readFromFile();
                    }
                }
                else
                {
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: File for song doesn't exist, getting data from Genius...");

                    getAndShowSongDetails();
                    getAndShowLyrics();

                    songTitle.Text = Intent.GetStringExtra("Title") ?? "Data not availible";
                    songArtist.Text = Intent.GetStringExtra("Artist") ?? "Data not availible";
                    ImageService.Instance.LoadUrl(Intent.GetStringExtra("Cover")).Transform(new RoundedTransformation(coverRadius)).Into(coverView);
                    ImageService.Instance.LoadUrl(Intent.GetStringExtra("Header")).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                    ImageService.Instance.LoadUrl(Intent.GetStringExtra("Header")).Transform(new BlurredTransformation(searchBlur)).Into(searchView);
                }
            }
            else if (Intent.GetStringExtra("FromFile") == "true")
            {
                if (await checkAndSetPermissions())
                {
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: File " + Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, "SmartLyrics/Saved Lyrics/" + Intent.GetStringExtra("Artist") + "-" + Intent.GetStringExtra("Title") + ".txt").ToString() + " for song exists, loading...");
                    await readFromFile();
                }
            }

            saveBtn.Click += async delegate
            {
                if (await checkAndSetPermissions())
                {
                    await saveSongLyrics();
                }
                else
                {
                    //TODO snackbar
                }
            };
        }

        private async Task getAndShowSongDetails()
        {
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: Starting async GetSongDeatils operation");
            string results = await Genius.GetSongDetails(Intent.GetStringExtra("APIPath"), "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: Results parsed into JObject");

            songAlbum.Text = (string)parsed["response"]["song"]["album"]["name"];
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "LyricsViwerActivity.cs: Displaying album name");

            if (parsed["response"]["song"]["featured_artists"].HasValues)
            {
                songFeat.Visibility = ViewStates.Visible;
                IList<JToken> parsedList = parsed["response"]["song"]["featured_artists"].Children().ToList();
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: Featured artists parsed list created");
                foreach (JToken artist in parsedList)
                {
                    if (songFeat.Text == "")
                    {
                        songFeat.Text += "feat. " + artist["name"].ToString();
                    }
                    else
                    {
                        songFeat.Text += (", " + artist["name"].ToString());
                    }
                }
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "LyricsViwerActivity.cs: Displaying featured artists");
            }
            else
            {
                songFeat.Visibility = ViewStates.Gone;
            }

            songExtraInfo = new string[] {songFeat.Text, songAlbum.Text};
        }

        private async Task getAndShowLyrics()
        {
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);

            HtmlWeb web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync("https://genius.com"+ Intent.GetStringExtra("Path"));
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: Loaded Genius page");
            var lyricsBody = doc.DocumentNode.SelectSingleNode("//div[@class='lyrics']");

            lyricsLoadingWheel.Visibility = ViewStates.Gone;
            downloadedLyrics = Regex.Replace(lyricsBody.InnerText, @"^\s*", "");
            downloadedLyrics = Regex.Replace(downloadedLyrics, @"[\s]+$", "");
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: RegEx work done!");
            songLyrics.Text = downloadedLyrics;
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "LyricsViwerActivity.cs: Showing lyrics");
        }

        private async Task readFromFile()
        {
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, "SmartLyrics/Saved Lyrics/" + Intent.GetStringExtra("Artist") + "-" + Intent.GetStringExtra("Title") + ".txt");
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);
            ImageView coverView = FindViewById<ImageView>(Resource.Id.coverView);
            ImageView headerView = FindViewById<ImageView>(Resource.Id.headerView);
            ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);

            string GetLine(string text, int lineNo)
            {
                string[] lines = text.Replace("\r", "").Split('\n');
                return lines.Length >= lineNo ? lines[lineNo - 1] : null;
            }

            using (StreamReader sr = File.OpenText(path))
            {
                string loadedLyrics = await sr.ReadToEndAsync();
                string loadedInfo = Regex.Replace(loadedLyrics, @"[\s\S]*.*(!!@@\/\\\/\\-----00-----\/\\\/\\@@!!).*[\s\S]", "");
                loadedLyrics = Regex.Replace(loadedLyrics, @"(!!@@\/\\\/\\-----00-----\/\\\/\\@@!!)[\s\S]*.*", "");

                songLyrics.Text = loadedLyrics;

                songTitle.Text = GetLine(loadedInfo, 1);
                songArtist.Text = GetLine(loadedInfo, 2);
                if (GetLine(loadedInfo, 3) == "")
                {
                    songFeat.Visibility = ViewStates.Gone;
                }
                songFeat.Text = GetLine(loadedInfo, 3);
                songAlbum.Text = GetLine(loadedInfo, 4);
                ImageService.Instance.LoadUrl(GetLine(loadedInfo, 6)).Transform(new RoundedTransformation(coverRadius)).Into(coverView);
                ImageService.Instance.LoadUrl(GetLine(loadedInfo, 5)).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                ImageService.Instance.LoadUrl(GetLine(loadedInfo, 5)).Transform(new BlurredTransformation(searchBlur)).Into(searchView);

                lyricsLoadingWheel.Visibility = ViewStates.Gone;
            }
        }

        private async Task<bool> checkAndSetPermissions()
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: Starting checkAndSetPermissions...");

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    var status = await CrossPermissions.Current.CheckPermissionStatusAsync(Plugin.Permissions.Abstractions.Permission.Storage);

                    if (status != PermissionStatus.Granted)
                    {
                        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Plugin.Permissions.Abstractions.Permission.Storage))
                        {
                            Log.WriteLine(LogPriority.Warn, "SmartLyrics", "LyricsViwerActivity.cs: Permission rationale thing... don't know what happened here.");
                            return false;
                        }

                        var results = await CrossPermissions.Current.RequestPermissionsAsync(new[] { Plugin.Permissions.Abstractions.Permission.Storage });
                        status = results[Plugin.Permissions.Abstractions.Permission.Storage];
                    }

                    if (status == PermissionStatus.Granted)
                    {
                        Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: Permission granted, returning true...");
                        return true;
                    }
                    else
                    {
                        Log.WriteLine(LogPriority.Warn, "SmartLyrics", "LyricsViwerActivity.cs: Something else happened, not granted but not unkown...");
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "LyricsViwerActivity.cs: Exception caught! " + ex.Message);
                return false;
            }
        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private async Task saveSongLyrics()
        {
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, "SmartLyrics/Saved Lyrics");

            if (Directory.Exists(path))
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: /SmartLyrics/ directory exists!");
            }
            else
            {
                Directory.CreateDirectory(path);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: /SmartLyrics/Saved Lyrics directory doesn't exist, creating...");
            }

            path = Path.Combine(path, Intent.GetStringExtra("Artist") + "-" + Intent.GetStringExtra("Title") + ".txt");

            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: File doesn't exist, creating" + path.ToString());
                    await sw.WriteAsync(downloadedLyrics);
                    await sw.WriteLineAsync("\n");
                    await sw.WriteLineAsync(@"!!@@/\/\-----00-----/\/\@@!!");
                    await sw.WriteLineAsync(Intent.GetStringExtra("Title"));
                    await sw.WriteLineAsync(Intent.GetStringExtra("Artist"));
                    await sw.WriteLineAsync(songExtraInfo[0]);
                    await sw.WriteLineAsync(songExtraInfo[1]);
                    await sw.WriteLineAsync(Intent.GetStringExtra("Header"));
                    await sw.WriteLineAsync(Intent.GetStringExtra("Cover"));
                    await sw.WriteLineAsync(Intent.GetStringExtra("APIPath"));
                    await sw.WriteLineAsync(Intent.GetStringExtra("Path"));

                    LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.linearFullscreen);
                    var snackbar = Snackbar.Make(layout, "Lyrics saved successfully", Snackbar.LengthLong);
                    snackbar.Show();
                }
            }
            else
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "LyricsViwerActivity.cs: File already exists, let's do nothing");
            }
        }
    }
}
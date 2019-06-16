using Android.App;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Widget;
using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using Android.Util;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System;
using FFImageLoading;
using FFImageLoading.Cache;
using FFImageLoading.Transformations;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using Plugin.CurrentActivity;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;

namespace SmartLyrics
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        List<Genius.Song> resultsToView;
        Genius.Song songInfo = new Genius.Song();
        string downloadedLyrics;
        int coverRadius = 10;
        int headerBlur = 20;
        int searchBlur = 25;
        string savedLyricsLocation = "SmartLyrics/Saved Lyrics/";
        string savedImagesLocation = "SmartLyrics/Saved Lyrics/Image Cache/";
        string savedSeparator = @"!@=-@!";

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main_search);
            CrossCurrentActivity.Current.Activity = this; //don't remove this, permission stuff needs it
            CrossCurrentActivity.Current.Init(this, savedInstanceState);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);
            SupportActionBar.Title = "";
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            Android.Support.V7.App.ActionBarDrawerToggle toggle = new Android.Support.V7.App.ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
            drawer.AddDrawerListener(toggle);
            toggle.SyncState();

            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;

            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            ImageButton saveBtn = FindViewById<ImageButton>(Resource.Id.saveBtn);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Loaded view");

            if (Intent.GetStringExtra("From File?") == "true")
            {
                songInfo.title = Intent.GetStringExtra("Title");
                songInfo.artist = Intent.GetStringExtra("Artist");

                await loadSong();
            }

            saveBtn.Click += async delegate
            {
                await checkAndSetPermissions(Manifest.Permission.WriteExternalStorage);
            };

            searchTxt.EditorAction += async delegate { await searchBtn_Click(); };
            searchResults.ItemClick += searchResults_ItemClick;
        }

        async Task searchBtn_Click()
        {
            ProgressBar searchLoadingWheel = FindViewById<ProgressBar>(Resource.Id.searchLoadingWheel);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            LinearLayout searchOverlay = FindViewById<LinearLayout>(Resource.Id.searchOverlay);

            if (searchTxt.Text != "")
            {
                searchLoadingWheel.Visibility = ViewStates.Visible;
                searchOverlay.Visibility = ViewStates.Visible;
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Started 'await getAndShowSearchResults' task");
                InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);
                imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);
                await getAndShowSearchResults();
                searchLoadingWheel.Visibility = ViewStates.Gone;
            }
        }

        async void searchResults_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            LinearLayout searchOverlay = FindViewById<LinearLayout>(Resource.Id.searchOverlay);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);

            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Attempting to display song...");

            songInfo.title = resultsToView.ElementAt(e.Position).title.ToString();
            songInfo.artist = resultsToView.ElementAt(e.Position).artist.ToString();
            songInfo.header = resultsToView.ElementAt(e.Position).header.ToString();
            songInfo.cover = resultsToView.ElementAt(e.Position).cover.ToString();
            songInfo.APIPath = resultsToView.ElementAt(e.Position).APIPath.ToString();
            songInfo.path = resultsToView.ElementAt(e.Position).path.ToString();
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Added info to class");

            searchOverlay.Visibility = ViewStates.Gone;
            lyricsLoadingWheel.Visibility = ViewStates.Visible;

            songLyrics.Text = "";
            songTitle.Text = "";
            songArtist.Text = "";
            songAlbum.Text = "";
            songFeat.Text = "";

            loadSong();
        }

        void NavigationView_NavigationViewSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);

            switch (e.MenuItem.ItemId)
            {
                case (Resource.Id.nav_search):
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Test for Search button on drawer");
                    break;
                case (Resource.Id.nav_saved):
                    StartActivity(typeof(SavedLyrics));
                    break;
                case (Resource.Id.nav_settings):
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Test for Settings button on drawer");
                    break;
                case (Resource.Id.nav_about):
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Test for About button on drawer");
                    break;
            }

            drawer.CloseDrawers();
        }

        private async Task getAndShowSearchResults()
        {
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Starting async GetSearchResults operation");
            string results = await Genius.GetSearchResults(searchTxt.Text, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Results parsed into JObject");

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Parsed results into list");
            resultsToView = new List<Genius.Song>();
            foreach (JToken result in parsedList)
            {
                Genius.Song song = new Genius.Song()
                {
                    title = (string)result["result"]["title"],
                    artist = (string)result["result"]["primary_artist"]["name"],
                    cover = (string)result["result"]["song_art_image_thumbnail_url"],
                    header = (string)result["result"]["header_image_url"],
                    APIPath = (string)result["result"]["api_path"],
                    path = (string)result["result"]["path"]
                };

                resultsToView.Add(song);
            }
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Created results list for listVew");

            var adapter = new Genius.SearchResultAdapter(this, resultsToView);
            searchResults.Adapter = adapter;

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Added results to activity view");
        }

        private async Task loadSong()
        {
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            ImageView coverView = FindViewById<ImageView>(Resource.Id.coverView);
            ImageView headerView = FindViewById<ImageView>(Resource.Id.headerView);
            ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);

            if (File.Exists(Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation + songInfo.artist + savedSeparator + songInfo.title + ".txt")))
            {
                await checkAndSetPermissions(Manifest.Permission.ReadExternalStorage);
            }
            else
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: File for song doesn't exist, getting data from Genius...");

                getAndShowSongDetails();
                getAndShowLyrics();

                songTitle.Text = songInfo.title;
                songArtist.Text = songInfo.artist;
                ImageService.Instance.LoadUrl(songInfo.cover).Transform(new RoundedTransformation(coverRadius)).Into(coverView);
                ImageService.Instance.LoadUrl(songInfo.header).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                ImageService.Instance.LoadUrl(songInfo.header).Transform(new CropTransformation(3, 0, 0)).Transform(new BlurredTransformation(searchBlur)).Transform(new BlurredTransformation(searchBlur)).Into(searchView);
            }
        }

        private async Task getAndShowSongDetails()
        {
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Starting async GetSongDeatils operation");
            string results = await Genius.GetSongDetails(songInfo.APIPath, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Results parsed into JObject");

            songInfo.album = (string)parsed["response"]["song"]["album"]["name"];
            songAlbum.Text = songInfo.album;
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Displaying album name");

            if (parsed["response"]["song"]["featured_artists"].HasValues)
            {
                songFeat.Visibility = ViewStates.Visible;
                IList<JToken> parsedList = parsed["response"]["song"]["featured_artists"].Children().ToList();
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Featured artists parsed list created");
                songInfo.featuredArtist = "feat. ";
                foreach (JToken artist in parsedList)
                {
                    if (songInfo.featuredArtist == "feat. ")
                    {
                        songInfo.featuredArtist += artist["name"].ToString();
                    }
                    else
                    {
                        songInfo.featuredArtist += ", " + artist["name"].ToString();
                    }
                }

                songFeat.Text = songInfo.featuredArtist;
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Displaying featured artists");
            }
            else
            {
                songFeat.Visibility = ViewStates.Gone;
            }
        }

        private async Task getAndShowLyrics()
        {
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            ImageButton saveBtn = FindViewById<ImageButton>(Resource.Id.saveBtn);

            HtmlWeb web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync("https://genius.com" + songInfo.path);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Loaded Genius page");
            var lyricsBody = doc.DocumentNode.SelectSingleNode("//div[@class='lyrics']");

            lyricsLoadingWheel.Visibility = ViewStates.Gone;
            saveBtn.Visibility = ViewStates.Visible;

            downloadedLyrics = Regex.Replace(lyricsBody.InnerText, @"^\s*", "");
            downloadedLyrics = Regex.Replace(downloadedLyrics, @"[\s]+$", "");
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: RegEx work done!");
            songLyrics.Text = downloadedLyrics;
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Showing lyrics");
        }

        private async Task readFromFile()
        {
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation + songInfo.artist + savedSeparator + songInfo.title + ".txt");
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
                if (GetLine(loadedInfo, 4) != "")
                {
                    songFeat.Visibility = ViewStates.Visible;
                    songFeat.Text = GetLine(loadedInfo, 4);
                }
                else
                {
                    songFeat.Visibility = ViewStates.Gone;
                }
                songAlbum.Text = GetLine(loadedInfo, 3);

                var coverPath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path + "/" + savedImagesLocation, GetLine(loadedInfo, 2) + savedSeparator + GetLine(loadedInfo, 1) + "-cover.jpg");
                var headerPath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path + "/" + savedImagesLocation, GetLine(loadedInfo, 2) + savedSeparator + GetLine(loadedInfo, 1) + "-header.jpg");

                Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Cover path:" + coverPath);

                ImageService.Instance.LoadFile(coverPath).Transform(new RoundedTransformation(coverRadius)).Into(coverView);
                ImageService.Instance.LoadFile(headerPath).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                ImageService.Instance.LoadFile(headerPath).Transform(new CropTransformation(3, 0, 0)).Transform(new BlurredTransformation(searchBlur)).Transform(new BlurredTransformation(searchBlur)).Into(searchView);

                lyricsLoadingWheel.Visibility = ViewStates.Gone;
            }
        }

        public async Task checkAndSetPermissions(string permission)
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Starting checkAndSetPermissions...");

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    if (ContextCompat.CheckSelfPermission(this, permission) == (int)Android.Content.PM.Permission.Granted)
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Permission for" + permission + " already granted");
                        
                        if (permission == Manifest.Permission.ReadExternalStorage)
                        {
                            await readFromFile();
                        }
                        else
                        {
                            await saveSongLyrics();
                        }
                    }
                    else
                    {
                        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Storage))
                        {
                            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: 'My lord, is this legal?' 'I will make it legal...'");

                            LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.linearFullscreen);
                            string[] p = { permission };
                            var snackbar = Snackbar.Make(layout, Resource.String.needStoragePermission, Snackbar.LengthIndefinite)
                                .SetAction(Android.Resource.String.Ok, new Action<View>(delegate (View obj)
                                {
                                    ActivityCompat.RequestPermissions(this, p, 1);
                                }));
                            snackbar.Show();
                        }
                        else
                        {
                            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: No need to ask user, trying to get permission...");

                            string[] p = { permission };
                            ActivityCompat.RequestPermissions(this, p, 1);
                        }
                    }
                }
                else
                {
                    await saveSongLyrics();
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "MainActivity.cs: Exception caught! " + ex.Message);
            }
        }

        private async Task saveSongLyrics()
        {
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation);
            var pathImg = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedImagesLocation);

            if (Directory.Exists(path))
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: /SmartLyrics/Saved Lyrics directory exists!");
            }
            else
            {
                Directory.CreateDirectory(path);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: /SmartLyrics/Saved Lyrics directory doesn't exist, creating...");
            }

            if (Directory.Exists(pathImg))
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: /SmartLyrics/Saved Lyrics/ImageCache directory exists!");
            }
            else
            {
                Directory.CreateDirectory(pathImg);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: /SmartLyrics/Saved Lyrics/ImageCache directory doesn't exist, creating...");
            }

            path = Path.Combine(path, songInfo.artist + savedSeparator + songInfo.title + ".txt");
            var pathHeader = Path.Combine(pathImg, songInfo.artist + savedSeparator + songInfo.title + "-header.jpg");
            var pathCover = Path.Combine(pathImg, songInfo.artist + savedSeparator + songInfo.title + "-cover.jpg");

            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: File doesn't exist, creating" + path.ToString());
                    await sw.WriteAsync(downloadedLyrics);
                    await sw.WriteLineAsync("\n");
                    await sw.WriteLineAsync(@"!!@@/\/\-----00-----/\/\@@!!");
                    await sw.WriteLineAsync(songInfo.title);
                    await sw.WriteLineAsync(songInfo.artist);
                    await sw.WriteLineAsync(songInfo.album);
                    await sw.WriteLineAsync(songInfo.featuredArtist);
                    await sw.WriteLineAsync(songInfo.header);
                    await sw.WriteLineAsync(songInfo.cover);
                    await sw.WriteLineAsync(songInfo.APIPath);
                    await sw.WriteLineAsync(songInfo.path);
                }

                using (var fileStream = File.Create(pathHeader))
                {
                    Stream header = await ImageService.Instance.LoadUrl(songInfo.header).AsJPGStreamAsync();
                    header.Seek(0, SeekOrigin.Begin);
                    header.CopyTo(fileStream);
                }

                using (var fileStream = File.Create(pathCover))
                {
                    Stream cover = await ImageService.Instance.LoadUrl(songInfo.cover).AsJPGStreamAsync();
                    cover.Seek(0, SeekOrigin.Begin);
                    cover.CopyTo(fileStream);
                }

                LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.linearFullscreen);
                var snackbar = Snackbar.Make(layout, "Lyrics saved successfully", Snackbar.LengthLong);
                snackbar.Show();
            }
            else
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: File already exists, let's do nothing!");
            }
        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Permission: " + permissions[0] + " | Result: " + grantResults[0].ToString());

            if (permissions[0] == Manifest.Permission.WriteExternalStorage && grantResults[0] == Android.Content.PM.Permission.Granted)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Write permission granted!");
                await saveSongLyrics();
            }
            else if (permissions[0] == Manifest.Permission.ReadExternalStorage && grantResults[0] == Android.Content.PM.Permission.Granted)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Read permission granted!");
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: File " + Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation + songInfo.artist + savedSeparator + songInfo.title + ".txt").ToString() + " for song exists, loading...");
                await readFromFile();
            }
            else if (grantResults[0] == Android.Content.PM.Permission.Denied || grantResults[1] == Android.Content.PM.Permission.Denied)
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "MainActivity.cs: Permission denied");

                LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.linearFullscreen);
                var snackbar = Snackbar.Make(layout, Resource.String.permissionDenied, Snackbar.LengthLong);
                snackbar.Show();
            }
        }
    }
}
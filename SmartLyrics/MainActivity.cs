using Android;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Support.Constraints;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using FFImageLoading;
using FFImageLoading.Transformations;
using HtmlAgilityPack;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json.Linq;
using Plugin.CurrentActivity;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartLyrics
{
    [Activity(Label = "@string/app_name", MainLauncher = true, ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.Orientation, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        List<Song> resultsToView;
        public static Song songInfo = new Song();
        public static Song notificationSong = new Song();
        public static bool fromFile = false;
        public static bool fromNotification = false;
        public static bool checkOnStart = false;
        string downloadedLyrics;
        int coverRadius = 16;
        int headerBlur = 25;
        int searchBlur = 25;
        string savedSeparator = @"!@=-@!";
        string lastView;
        string lastSearch = "";
        ISharedPreferences prefs;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main);
            CrossCurrentActivity.Current.Activity = this; //don't remove this, permission stuff needs it
            CrossCurrentActivity.Current.Init(this, savedInstanceState);

            AppCenter.Start("b9b36f01-6d42-4ead-afc5-37568c7b8518", typeof(Analytics), typeof(Crashes));

            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;

            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            ImageButton searchBtn = FindViewById<ImageButton>(Resource.Id.searchBtn);
            ImageButton drawerBtn = FindViewById<ImageButton>(Resource.Id.drawerBtn);
            ImageButton coverView = FindViewById<ImageButton>(Resource.Id.coverView);
            Button gotoLyricsBtn = FindViewById<Button>(Resource.Id.gotoLyricsBtn);
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            TextView npTxt = FindViewById<TextView>(Resource.Id.npTxt);
            ConstraintLayout welcomeScreen = FindViewById<ConstraintLayout>(Resource.Id.welcomeScreen);

            prefs = PreferenceManager.GetDefaultSharedPreferences(this);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnCreate (MainActivity): Loaded view");

            if (checkOnStart)
            {
                npTxt.Visibility = ViewStates.Visible;
                ChangeNotificationToInfo();
                await LoadSong();
            }

            coverView.Click += async delegate
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "OnCreate (MainActivity): Clicked on save button");
                await CheckAndSetPermissions(Manifest.Permission.WriteExternalStorage);
            };

            refreshLayout.Refresh += async delegate
            {
                if (notificationSong.title != songInfo.title && notificationSong.artist != songInfo.artist && notificationSong.title != null)
                {
                    npTxt.Visibility = ViewStates.Visible;
                    songLyrics.Text = "";

                    ChangeNotificationToInfo();
                    await LoadSong();
                }
                else
                {
                    refreshLayout.Refreshing = false;
                }
            };

            searchBtn.Click += async delegate { await searchBtn_Click(); };

            drawerBtn.Click += delegate
            {
                drawer.OpenDrawer(navigationView);
            };

            gotoLyricsBtn.Click += delegate
            {
                var intent = new Intent(this, typeof(SavedLyrics)).SetFlags(ActivityFlags.ReorderToFront);
                StartActivity(intent);
            };

            searchTxt.EditorAction += async delegate { await searchBtn_Action(); };
            searchResults.ItemClick += searchResults_ItemClick;
        }

        protected override async void OnResume()
        {
            base.OnResume();

            TextView npTxt = FindViewById<TextView>(Resource.Id.npTxt);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnResume (MainActivity): onResume started");

            if (fromFile)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnResume (MainActivity): Trying to load song from file");

                npTxt.Visibility = ViewStates.Gone;
                fromFile = false;
                await LoadSong();
            }
            else if (fromNotification)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnResume (MainActivity): Trying to load song from notification");

                npTxt.Visibility = ViewStates.Visible;
                ChangeNotificationToInfo();
                fromNotification = false;
                await LoadSong();
            }
        }

        async Task searchBtn_Click()
        {
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            ConstraintLayout welcomeScreen = FindViewById<ConstraintLayout>(Resource.Id.welcomeScreen);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);

            InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);

            if (searchTxt.Visibility == ViewStates.Visible)
            {
                await searchBtn_Click();
            }
            else if (searchTxt.Visibility == ViewStates.Visible && lastSearch == searchTxt.Text)
            {
                headerTxt.Visibility = ViewStates.Visible;
                searchTxt.Visibility = ViewStates.Gone;
                searchResults.Visibility = ViewStates.Gone;

                if (lastView == "welcome")
                {
                    welcomeScreen.Visibility = ViewStates.Visible;
                }
                else
                {
                    refreshLayout.Visibility = ViewStates.Visible;
                }
            }
            else
            {
                if (welcomeScreen.Visibility == ViewStates.Visible)
                {
                    lastView = "welcome";
                }
                else if (refreshLayout.Visibility == ViewStates.Visible)
                {
                    lastView = "lyrics";
                }

                searchResults.Visibility = ViewStates.Visible;
                headerTxt.Visibility = ViewStates.Gone;
                searchTxt.Visibility = ViewStates.Visible;
                welcomeScreen.Visibility = ViewStates.Gone;
                refreshLayout.Visibility = ViewStates.Gone;

                imm.ShowSoftInput(searchTxt, ShowFlags.Forced);
            }
        }

        async Task searchBtn_Action()
        {
            ProgressBar searchLoadingWheel = FindViewById<ProgressBar>(Resource.Id.searchLoadingWheel);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);

            InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);

            if (searchTxt.Text != "")
            {
                lastSearch = searchTxt.Text;
                searchLoadingWheel.Visibility = ViewStates.Visible;
                searchResults.Visibility = ViewStates.Visible;
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "searchBtn_Click (MainActivity): Started 'await getAndShowSearchResults' task");
                imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);
                await GetAndShowSearchResults();
                searchLoadingWheel.Visibility = ViewStates.Gone;
            }
        }

        async void searchResults_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            TextView npTxt = FindViewById<TextView>(Resource.Id.npTxt);

            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "searchResults_ItemClick (MainActivity): Attempting to display song...");

            songInfo.title = resultsToView.ElementAt(e.Position).title.ToString();
            songInfo.artist = resultsToView.ElementAt(e.Position).artist.ToString();
            songInfo.header = resultsToView.ElementAt(e.Position).header.ToString();
            songInfo.cover = resultsToView.ElementAt(e.Position).cover.ToString();
            songInfo.APIPath = resultsToView.ElementAt(e.Position).APIPath.ToString();
            songInfo.path = resultsToView.ElementAt(e.Position).path.ToString();
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "searchResults_ItemClick (MainActivity): Added info to class");

            lyricsLoadingWheel.Visibility = ViewStates.Visible;
            npTxt.Visibility = ViewStates.Gone;
            searchResults.Visibility = ViewStates.Gone;

            songLyrics.Text = "";
            songTitle.Text = "";
            songArtist.Text = "";
            songAlbum.Text = "";
            songFeat.Text = "";

            LoadSong();
        }

        void NavigationView_NavigationViewSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);

            e.MenuItem.SetCheckable(true);
            var intent = new Intent(this, typeof(SavedLyrics)).SetFlags(ActivityFlags.ReorderToFront);

            switch (e.MenuItem.ItemId)
            {
                case (Resource.Id.nav_search):
                    drawer.CloseDrawers();
                    break;
                case (Resource.Id.nav_saved):
                    intent = new Intent(this, typeof(SavedLyrics)).SetFlags(ActivityFlags.ReorderToFront);
                    StartActivity(intent);
                    break;
                case (Resource.Id.nav_spotify):
                    intent = new Intent(this, typeof(SpotifyDownload)).SetFlags(ActivityFlags.ReorderToFront);
                    StartActivity(intent);
                    break;
                case (Resource.Id.nav_settings):
                    intent = new Intent(this, typeof(Settings)).SetFlags(ActivityFlags.ReorderToFront);
                    StartActivity(intent);
                    break;
            }

            e.MenuItem.SetCheckable(false);
            drawer.CloseDrawers();
        }

        private async Task GetAndShowSearchResults()
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getAndShowSearchResults (MainActivity): Started getAndShowSearchResults method");

            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndShowSearchResults (MainActivity): Starting GetSearchResults operation");
            string results = await Genius.GetSearchResults(searchTxt.Text, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndShowSearchResults (MainActivity): Results parsed into JObject");

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndShowSearchResults (MainActivity): Parsed results into list");
            resultsToView = new List<Song>();
            foreach (JToken result in parsedList)
            {
                Song song = new Song()
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
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndShowSearchResults (MainActivity): Created results list for listVew");

            var adapter = new SearchResultAdapter(this, resultsToView);
            searchResults.Adapter = adapter;

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getAndShowSearchResults (MainActivity): Added results to activity view");
        }

        private void ChangeNotificationToInfo()
        {
            songInfo.title = notificationSong.title;
            songInfo.artist = notificationSong.artist;
            songInfo.featuredArtist = notificationSong.featuredArtist;
            songInfo.album = notificationSong.album;
            songInfo.cover = notificationSong.cover;
            songInfo.header = notificationSong.header;
            songInfo.APIPath = notificationSong.APIPath;
            songInfo.path = notificationSong.path;
        }

        private async Task LoadSong()
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "loadSong (MainActivity): Started loadSong method");

            checkOnStart = false;

            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            ImageButton coverView = FindViewById<ImageButton>(Resource.Id.coverView);
            ImageView headerView = FindViewById<ImageView>(Resource.Id.headerView);
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            ImageView shadowView = FindViewById<ImageView>(Resource.Id.shadowView);
            ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);

            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
            ConstraintLayout welcomeScreen = FindViewById<ConstraintLayout>(Resource.Id.welcomeScreen);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);

            refreshLayout.Visibility = ViewStates.Visible;
            searchTxt.Visibility = ViewStates.Gone;
            headerTxt.Visibility = ViewStates.Visible;
            welcomeScreen.Visibility = ViewStates.Gone;

            if (File.Exists(Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, GlobalMethods.savedLyricsLocation + songInfo.artist + savedSeparator + songInfo.title + ".txt")))
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "loadSong (MainActivity): File for song doesn't exists, starting checkAndSetPermissions...");
                await CheckAndSetPermissions(Manifest.Permission.ReadExternalStorage);
            }
            else
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "loadSong (MainActivity): File for song doesn't exist, getting data from Genius...");

                songTitle.Text = songInfo.title;
                songArtist.Text = songInfo.artist;
                savedView.Visibility = ViewStates.Gone;
                shadowView.Visibility = ViewStates.Visible;
                ImageService.Instance.LoadUrl(songInfo.cover).Transform(new RoundedTransformation(coverRadius)).Into(coverView);
                ImageService.Instance.LoadUrl(songInfo.header).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                ImageService.Instance.LoadUrl(songInfo.header).Transform(new CropTransformation(3, 0, 0)).Transform(new BlurredTransformation(searchBlur)).Transform(new BlurredTransformation(searchBlur)).Into(searchView);

                GetAndShowSongDetails();
                await GetAndShowLyrics();
            }
        }

        private async Task GetAndShowSongDetails()
        {
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getAndShowSongDetails (MainActivity): Starting GetSongDetails operation");
            string results = await Genius.GetSongDetails(songInfo.APIPath, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndShowSongDetails (MainActivity): Results parsed into JObject");

            songInfo.album = (string)parsed["response"]["song"]["album"]["name"];
            songAlbum.Text = songInfo.album;
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "getAndShowSongDetails (MainActivity): Displaying album name");

            if (parsed["response"]["song"]["featured_artists"].HasValues)
            {
                songFeat.Visibility = ViewStates.Visible;
                IList<JToken> parsedList = parsed["response"]["song"]["featured_artists"].Children().ToList();
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "getAndShowSongDetails (MainActivity): Featured artists parsed list created");
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
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "getAndShowSongDetails (MainActivity): Displaying featured artists");
            }
            else
            {
                songFeat.Visibility = ViewStates.Gone;
                songInfo.featuredArtist = "";
            }
        }

        private async Task GetAndShowLyrics()
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "GetAndShowLyrics (MainActivity): Started GetAndShowLyrics method");

            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);

            HtmlWeb web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync("https://genius.com" + songInfo.path);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowLyrics (MainActivity): Loaded Genius page");
            var lyricsBody = doc.DocumentNode.SelectSingleNode("//div[@class='lyrics']");

            lyricsLoadingWheel.Visibility = ViewStates.Gone;
            refreshLayout.Refreshing = false;

            downloadedLyrics = Regex.Replace(lyricsBody.InnerText, @"^\s*", "");
            downloadedLyrics = Regex.Replace(downloadedLyrics, @"[\s]+$", "");
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowLyrics (MainActivity): RegEx work done!");
            songLyrics.Text = downloadedLyrics;
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "GetAndShowLyrics (MainActivity): Showing lyrics");
        }

        private async Task ReadFromFile()
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "ReadFromFile (MainActivity): Started ReadFromFile method");

            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, GlobalMethods.savedLyricsLocation + songInfo.artist + savedSeparator + songInfo.title + ".txt");
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);
            ImageButton coverView = FindViewById<ImageButton>(Resource.Id.coverView);
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            ImageView headerView = FindViewById<ImageView>(Resource.Id.headerView);
            ImageView shadowView = FindViewById<ImageView>(Resource.Id.shadowView);
            ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);

            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);

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

                var coverPath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path + "/" + GlobalMethods.savedImagesLocation, GetLine(loadedInfo, 2) + savedSeparator + GetLine(loadedInfo, 1) + "-cover.jpg");
                var headerPath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path + "/" + GlobalMethods.savedImagesLocation, GetLine(loadedInfo, 2) + savedSeparator + GetLine(loadedInfo, 1) + "-header.jpg");

                ImageService.Instance.LoadFile(coverPath).Transform(new RoundedTransformation(coverRadius)).Into(coverView);
                if (File.Exists(headerPath))
                {
                    ImageService.Instance.LoadFile(headerPath).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                    ImageService.Instance.LoadFile(headerPath).Transform(new CropTransformation(3, 0, 0)).Transform(new BlurredTransformation(searchBlur)).Transform(new BlurredTransformation(searchBlur)).Into(searchView);
                }
                else
                {
                    ImageService.Instance.LoadFile(coverPath).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                    ImageService.Instance.LoadFile(coverPath).Transform(new CropTransformation(3, 0, 0)).Transform(new BlurredTransformation(searchBlur)).Transform(new BlurredTransformation(searchBlur)).Into(searchView);
                }

                shadowView.Visibility = ViewStates.Visible;
                lyricsLoadingWheel.Visibility = ViewStates.Gone;
                savedView.Visibility = ViewStates.Visible;
                refreshLayout.Refreshing = false;
            }
        }

        private async Task SaveSongLyrics()
        {
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, GlobalMethods.savedLyricsLocation);
            var pathImg = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, GlobalMethods.savedImagesLocation);

            await GlobalMethods.CheckIfAppFolderExists();

            path = Path.Combine(path, songInfo.artist + savedSeparator + songInfo.title + ".txt");
            var pathHeader = Path.Combine(pathImg, songInfo.artist + savedSeparator + songInfo.title + "-header.jpg");
            var pathCover = Path.Combine(pathImg, songInfo.artist + savedSeparator + songInfo.title + "-cover.jpg");

            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SaveSongLyrics (MainActivity): File doesn't exist, creating" + path.ToString());
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

                if (prefs.GetBoolean("save_header", true))
                {
                    using (var fileStream = File.Create(pathHeader))
                    {
                        Stream header = await ImageService.Instance.LoadUrl(songInfo.header).AsJPGStreamAsync();
                        header.Seek(0, SeekOrigin.Begin);
                        header.CopyTo(fileStream);
                    }
                }

                using (var fileStream = File.Create(pathCover))
                {
                    Stream cover = await ImageService.Instance.LoadUrl(songInfo.cover).AsJPGStreamAsync();
                    cover.Seek(0, SeekOrigin.Begin);
                    cover.CopyTo(fileStream);
                }

                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                var snackbar = Snackbar.Make(layout, Resource.String.savedSuccessfully, Snackbar.LengthLong);
                snackbar.Show();
            }
            else
            {
                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                var snackbar = Snackbar.Make(layout, Resource.String.alreadySaved, Snackbar.LengthLong);
                snackbar.Show();
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "SaveSongLyrics (MainActivity): File already exists, let's do nothing!");
            }
        }

        private async Task CheckAndSetPermissions(string permission)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "checkAndSetPermissions (MainActivity): Started checkAndSetPermissions method");

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    if (ContextCompat.CheckSelfPermission(this, permission) == (int)Android.Content.PM.Permission.Granted)
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "checkAndSetPermissions (MainActivity): Permission for" + permission + " already granted");

                        if (permission == Manifest.Permission.ReadExternalStorage)
                        {
                            await ReadFromFile();
                        }
                        else
                        {
                            await SaveSongLyrics();
                        }
                    }
                    else
                    {
                        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Storage))
                        {
                            Log.WriteLine(LogPriority.Info, "SmartLyrics", "checkAndSetPermissions (MainActivity): Rationale needed, trying to get permission...");

                            ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
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
                            Log.WriteLine(LogPriority.Info, "SmartLyrics", "checkAndSetPermissions (MainActivity): No need to ask user, trying to get permission...");

                            string[] p = { permission };
                            ActivityCompat.RequestPermissions(this, p, 1);
                        }
                    }
                }
                else
                {
                    if (permission == Manifest.Permission.ReadExternalStorage)
                    {
                        await ReadFromFile();
                    }
                    else
                    {
                        await SaveSongLyrics();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "checkAndSetPermissions (MainActivity): Exception caught! " + ex.Message);
            }
        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnRequestPermissionsResult (MainActivity): Permission: " + permissions[0] + " | Result: " + grantResults[0].ToString());

            if (permissions[0] == Manifest.Permission.WriteExternalStorage && grantResults[0] == Android.Content.PM.Permission.Granted)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnRequestPermissionsResult (MainActivity): Write permission granted!");
                await SaveSongLyrics();
            }
            else if (permissions[0] == Manifest.Permission.ReadExternalStorage && grantResults[0] == Android.Content.PM.Permission.Granted)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnRequestPermissionsResult (MainActivity): Read permission granted!");
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "OnRequestPermissionsResult (MainActivity): File " + Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, GlobalMethods.savedLyricsLocation + songInfo.artist + savedSeparator + songInfo.title + ".txt").ToString() + " for song exists, loading...");
                await ReadFromFile();
            }
            else if (grantResults[0] == Android.Content.PM.Permission.Denied || grantResults[1] == Android.Content.PM.Permission.Denied)
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "OnRequestPermissionsResult (MainActivity): Permission denied");

                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                var snackbar = Snackbar.Make(layout, Resource.String.permissionDenied, Snackbar.LengthLong);
                snackbar.Show();
            }
        }

        public override void OnBackPressed()
        {
            ConstraintLayout welcomeScreen = FindViewById<ConstraintLayout>(Resource.Id.welcomeScreen);
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);

            if (searchTxt.Visibility == ViewStates.Visible)
            {
                headerTxt.Visibility = ViewStates.Visible;
                searchTxt.Visibility = ViewStates.Gone;
                searchResults.Visibility = ViewStates.Gone;

                if (lastView == "welcome")
                {
                    welcomeScreen.Visibility = ViewStates.Visible;
                }
                else
                {
                    refreshLayout.Visibility = ViewStates.Visible;
                }
            }
            else
            {
                base.OnBackPressed();
            }
        }
    }
}
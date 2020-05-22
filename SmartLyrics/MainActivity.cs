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
using Android.Text;
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
using SmartLyrics.Common;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using static SmartLyrics.Globals;

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

        readonly int coverRadius = 16;
        readonly int headerBlur = 25;
        readonly int searchBlur = 25;

        string lastView;
        string lastSearch = "";

        //! used to alert a method that called CheckAndSetPermissions that the user
        //! made their decision
        //----------------------------
        //! index 1 is the status of the permission (true = arrived, false = didn't arrive)
        //! index 2 is the result (true = granted, false = denied) 
        bool[] permissionGranted =  new bool[2] { false, false};

        ISharedPreferences prefs;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main);
            CrossCurrentActivity.Current.Activity = this; //don't remove this, permission stuff needs it
            CrossCurrentActivity.Current.Init(this, savedInstanceState);

            AppCenter.Start("b07a2f8e-5d02-4516-aadc-2cba2c27fcf8",
                   typeof(Analytics), typeof(Crashes));

            //initialize UI variables
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
            //--UI-

            prefs = PreferenceManager.GetDefaultSharedPreferences(this);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "OnCreate (MainActivity): Loaded view");

            if (checkOnStart)
            {
                npTxt.Visibility = ViewStates.Visible;
                ChangeNotificationToInfo();
                await LoadSong();
            }

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
                    await LoadSong();
                    refreshLayout.Refreshing = false;
                }
            };

            drawerBtn.Click += delegate
            {
                drawer.OpenDrawer(navigationView);
            };

            gotoLyricsBtn.Click += delegate
            {
                var intent = new Intent(this, typeof(SavedLyrics)).SetFlags(ActivityFlags.ReorderToFront);
                StartActivity(intent);
            };

            coverView.Click += async delegate { await SaveButton_Action(); };
            searchBtn.Click += async delegate { await SearchButton_Action(); };
            searchTxt.EditorAction += async delegate { await SearchKeyboardButton_Action(); };
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

        private async Task SearchButton_Action()
        {
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            ConstraintLayout welcomeScreen = FindViewById<ConstraintLayout>(Resource.Id.welcomeScreen);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);

            InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);

            if (searchTxt.Visibility == ViewStates.Visible)
            {
                await SearchButton_Action();
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

        private async Task SearchKeyboardButton_Action()
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
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "SearchButton_Action (MainActivity): Started 'await GetAndShowSearchResults' task");
                imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);
                await GetAndShowSearchResults();
                searchLoadingWheel.Visibility = ViewStates.Gone;
            }
        }

        private async Task SaveButton_Action()
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SaveSong (MainActivity): Clicked on save button");

            //check for write permissions
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SaveSong (MainActivity): Checking write permission...");
            var permission = Manifest.Permission.WriteExternalStorage;
            int permissionStatus = await CheckAndSetPermissions(permission);

            ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
            Snackbar snackbar;

            switch (permissionStatus)
            {
                case 0:
                    await SaveSong();
                    break;

                case 1:
                    while (!permissionGranted[0])
                    {
                        await Task.Delay(200);
                        Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SaveButton_Action (MainActivity): Waiting permission status...");
                    }

                    //reset permission marker
                    permissionGranted[0] = false;

                    if (permissionGranted[1])
                    {
                        await SaveSong();
                    }
                    else
                    {
                        snackbar = Snackbar.Make(layout, Resource.String.permissionDenied, Snackbar.LengthLong);
                        snackbar.Show();
                    }
                    break;

                case 2:
                    snackbar = Snackbar.Make(layout, Resource.String.saveError, Snackbar.LengthLong);
                    snackbar.Show();

                    Log.WriteLine(LogPriority.Error, "SmartLyrics", "SaveButton_Action (MainActivity): An error occured while getting permission!");
                    break;
            }
        }

        async void searchResults_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            //initialize UI variables
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            TextView npTxt = FindViewById<TextView>(Resource.Id.npTxt);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);

            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);
            //--UI--

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "searchResults_ItemClick (MainActivity): Attempting to display song...");

            InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);
            imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);

            songInfo.id = resultsToView.ElementAt(e.Position).id;
            songInfo.title = resultsToView.ElementAt(e.Position).title;
            songInfo.artist = resultsToView.ElementAt(e.Position).artist;
            songInfo.header = resultsToView.ElementAt(e.Position).header;
            songInfo.cover = resultsToView.ElementAt(e.Position).cover;
            songInfo.APIPath = resultsToView.ElementAt(e.Position).APIPath;
            songInfo.path = resultsToView.ElementAt(e.Position).path;
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "searchResults_ItemClick (MainActivity): Added info to class");

            lyricsLoadingWheel.Visibility = ViewStates.Visible;
            npTxt.Visibility = ViewStates.Gone;
            searchResults.Visibility = ViewStates.Gone;

            songLyrics.Text = "";
            songTitle.Text = "";
            songArtist.Text = "";
            songAlbum.Text = "";
            songFeat.Text = "";

            await LoadSong();
        }

        void NavigationView_NavigationViewSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);

            e.MenuItem.SetCheckable(true);
            _ = new Intent(this, typeof(SavedLyrics)).SetFlags(ActivityFlags.ReorderToFront);
            Intent intent;
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
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "GetAndShowSearchResults (MainActivity): Started GetAndShowSearchResults method");

            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowSearchResults (MainActivity): Starting GetSearchResults operation");
            string results = await Genius.GetSearchResults(searchTxt.Text, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowSearchResults (MainActivity): Parsed results into list");

            resultsToView = new List<Song>();
            foreach (JToken result in parsedList)
            {
                Song song = new Song()
                {
                    id = (int)result["result"]["id"],
                    title = (string)result["result"]["title"],
                    artist = (string)result["result"]["primary_artist"]["name"],
                    cover = (string)result["result"]["song_art_image_thumbnail_url"],
                    header = (string)result["result"]["header_image_url"],
                    APIPath = (string)result["result"]["api_path"],
                    path = (string)result["result"]["path"]
                };

                resultsToView.Add(song);
            }
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowSearchResults (MainActivity): Created results list for listVew");

            var adapter = new SearchResultAdapter(this, resultsToView);
            searchResults.Adapter = adapter;

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "GetAndShowSearchResults (MainActivity): Added results to activity view");
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

        //handles all song loading by itself. can be called at anytime
        //if songInfo contains a song path
        private async Task LoadSong()
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "LoadSong (MainActivity): Started LoadSong method");

            checkOnStart = false;

            //initialize UI variables
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
            //--UI--

            //check if song is downloaded
            if (await DatabaseHandling.GetSongFromTable(songInfo.id) == null)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "LoadSong (MainActivity): File for song doesn't exist, getting data from Genius...");

                songTitle.Text = songInfo.title;
                songArtist.Text = songInfo.artist;
                savedView.Visibility = ViewStates.Gone;
                shadowView.Visibility = ViewStates.Visible;
                ImageService.Instance.LoadUrl(songInfo.cover).Transform(new RoundedTransformation(coverRadius)).Into(coverView);
                ImageService.Instance.LoadUrl(songInfo.header).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                ImageService.Instance.LoadUrl(songInfo.header).Transform(new CropTransformation(3, 0, 0)).Transform(new BlurredTransformation(searchBlur)).Transform(new BlurredTransformation(searchBlur)).Into(searchView);

                GetAndShowSongDetails();
                try
                {
                    await GetAndShowLyrics();
                }
                catch (NullReferenceException ex)
                {
                    Crashes.TrackError(ex);
                    Log.WriteLine(LogPriority.Error, "SmartLyrics", "LoadSong (MainActivity): NullReferenceException while getting lyrics");
                }
            }
            else
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "LoadSong (MainActivity): File for song exists, starting CheckAndSetPermissions...");

                var permission = Manifest.Permission.ReadExternalStorage;
                int permissionStatus = await CheckAndSetPermissions(permission);

                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                Snackbar snackbar;

                switch (permissionStatus)
                {
                    case 0:
                        await ReadFromFile();
                        break;
                    case 1:
                        while (!permissionGranted[0])
                        {
                            await Task.Delay(200);
                            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SaveButton_Action (MainActivity): Waiting permission status...");
                        }

                        //reset permission marker
                        permissionGranted[0] = false;

                        if (permissionGranted[1])
                        {
                            await ReadFromFile();
                        }
                        else
                        {
                            snackbar = Snackbar.Make(layout, Resource.String.permissionDenied, Snackbar.LengthLong);
                            snackbar.Show();
                        }
                        break;
                    case 2:
                    {
                        snackbar = Snackbar.Make(layout, Resource.String.readError, Snackbar.LengthLong);
                        snackbar.Show();

                        Log.WriteLine(LogPriority.Error, "SmartLyrics", "LoadSong (MainActivity): An error occured while requesting permission!");
                        break;
                    }
                }
            }
        }

        private async Task GetAndShowSongDetails()
        {
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "GetAndShowSongDetails (MainActivity): Starting GetSongDetails operation");
            string results = await Genius.GetSongDetails(songInfo.APIPath, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowSongDetails (MainActivity): Results parsed into JObject");

            songInfo.id = (int)parsed["response"]["song"]["id"];

            songInfo.album = (string)parsed["response"]["song"]["album"]["name"];
            songAlbum.Text = songInfo.album;
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "GetAndShowSongDetails (MainActivity): Displaying album name");

            if (parsed["response"]["song"]["featured_artists"].HasValues)
            {
                songFeat.Visibility = ViewStates.Visible;
                IList<JToken> parsedList = parsed["response"]["song"]["featured_artists"].Children().ToList();
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowSongDetails (MainActivity): Featured artists parsed list created");

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
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "GetAndShowSongDetails (MainActivity): Displaying featured artists");
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

            //initialize UI variables
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            //--UI--

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = await web.LoadFromWebAsync("https://genius.com" + songInfo.path);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowLyrics (MainActivity): Loaded Genius page");

            songInfo.lyrics = await HTMLParsing.ParseHTML(doc);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GetAndShowLyrics (MainActivity): Parsed HTML");

            lyricsLoadingWheel.Visibility = ViewStates.Gone;

            songLyrics.TextFormatted = Html.FromHtml(songInfo.lyrics, FromHtmlOptions.ModeCompact);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "GetAndShowLyrics (MainActivity): Showing lyrics");
            refreshLayout.Refreshing = false;

            savedView.Visibility = ViewStates.Gone;
        }

        private async Task ReadFromFile()
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "ReadFromFile (MainActivity): Started ReadFromFile method");
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation + songInfo.id + lyricsExtension);

            //initialize UI variables
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
            //--UI--

            using (StreamReader sr = File.OpenText(path))
            {
                //songInfo is already loaded, load lyrics and images from disk
                string loadedLyrics = await sr.ReadToEndAsync();
                songLyrics.TextFormatted = Html.FromHtml(loadedLyrics, FromHtmlOptions.ModeCompact);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "ReadFromFile (MainActivity): Displayed lyrics");

                string coverPath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path + "/" + savedImagesLocation, songInfo.id + "-cover.jpg");
                string headerPath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path + "/" + savedImagesLocation, songInfo.id + "-header.jpg");

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

                songTitle.Text = songInfo.title;
                songArtist.Text = songInfo.artist;
                songAlbum.Text = songInfo.album;
                if (songInfo.featuredArtist == "")
                {
                    songFeat.Visibility = ViewStates.Gone;
                }
                else
                {
                    songFeat.Visibility = ViewStates.Visible;
                    songFeat.Text = songInfo.featuredArtist;
                }

                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "ReadFromFile (MainActivity): Displayed song info");

                shadowView.Visibility = ViewStates.Visible;
                lyricsLoadingWheel.Visibility = ViewStates.Gone;
                savedView.Visibility = ViewStates.Visible;
                refreshLayout.Refreshing = false;

                Log.WriteLine(LogPriority.Info, "SmartLyrics", "ReadFromFile (MainActivity): Done reading from file!");
            }
        }

        private async Task SaveSong()
        {
            string pathImg = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedImagesLocation);

            await CheckAndCreateAppFolders();

            //header and cover images are always saved on a separate folder with
            //the song's ID to identify it
            string pathHeader = Path.Combine(pathImg, songInfo.id + "-header.jpg");
            string pathCover = Path.Combine(pathImg, songInfo.id + "-cover.jpg");

            if (await DatabaseHandling.WriteInfoAndLyrics(songInfo))
            {
                //show Snackbar alerting user of success
                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                var snackbar = Snackbar.Make(layout, Resource.String.savedSuccessfully, Snackbar.LengthLong);
                snackbar.Show();
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "SaveSong (MainActivity): Song saved successfully! ");
            }
            else
            {
                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                var snackbar = Snackbar.Make(layout, Resource.String.alreadySaved, Snackbar.LengthLong);
                snackbar.Show();
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "SaveSong (MainActivity): Song already exists!");
            }

            //save header and cover images based on preferences
            if (prefs.GetBoolean("save_header", true))
            {
                using (var fileStream = File.Create(pathHeader))
                {
                    Stream header = await ImageService.Instance.LoadUrl(songInfo.header).AsJPGStreamAsync();
                    header.Seek(0, SeekOrigin.Begin);
                    header.CopyTo(fileStream);

                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "SaveSong (MainActivity): Saved header image.");
                }
            }

            using (var fileStream = File.Create(pathCover))
            {
                Stream cover = await ImageService.Instance.LoadUrl(songInfo.cover).AsJPGStreamAsync();
                cover.Seek(0, SeekOrigin.Begin);
                cover.CopyTo(fileStream);

                Log.WriteLine(LogPriority.Info, "SmartLyrics", "SaveSong (MainActivity): Saved cover image.");
            }

            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            savedView.Visibility = ViewStates.Visible;
        }

        //returns 0 if successfully granted, 1 if the code calling this method
        //needs to wait and 2 if an exception was cought
        private async Task<int> CheckAndSetPermissions(string permission)
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "checkAndSetPermissions (MainActivity): Started CheckAndSetPermissions method");

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    if (ContextCompat.CheckSelfPermission(this, permission) == (int)Android.Content.PM.Permission.Granted)
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "checkAndSetPermissions (MainActivity): Permission for" + permission + " already granted");
                        return 0;
                    }
                    else
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
                        return 1;
                    }
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "CheckAndSetPermissions (MainActivity): Exception caught! " + ex.Message);
                Crashes.TrackError(ex);

                return 2;
            }
        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "OnRequestPermissionsResult (MainActivity): Permission: " + permissions[0] + " | Result: " + grantResults[0].ToString());

            if (grantResults[0] == Android.Content.PM.Permission.Granted)
            {
                //set marker and then the result of the permission request
                permissionGranted[0] = true;
                permissionGranted[1] = true;
            }
            else if (grantResults[0] == Android.Content.PM.Permission.Denied || grantResults[1] == Android.Content.PM.Permission.Denied)
            {
                //set marker and then the result of the permission request
                permissionGranted[0] = true;
                permissionGranted[1] = false;
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
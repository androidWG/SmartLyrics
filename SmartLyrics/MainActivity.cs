using Android;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.Constraints;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
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
using SmartLyrics.Toolbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using static SmartLyrics.Globals;
using Exception = System.Exception;

namespace SmartLyrics
{
    [Activity(Label = "@string/app_name", MainLauncher = true, ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.Orientation, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private List<Song> resultsToView;
        public static Song songInfo;
        public static Song notificationSong;
        private Song previousNtfSong;

        public static bool fromFile = false;
        public static bool fromNotification = false;
        private readonly int coverRadius = 16;
        private readonly int headerBlur = 25;
        private readonly int searchBlur = 25;

        private Timer checkTimer;
        private bool nowPlayingMode = false;
        private bool shouldCheck = false;
        private ISharedPreferences prefs;
        private string lastView = "";
        private string lastSearch = "";

        //! This variable keeps track of multiple simultaneous searches
        //! that happen while you're typing. This list contains no relevant content.
        //! The GetAndShowSearchResults method just uses it to compare the size of
        //! the list when the method started and when results are ready to show up.
        //? There's almost definitely a better way of keeping track of the newest
        //? search then the size of a list (int variable for example).
        private readonly List<string> t = new List<string>();

        //! Used to alert a method that called PermissionChecking.CheckAndSetPermissions
        //! that the user made their decision
        //----------------------------
        //! Index 0 is the status of the permission (true = arrived, false = didn't arrive)
        //! Index 1 is the result (true = granted, false = denied)
        //? There's 100% totally a better way to do this but I'm lazy
        //  Same on any activity that asks for permissions
        private readonly bool[] permissionGranted = new bool[2] { false, false };
        TextView npTxt;
        ImageView shineView;
        TextView noResultsTxt;
        TextView faceTxt;
        ConstraintLayout welcomeScreen;
        SwipeRefreshLayout refreshLayout;
        EditText searchTxt;

        #region Standard Activity Shit
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main);
            CrossCurrentActivity.Current.Activity = this; //don't remove this, permission stuff needs it
            CrossCurrentActivity.Current.Init(this, savedInstanceState);
            Log.WriteLine(LogPriority.Info, "MainActivity", "OnCreate: Loaded view");

            //Startup error handling
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            AppCenter.Start("b07a2f8e-5d02-4516-aadc-2cba2c27fcf8",
                   typeof(Analytics), typeof(Crashes)); //TODO: add Event Trackers

            #region UI Variables
            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;

            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            ImageButton searchBtn = FindViewById<ImageButton>(Resource.Id.searchBtn);
            ImageButton drawerBtn = FindViewById<ImageButton>(Resource.Id.drawerBtn);
            ImageButton coverView = FindViewById<ImageButton>(Resource.Id.coverView);
            Button gotoLyricsBtn = FindViewById<Button>(Resource.Id.gotoLyricsBtn);

            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);

            npTxt = FindViewById<TextView>(Resource.Id.npTxt);
            shineView = FindViewById<ImageView>(Resource.Id.shineView);
            faceTxt = FindViewById<TextView>(Resource.Id.faceTxt);
            noResultsTxt = FindViewById<TextView>(Resource.Id.noResultsTxt);
            refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            welcomeScreen = FindViewById<ConstraintLayout>(Resource.Id.welcomeScreen);
            searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);

            noResultsTxt.Visibility = ViewStates.Invisible;
            faceTxt.Visibility = ViewStates.Invisible;
            #endregion

            //Load preferences
            prefs = AndroidX.Preference.PreferenceManager.GetDefaultSharedPreferences(this);

            InitTimer();

            #region Event Subscriptions
            refreshLayout.Refresh += async delegate
            {
                Log.WriteLine(LogPriority.Verbose, "MainActivity", "refreshLayout.Refresh: Refreshing song...");
                if (!nowPlayingMode && notificationSong != previousNtfSong)
                {
                    songInfo = notificationSong;
                    previousNtfSong = notificationSong;
                    nowPlayingMode = true;

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
                Intent intent = new Intent(this, typeof(SavedLyrics)).SetFlags(ActivityFlags.ReorderToFront);
                StartActivity(intent);
            };

            searchTxt.TextChanged += async delegate
            {
                if (searchTxt.Text == "")
                {
                    t.Clear();
                    Log.WriteLine(LogPriority.Info, "MainActivity", "OnCreate: Cleared Task list");
                }

                t.Add("wow taskk");
                await SearchKeyboardButton_Action(false, t.Count);
            };

            coverView.Click += async delegate { await SaveButton_Action(); };
            searchBtn.Click += async delegate { SearchButton_Action(); };
            searchResults.ItemClick += SearchResuls_ItemClick;
            #endregion
        }

        protected override async void OnResume()
        {
            base.OnResume();
            Log.WriteLine(LogPriority.Verbose, "MainActivity", "OnResume: OnResume started");

            if (fromFile)
            {
                Log.WriteLine(LogPriority.Info, "MainActivity", "OnResume: Trying to load song from file");

                nowPlayingMode = false;
                fromFile = false;
                await LoadSong();
            }
            else if (fromNotification && songInfo == null)
            {
                Log.WriteLine(LogPriority.Info, "MainActivity", "OnResume: From notification, attempting to load");
                NotificationManagerCompat ntfManager = NotificationManagerCompat.From(this);
                ntfManager.CancelAll();

                RunOnUiThread(() =>
                {
                    welcomeScreen.Visibility = ViewStates.Gone;
                    refreshLayout.Visibility = ViewStates.Visible;
                });

                songInfo = notificationSong;
                previousNtfSong = notificationSong;

                shouldCheck = false;
                nowPlayingMode = true;
                fromNotification = false;
                await LoadSong();
            }
        }
        #endregion


        #region Button Actions
        private void SearchButton_Action()
        {
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);

            InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);

            if (searchTxt.Visibility == ViewStates.Visible && lastSearch == searchTxt.Text)
            {
                if (lastView == "welcome")
                {
                    welcomeScreen.Visibility = ViewStates.Visible;
                }
                else
                {
                    refreshLayout.Visibility = ViewStates.Visible;
                }

                headerTxt.Visibility = ViewStates.Visible;
                searchTxt.Visibility = ViewStates.Gone;
                searchResults.Visibility = ViewStates.Gone;

                noResultsTxt.Visibility = ViewStates.Invisible;
                faceTxt.Visibility = ViewStates.Invisible;

                //if (nowPlayingMode)
                //{
                //    npTxt.Visibility = ViewStates.Visible;
                //}
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
                npTxt.Visibility = ViewStates.Gone;

                searchTxt.RequestFocus();
                imm.ShowSoftInput(searchTxt, ShowFlags.Forced);
                imm.ToggleSoftInput(ShowFlags.Forced, HideSoftInputFlags.ImplicitOnly);
            }
        }

        private async Task SearchKeyboardButton_Action(bool hideKeyboard, int index)
        {
            #region UI Variables
            ProgressBar searchLoadingWheel = FindViewById<ProgressBar>(Resource.Id.searchLoadingWheel);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            #endregion

            if (searchTxt.Text != "")
            {
                if (hideKeyboard)
                {
                    InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);
                    imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);
                }

                lastSearch = searchTxt.Text;
                searchLoadingWheel.Visibility = ViewStates.Visible;
                searchResults.Visibility = ViewStates.Visible;

                noResultsTxt.Visibility = ViewStates.Invisible;
                faceTxt.Visibility = ViewStates.Invisible;

                Log.WriteLine(LogPriority.Verbose, "MainActivity", "SearchKeybaordButton_Action: Started search...");

                //Pass on index property to GetAndShowSearchResults to keep track of most recent query
                await GetAndShowSearchResults(searchTxt.Text, index);

                searchLoadingWheel.Visibility = ViewStates.Gone;
            }
        }

        private async Task SaveButton_Action()
        {
            Log.WriteLine(LogPriority.Verbose, "MainActivity", "SaveButton_Action: Clicked on save button");

            //Check for write permissions
            Log.WriteLine(LogPriority.Verbose, "MainActivity", "SaveButton_Action: Checking write permission...");
            string permission = Manifest.Permission.WriteExternalStorage;
            int permissionStatus = await PermissionChecking.CheckAndSetPermissions(permission, this);

            ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
            Snackbar snackbar;

            switch (permissionStatus)
            {
                case 0:
                    await SaveSong();
                    break;

                case 1:
                    string[] p = { permission };
                    snackbar = Snackbar.Make(layout, Resource.String.needStoragePermission, Snackbar.LengthIndefinite)
                        .SetAction(Android.Resource.String.Ok, new Action<View>(delegate (View obj)
                        {
                            ActivityCompat.RequestPermissions(this, p, 1);
                        }));
                    snackbar.Show();

                    while (!permissionGranted[0])
                    {
                        await Task.Delay(200);
                        Log.WriteLine(LogPriority.Verbose, "MainActivity", "SaveButton_Action: Waiting permission status...");
                    }

                    //Reset permission marker
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

                    Log.WriteLine(LogPriority.Error, "MainActivity", "SaveButton_Action: An error occured while getting permission!");
                    break;
            }
        }

        private async void SearchResuls_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            #region UI Variables
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            #endregion

            Log.WriteLine(LogPriority.Info, "MainActivity", $"SearchResuls_ItemClick: Attempting to display song at position {e.Position}.");

            InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);
            imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);

            nowPlayingMode = false;
            UpdateSong(false, true);

            songInfo = new Song(); //Clear variable and initialize it incase it's not initialized already

            songInfo.Id = resultsToView.ElementAt(e.Position).Id;
            songInfo.Title = resultsToView.ElementAt(e.Position).Title;
            songInfo.Artist = resultsToView.ElementAt(e.Position).Artist;
            songInfo.Header = resultsToView.ElementAt(e.Position).Header;
            songInfo.Cover = resultsToView.ElementAt(e.Position).Cover;
            songInfo.APIPath = resultsToView.ElementAt(e.Position).APIPath;
            songInfo.Path = resultsToView.ElementAt(e.Position).Path;
            Log.WriteLine(LogPriority.Verbose, "MainActivity", "SearchResuls_ItemClick: Added info to class");

            lyricsLoadingWheel.Visibility = ViewStates.Visible;
            searchResults.Visibility = ViewStates.Gone;

            await LoadSong();
        }

        private void NavigationView_NavigationViewSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
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
                    intent = new Intent(this, typeof(SettingsActivity)).SetFlags(ActivityFlags.ReorderToFront);
                    StartActivity(intent);
                    break;
            }

            e.MenuItem.SetCheckable(false);
            drawer.CloseDrawers();
        }
        #endregion


        #region Search
        private async Task GetAndShowSearchResults(string query, int index)
        {
            #region UI Variables
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            #endregion

            Log.WriteLine(LogPriority.Info, "MainActivity", $"GetAndShowSearchResults: Started GetAndShowSearchResults method of index {index}, query {query}");

            Log.WriteLine(LogPriority.Verbose, "MainActivity", "GetAndShowSearchResults: Starting GetSearchResults operation");
            string results = await HTTPRequests.GetRequest(geniusSearchURL + Uri.EscapeUriString(query), geniusAuthHeader);
            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Log.WriteLine(LogPriority.Verbose, "MainActivity", "GetAndShowSearchResults: Parsed results into list");

            List<Song> resultsList = new List<Song>();
            if (parsedList.Count != 0)
            {
                resultsList = new List<Song>();
                foreach (JToken result in parsedList)
                {
                    Song song = new Song()
                    {
                        Id = (int)result["result"]["id"],
                        Title = (string)result["result"]["title"],
                        Artist = (string)result["result"]["primary_artist"]["name"],
                        Cover = (string)result["result"]["song_art_image_thumbnail_url"],
                        Header = (string)result["result"]["header_image_url"],
                        APIPath = (string)result["result"]["api_path"],
                        Path = (string)result["result"]["path"]
                    };

                    resultsList.Add(song);
                }
                Log.WriteLine(LogPriority.Verbose, "MainActivity", "GetAndShowSearchResults: Created results list for listVew");

                if (index == t.Count)
                {
                    SearchResultAdapter adapter = new SearchResultAdapter(this, resultsList);
                    searchResults.Adapter = adapter;

                    resultsToView = resultsList;

                    Log.WriteLine(LogPriority.Verbose, "MainActivity", $"GetAndShowSearchResults: Added results of {index}, query {query} to activity view at count {t.Count}");
                }
                else
                {
                    Log.WriteLine(LogPriority.Verbose, "MainActivity", $"GetAndShowSearchResults: Results of index {index}, query {query} is smaller than Task list of size {t.Count}");
                }
            }
            else
            {
                resultsList = new List<Song>();
                SearchResultAdapter adapter = new SearchResultAdapter(this, resultsList);
                searchResults.Adapter = adapter;

                resultsToView = resultsList;

                noResultsTxt.Visibility = ViewStates.Visible;
                faceTxt.Visibility = ViewStates.Visible;
                Log.WriteLine(LogPriority.Warn, "MainActivity", "GetAndShowSearchResults: No results found!");
            }
        }
        #endregion


        private async void CheckIfSongIsPlaying()
        {
            if (shouldCheck && MiscTools.IsInForeground() && !fromNotification) //Checks for the user coming from outside the app are made on OnResume method
            {
                if (notificationSong != previousNtfSong)
                {
                    Log.WriteLine(LogPriority.Info, "MainActivity", "CheckIfSongIsPlaying: Song playing is different, updating...");

                    nowPlayingMode = true;
                    songInfo = notificationSong;
                    previousNtfSong = notificationSong;

                    bool autoUpdate = prefs.GetBoolean("auto_update_page", false);

                    if (autoUpdate && nowPlayingMode)
                    {
                        shouldCheck = false;
                        await LoadSong();
                    }
                    else if (nowPlayingMode)
                    {
                        shouldCheck = false;

                        Android.Views.Animations.Animation anim = Animations.BlinkingAnimation(700, 3);
                        npTxt.StartAnimation(anim);
                        Log.WriteLine(LogPriority.Info, "MainActivity", "CheckIfSongIsPlaying: Playing animation on npTxt");
                    }
                    else if (!nowPlayingMode)
                    {
                        shouldCheck = false;

                        Android.Views.Animations.Animation anim = Animations.BlinkingImageAnimation(500, 4);
                        shineView.Visibility = ViewStates.Visible;
                        shineView.StartAnimation(anim);
                        Log.WriteLine(LogPriority.Info, "MainActivity", "CheckIfSongIsPlaying: Playing animation on shineView");
                    }
                }
                //else if (notificationSong == songInfo && !nowPlayingMode)
                //{
                //    Log.WriteLine(LogPriority.Verbose, "MainActivity", "CheckIfSongIsPlaying: Song playing is the same as the one being shown");
                //    nowPlayingMode = true;
                //}
            }
        }

        public void InitTimer()
        {
            checkTimer = new Timer();
            checkTimer.Elapsed += new ElapsedEventHandler(CheckTimer_Tick);
            checkTimer.Interval = 1000; // in miliseconds
            checkTimer.Start();
        }

        private void CheckTimer_Tick(object sender, EventArgs e)
        {
            CheckIfSongIsPlaying();
            
            if (nowPlayingMode/* && searchTxt.Visibility != ViewStates.Visible*/)
            {
                npTxt.Visibility = ViewStates.Visible;
            }
            else
            {
                npTxt.Visibility = ViewStates.Gone;
            }

            if (searchTxt.Visibility == ViewStates.Visible)
            {

            }
        }


        #region Load from Internet
        private async Task GetAndShowSongDetails()
        {
            Log.WriteLine(LogPriority.Info, "MainActivity", "GetAndShowSongDetails: Starting GetSongDetails operation");
            string results = await HTTPRequests.GetRequest(geniusAPIURL + songInfo.APIPath, geniusAuthHeader);
            JObject parsed = JObject.Parse(results);
            Log.WriteLine(LogPriority.Verbose, "MainActivity", "GetAndShowSongDetails: Results parsed into JObject");

            songInfo.Title = (string)parsed["response"]["song"]["title"];
            songInfo.Artist = (string)parsed["response"]["song"]["primary_artist"]["name"];
            songInfo.Album = (string)parsed["response"]["song"]["album"]["name"];
            songInfo.Header = (string)parsed["response"]["song"]["header_image_url"];
            songInfo.Cover = (string)parsed["response"]["song"]["song_art_image_url"];
            songInfo.APIPath = (string)parsed["response"]["song"]["api_path"];
            songInfo.Path = (string)parsed["response"]["song"]["path"];

            if (parsed["response"]["song"]["featured_artists"].HasValues)
            {
                IList<JToken> parsedList = parsed["response"]["song"]["featured_artists"].Children().ToList();

                songInfo.FeaturedArtist = "feat. ";
                foreach (JToken artist in parsedList)
                {
                    if (songInfo.FeaturedArtist == "feat. ")
                    {
                        songInfo.FeaturedArtist += artist["name"].ToString();
                    }
                    else
                    {
                        songInfo.FeaturedArtist += ", " + artist["name"].ToString();
                    }
                }

                Log.WriteLine(LogPriority.Info, "MainActivity", "GetAndShowSongDetails: Added featured artists to songInfo");
            }
            else
            {
                songInfo.FeaturedArtist = "";
            }

            //Exceute all Japanese transliteration tasks at once
            if (songInfo.Title.ContainsJapanese() || songInfo.Artist.ContainsJapanese())
            {
                Task<string> awaitTitle = songInfo.Title.StripJapanese();
                Task<string> awaitArtist = songInfo.Artist.StripJapanese();
                Task<string> awaitAlbum = songInfo.Album.StripJapanese();

                await Task.WhenAll(awaitTitle, awaitArtist, awaitAlbum);

                songInfo.Title = await awaitTitle;
                songInfo.Artist = await awaitArtist;
                songInfo.Album = await awaitAlbum;
            }

            UpdateSong(true, false);
        }

        private async Task GetAndShowLyrics()
        {
            Log.WriteLine(LogPriority.Info, "MainActivity", "GetAndShowLyrics: Started GetAndShowLyrics method");

            #region UI Variables
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView infoTxt = FindViewById<TextView>(Resource.Id.infoTxt);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            ImageButton fabMore = FindViewById<ImageButton>(Resource.Id.fabMore);
            #endregion

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = await web.LoadFromWebAsync("https://genius.com" + songInfo.Path);
            Log.WriteLine(LogPriority.Verbose, "MainActivity", "GetAndShowLyrics: Loaded Genius page");

            songInfo.Lyrics = await HTMLParsing.ParseHTML(doc);
            Log.WriteLine(LogPriority.Verbose, "MainActivity", "GetAndShowLyrics: Parsed HTML");

            lyricsLoadingWheel.Visibility = ViewStates.Gone;
            fabMore.Visibility = ViewStates.Visible;
            savedView.Visibility = ViewStates.Gone;
            infoTxt.Visibility = ViewStates.Visible;

            songLyrics.TextFormatted = Html.FromHtml(songInfo.Lyrics, FromHtmlOptions.ModeCompact);

            shouldCheck = true;
            refreshLayout.Refreshing = false;

            Log.WriteLine(LogPriority.Info, "MainActivity", "GetAndShowLyrics: Showing lyrics");
        }
        #endregion


        #region Song Loading
        //Handles all song loading by itself.
        //Can be called at anytime if songInfo contains a song path
        private async Task LoadSong()
        {
            Log.WriteLine(LogPriority.Info, "MainActivity", "LoadSong: Started LoadSong method");

            refreshLayout.Visibility = ViewStates.Visible;
            welcomeScreen.Visibility = ViewStates.Gone;

            #region UI Variables
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);

            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);

            refreshLayout.Visibility = ViewStates.Visible;
            searchTxt.Visibility = ViewStates.Gone;
            headerTxt.Visibility = ViewStates.Visible;
            welcomeScreen.Visibility = ViewStates.Gone;
            #endregion

            //Check if song is downloaded
            if (await DatabaseHandling.GetSongFromTable(songInfo.Id) == null)
            {
                Log.WriteLine(LogPriority.Info, "MainActivity", "LoadSong: File for song doesn't exist, getting data from APIRequests.Genius...");

                savedView.Visibility = ViewStates.Gone;

                GetAndShowSongDetails();
                try
                {
                    await GetAndShowLyrics();
                }
                catch (NullReferenceException ex)
                {
                    Crashes.TrackError(ex);
                    Log.WriteLine(LogPriority.Error, "MainActivity", "LoadSong: NullReferenceException while getting lyrics");
                }
            }
            else
            {
                Log.WriteLine(LogPriority.Info, "MainActivity", "LoadSong: File for song exists, starting CheckAndSetPermissions...");

                string permission = Manifest.Permission.ReadExternalStorage;
                int permissionStatus = await PermissionChecking.CheckAndSetPermissions(permission, this);

                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                Snackbar snackbar;

                switch (permissionStatus)
                {
                    case 0:
                        await ReadFromFile();
                        break;
                    case 1:
                        string[] p = { permission };
                        snackbar = Snackbar.Make(layout, Resource.String.needStoragePermission, Snackbar.LengthIndefinite)
                            .SetAction(Android.Resource.String.Ok, new Action<View>(delegate (View obj)
                            {
                                ActivityCompat.RequestPermissions(this, p, 1);
                            }));
                        snackbar.Show();

                        while (!permissionGranted[0])
                        {
                            await Task.Delay(200);
                            Log.WriteLine(LogPriority.Verbose, "MainActivity", "SaveButton_Action: Waiting permission status...");
                        }

                        //Reset permission marker
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

                        Log.WriteLine(LogPriority.Error, "MainActivity", "LoadSong: An error occured while requesting permission!");
                        break;
                    }
                }
            }
        }

        private async Task ReadFromFile()
        {
            Log.WriteLine(LogPriority.Info, "MainActivity", "ReadFromFile: Started ReadFromFile method");
            string path = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath, savedLyricsLocation + songInfo.Id + lyricsExtension);

            #region UI Variables
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            TextView infoTxt = FindViewById<TextView>(Resource.Id.infoTxt);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            ImageButton fabMore = FindViewById<ImageButton>(Resource.Id.fabMore);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            #endregion

            using (StreamReader sr = File.OpenText(path))
            {
                //songInfo is already loaded, load lyrics and images from disk
                //TODO: Add error handling
                songInfo.Lyrics = await sr.ReadToEndAsync();

                Log.WriteLine(LogPriority.Verbose, "MainActivity", "ReadFromFile: Read lyrics from file");
            }

            UpdateSong(true, false, true);

            lyricsLoadingWheel.Visibility = ViewStates.Gone;
            fabMore.Visibility = ViewStates.Visible;
            savedView.Visibility = ViewStates.Visible;
            infoTxt.Visibility = ViewStates.Visible;

            refreshLayout.Refreshing = false;
            shouldCheck = true;

            Log.WriteLine(LogPriority.Info, "MainActivity", "ReadFromFile: Done reading from file!");
        }

        private async Task SaveSong()
        {
            string pathImg = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath, savedImagesLocation);

            await MiscTools.CheckAndCreateAppFolders();

            //Header and cover images are always saved on a separate folder with
            //the song's ID to identify it.
            string pathHeader = Path.Combine(pathImg, songInfo.Id + "-header.jpg");
            string pathCover = Path.Combine(pathImg, songInfo.Id + "-cover.jpg");

            if (await DatabaseHandling.WriteInfoAndLyrics(songInfo))
            {
                //Show Snackbar alerting user of success
                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                Snackbar snackbar = Snackbar.Make(layout, Resource.String.savedSuccessfully, Snackbar.LengthLong);
                snackbar.Show();
                Log.WriteLine(LogPriority.Info, "MainActivity", "SaveSong: Song saved successfully! ");
            }
            else
            {
                ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                Snackbar snackbar = Snackbar.Make(layout, Resource.String.alreadySaved, Snackbar.LengthLong);
                snackbar.Show();
                Log.WriteLine(LogPriority.Warn, "MainActivity", "SaveSong: Song already exists!");
            }

            //Save header and cover images based on preferences
            if (prefs.GetBoolean("save_header", true))
            {
                using (FileStream fileStream = File.Create(pathHeader))
                {
                    Stream header = await ImageService.Instance.LoadUrl(songInfo.Header).AsJPGStreamAsync();
                    header.Seek(0, SeekOrigin.Begin);
                    header.CopyTo(fileStream);

                    Log.WriteLine(LogPriority.Info, "MainActivity", "SaveSong: Saved header image.");
                }
            }

            using (FileStream fileStream = File.Create(pathCover))
            {
                Stream cover = await ImageService.Instance.LoadUrl(songInfo.Cover).AsJPGStreamAsync();
                cover.Seek(0, SeekOrigin.Begin);
                cover.CopyTo(fileStream);

                Log.WriteLine(LogPriority.Info, "MainActivity", "SaveSong: Saved cover image.");
            }

            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            savedView.Visibility = ViewStates.Visible;
        }
        #endregion


        #region Tools
        private void UpdateSong(bool updateImages, bool clearLabels, bool imagesOnDisk = false)
        {
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);

            if (clearLabels)
            {
                songLyrics.Text = "";
                songTitle.Text = "";
                songArtist.Text = "";
                songAlbum.Text = "";
                songFeat.Text = "";
            }
            else
            {
                if (!string.IsNullOrEmpty(songInfo.Lyrics))
                {
                    songLyrics.TextFormatted = Html.FromHtml(songInfo.Lyrics, FromHtmlOptions.ModeCompact);
                }
                songTitle.Text = songInfo.Title;
                songArtist.Text = songInfo.Artist;
                songAlbum.Text = songInfo.Album;
                if (string.IsNullOrEmpty(songInfo.FeaturedArtist))
                {
                    songFeat.Visibility = ViewStates.Gone;
                }
                else
                {
                    songFeat.Visibility = ViewStates.Visible;
                    songFeat.Text = songInfo.FeaturedArtist;
                }

                Log.WriteLine(LogPriority.Verbose, "MainActivity", "UpdateSong: Updated labels");
            }

            if (updateImages)
            {
                ImageButton coverView = FindViewById<ImageButton>(Resource.Id.coverView);
                ImageView headerView = FindViewById<ImageView>(Resource.Id.headerView);
                ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);

                if (imagesOnDisk)
                {
                    string coverPath = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath + "/" + savedImagesLocation, songInfo.Id + "-cover.jpg");
                    string headerPath = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath + "/" + savedImagesLocation, songInfo.Id + "-header.jpg");

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

                    Log.WriteLine(LogPriority.Verbose, "MainActivity", "UpdateSong: Updated images from disk");
                }
                else
                {
                    Log.WriteLine(LogPriority.Verbose, "MainActivity", $"UpdateSong: Loading cover from {songInfo.Cover} and header from {songInfo.Header}");
                    ImageService.Instance.LoadUrl(songInfo.Cover).Transform(new RoundedTransformation(coverRadius)).Into(coverView);
                    ImageService.Instance.LoadUrl(songInfo.Header).Transform(new BlurredTransformation(headerBlur)).Into(headerView);
                    ImageService.Instance.LoadUrl(songInfo.Header).Transform(new CropTransformation(3, 0, 0)).Transform(new BlurredTransformation(searchBlur)).Transform(new BlurredTransformation(searchBlur)).Into(searchView);

                    Log.WriteLine(LogPriority.Verbose, "MainActivity", "UpdateSong: Updated images from the internet");
                }
            }

            Log.WriteLine(LogPriority.Info, "MainActivity", "UpdateSong: Finished updating from songInfo");
        }
        #endregion


        #region The stuff that's always on the bottom
        //Same on any activity that asks for permissions
        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            Log.WriteLine(LogPriority.Verbose, "MainActivity", "OnRequestPermissionsResult: Permission: " + permissions[0] + " | Result: " + grantResults[0].ToString());

            if (grantResults[0] == Android.Content.PM.Permission.Granted)
            {
                permissionGranted[0] = true;
                permissionGranted[1] = true;
            }
            else if (grantResults[0] == Android.Content.PM.Permission.Denied || grantResults[1] == Android.Content.PM.Permission.Denied)
            {
                permissionGranted[0] = true;
                permissionGranted[1] = false;
            }
        }

        public override void OnBackPressed()
        {
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
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
        #endregion


        #region Error Handling
        private static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
        {
            Exception newExc = new Exception("TaskSchedulerOnUnobservedTaskException", unobservedTaskExceptionEventArgs.Exception);
            LogUnhandledException(newExc);
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            Exception newExc = new Exception("CurrentDomainOnUnhandledException", unhandledExceptionEventArgs.ExceptionObject as Exception);
            LogUnhandledException(newExc);
        }

        internal static void LogUnhandledException(Exception exception)
        {
            try
            {
                string errorMessage = string.Format("Time: {0}\r\nError: Unhandled Exception\r\n{1}",
                        DateTime.Now, exception.ToString());

                System.Diagnostics.Debug.WriteLine(errorMessage);
                Log.WriteLine(LogPriority.Error, "Crash Report", errorMessage);
                Crashes.TrackError(exception);
            }
            catch
            {
                //Just suppress any error logging exceptions
            }
        }

        //TODO: Finish exception handling
        // If there is an unhandled exception, the exception information is diplayed 
        // on screen the next time the app is started (only in debug configuration)
        [Conditional("DEBUG")]
        private void DisplayCrashReport()
        {
            const string errorFilename = "Fatal.log";
            string libraryPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            string errorFilePath = Path.Combine(libraryPath, errorFilename);

            if (!File.Exists(errorFilePath))
            {
                return;
            }

            string errorText = File.ReadAllText(errorFilePath);
            new AndroidX.AppCompat.App.AlertDialog.Builder(this)
                .SetPositiveButton("Clear", (sender, args) =>
                {
                    File.Delete(errorFilePath);
                })
                .SetNegativeButton("Close", (sender, args) =>
                {
                    // User pressed Close.
                })
                .SetMessage(errorText)
                .SetTitle("Crash Report")
                .Show();
        }

        #endregion
    }
}
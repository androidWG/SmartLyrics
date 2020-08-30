using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.ConstraintLayout.Widget;

using FFImageLoading;
using FFImageLoading.Transformations;
using HtmlAgilityPack;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using static SmartLyrics.Globals;
using static SmartLyrics.Common.Logging;
using SmartLyrics.IO;
using SmartLyrics.Common;
using SmartLyrics.Toolbox;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using static System.Reflection.MethodBase;

using Exception = System.Exception;
using Type = SmartLyrics.Common.Logging.Type;

namespace SmartLyrics
{
    [Activity(Label = "@string/app_name", MainLauncher = true, ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.Orientation, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait, LaunchMode = Android.Content.PM.LaunchMode.SingleTop)]
    public class MainActivity : AppCompatActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private List<Song> resultsToView;
        public static SongBundle songInfo;
        public static Song notificationSong = new Song(); //TODO: Change to SongBundle
        private Song previousNtfSong = new Song();

        View welcomeView;
        View songView;

        public static bool fromNotification = false;
        private readonly int coverRadius = 16;
        private readonly int headerBlur = 25;
        private readonly int searchBlur = 25;

        private Timer checkTimer;
        private bool nowPlayingMode = false;
        private bool shouldCheck = false;
        private string lastSearch = "";

        //! This variable keeps track of multiple simultaneous searches
        //! that happen while you're typing. This list contains no relevant content.
        //! The GetAndShowSearchResults method just uses it to compare the size of
        //! the list when the method started and when results are ready to show up.
        //? There's almost definitely a better way of keeping track of the newest
        //? search then the size of a list (int variable for example).
        private readonly List<string> t = new List<string>();

        TextView npTxt;
        ImageView shineView;
        TextView noResultsTxt;
        TextView faceTxt;
        EditText searchTxt;
        FrameLayout dynamicLayout;

        InputMethodManager imm;

        #region Standard Activity Shit
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main);
            Log(Type.Info, "Loaded view");

            // Startup error handling
            //AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            //TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
            AppCenter.Start("b07a2f8e-5d02-4516-aadc-2cba2c27fcf8",
                   typeof(Analytics), typeof(Crashes)); //TODO: Add Event Trackers

            #region UI Variables
            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;

            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            ImageButton searchBtn = FindViewById<ImageButton>(Resource.Id.searchBtn);
            ImageButton drawerBtn = FindViewById<ImageButton>(Resource.Id.drawerBtn);

            npTxt = FindViewById<TextView>(Resource.Id.npTxt);
            faceTxt = FindViewById<TextView>(Resource.Id.faceTxt);
            noResultsTxt = FindViewById<TextView>(Resource.Id.noResultsTxt);
            searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);
            dynamicLayout = FindViewById<FrameLayout>(Resource.Id.dynamicLayout);

            noResultsTxt.Visibility = ViewStates.Invisible;
            faceTxt.Visibility = ViewStates.Invisible;

            imm = (InputMethodManager)GetSystemService(InputMethodService);
            #endregion

            // Load preferences into global variable
            prefs = AndroidX.Preference.PreferenceManager.GetDefaultSharedPreferences(this);

            // Inflate layouts
            if (!fromNotification) { InflateWelcome(); }
            else { InflateSong(); }

            InitTimer();

            //! Some event subscriptions happen on the InflateWelcome
            //! and InflateSong methods.
            #region Event Subscriptions
            searchTxt.TextChanged += async delegate
            {
                if (string.IsNullOrEmpty(searchTxt.Text))
                {
                    t.Clear();
                    Log(Type.Info, "Cleared Task list");
                }

                t.Add("wow taskk"); //Placeholder
                await SearchKeyboardButton_Action(false, t.Count);
            };

            searchResults.Touch += (s, e) =>
            {
                imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);
                e.Handled = false;
            };

            drawerBtn.Click += delegate { drawer.OpenDrawer(navigationView); };
            searchBtn.Click += async delegate { SearchButton_Action(); };
            searchResults.ItemClick += SearchResuls_ItemClick;
            #endregion
        }

        protected override async void OnResume()
        {
            base.OnResume();
            Log(Type.Info, "OnResume started");

            if (fromNotification)
            {
                Log(Type.Event, "Attempting to load song from notification");
                Analytics.TrackEvent("Opening song from notification", new Dictionary<string, string> {
                    { "NotificationSong", JsonConvert.SerializeObject(notificationSong) }
                });
                NotificationManagerCompat ntfManager = NotificationManagerCompat.From(this);
                ntfManager.CancelAll();

                songInfo.Normal = notificationSong;
                previousNtfSong = notificationSong;

                shouldCheck = false;
                nowPlayingMode = true;
                await LoadSong();
            }
        }

        protected override void OnStop()
        {
            base.OnStop();

            Log(Type.Info, "OnStop started");
            notificationSong = new Song();
            previousNtfSong = notificationSong;
        }

        protected override async void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            //This should be called when a notification is opened, but that's not happening
            //Probably because it's not a new intent from a lodaded activity, but a new one, you dumbass

            if (intent.HasExtra("SavedSong"))
            {
                SongBundle saved = JsonConvert.DeserializeObject<SongBundle>(intent.GetStringExtra("SavedSong"));
                songInfo = saved;

                Log(Type.Event, "Recieved SavedSongs intent, loading song from disk");
                if (prefs.GetBoolean("sendSongInfo", false))
                {
                    Analytics.TrackEvent("Recieved SavedSongs intent", new Dictionary<string, string> {
                        { "SongID", intent.GetStringExtra("SavedSong") }});
                }
                else
                {
                    Analytics.TrackEvent("Recieved SavedSongs intent");
                }

                await LoadSong();
            }

            Log(Type.Info, "OnNewIntent started");
        }
        #endregion


        #region Button Actions
        private void SearchButton_Action()
        {
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);

            if (searchTxt.Visibility == ViewStates.Visible && lastSearch == searchTxt.Text)
            {
                RunOnUiThread(() =>
                {
                    dynamicLayout.Visibility = ViewStates.Visible;

                    headerTxt.Visibility = ViewStates.Visible;
                    searchTxt.Visibility = ViewStates.Gone;
                    searchResults.Visibility = ViewStates.Gone;

                    noResultsTxt.Visibility = ViewStates.Invisible;
                    faceTxt.Visibility = ViewStates.Invisible;
                });

                imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);
                Log(Type.Action, "Hiding search screen");
                Analytics.TrackEvent("Hiding search screen");
            }
            else
            {
                RunOnUiThread(() =>
                {
                    dynamicLayout.Visibility = ViewStates.Gone;

                    searchResults.Visibility = ViewStates.Visible;
                    headerTxt.Visibility = ViewStates.Gone;
                    searchTxt.Visibility = ViewStates.Visible;

                    npTxt.Visibility = ViewStates.Gone;
                });

                searchTxt.RequestFocus();
                imm.ShowSoftInput(searchTxt, ShowFlags.Forced);
                imm.ToggleSoftInput(ShowFlags.Forced, HideSoftInputFlags.ImplicitOnly);

                Log(Type.Action, "Showing search screen");
                Analytics.TrackEvent("Showing search screen");
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
                if (hideKeyboard) { imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0); }
                string searchString = searchTxt.Text;

                lastSearch = searchString;
                searchLoadingWheel.Visibility = ViewStates.Visible;
                searchResults.Visibility = ViewStates.Visible;

                noResultsTxt.Visibility = ViewStates.Invisible;
                faceTxt.Visibility = ViewStates.Invisible;

                //Pass on index property to GetAndShowSearchResults to keep track of most recent query
                await GetAndShowSearchResults(searchString, index);
            }
        }

        private async void SearchResuls_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            if (welcomeView != null)
            {
                dynamicLayout.RemoveView(welcomeView);
                welcomeView = null;

                InflateSong();
            }

            dynamicLayout.Visibility = ViewStates.Visible;

            #region UI Variables
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            #endregion

            imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);
            nowPlayingMode = false;
            ClearLabels();

            if (songInfo == null) { songInfo = new SongBundle(new Song(), new RomanizedSong()); }
            songInfo.Normal = resultsToView.ElementAt(e.Position);

            Log(Type.Action, $"Attempting to display song at search position {e.Position}.");
            Analytics.TrackEvent("Attempting to display song from search", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() },
                    { "ListPosition", e.Position.ToString() }
                });

            RunOnUiThread(() =>
            {
                lyricsLoadingWheel.Visibility = ViewStates.Visible;
                searchResults.Visibility = ViewStates.Gone;
            });

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
            ProgressBar searchLoadingWheel = FindViewById<ProgressBar>(Resource.Id.searchLoadingWheel);
            #endregion

            Log(Type.Event, $"Started search of index {index}, with query {query}");

            string results = await HTTPRequests.GetRequest(geniusSearchURL + Uri.EscapeUriString(query), geniusAuthHeader);
            JObject parsed = JObject.Parse(results);

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();

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

                    if (song.Title.ContainsJapanese() && prefs.GetBoolean("romanize_search", false))
                    {
                        song.Title = await JapaneseTools.StripJapanese(song.Title);
                    }
                    if (song.Artist.ContainsJapanese() && prefs.GetBoolean("romanize_search", false))
                    {
                        song.Artist = await JapaneseTools.StripJapanese(song.Artist);
                    }

                    resultsList.Add(song);
                }

                if (index == t.Count)
                {
                    SearchResultAdapter adapter = new SearchResultAdapter(this, resultsList);
                    searchResults.Adapter = adapter;

                    resultsToView = resultsList;
                    searchLoadingWheel.Visibility = ViewStates.Gone;

                    Log(Type.Processing, $"Added results of {index} to activity view at count {t.Count}");
                }
                else
                {
                    Log(Type.Processing, $"Results of index {index} is smaller than Task list with size {t.Count}");
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
                Log(Type.Info, $"No results found for query {query}");
            }
        }
        #endregion


        #region Timer
        private async void CheckIfSongIsPlaying()
        {
            if (shouldCheck && MiscTools.IsInForeground() && !fromNotification) //Checks for the user coming from outside the app are made on OnResume method
            {
                if (notificationSong.Id != previousNtfSong.Id)
                {
                    Log(Type.Event, "Song playing is different, updating...");

                    nowPlayingMode = true;
                    songInfo.Normal = notificationSong;
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
                        Log(Type.Event, "Playing animation on npTxt");
                    }
                    else if (!nowPlayingMode)
                    {
                        shouldCheck = false;

                        Android.Views.Animations.Animation anim = Animations.BlinkingImageAnimation(500, 4);
                        shineView.Visibility = ViewStates.Visible;
                        shineView.StartAnimation(anim);
                        Log(Type.Event, "Playing animation on shineView");
                    }
                }
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
        }
        #endregion


        #region Load from Genius
        private async Task GetAndShowSongDetails()
        {
            Log(Type.Info, "Starting GetSongDetails operation");
            string results = await HTTPRequests.GetRequest(geniusAPIURL + songInfo.Normal.APIPath, geniusAuthHeader);

            JObject parsed = JObject.Parse(results);
            parsed = (JObject)parsed["response"]["song"]; //Change root to song

            Song fromJSON = new Song();
            fromJSON.Title = (string)parsed?.SelectToken("title") ?? "";
            fromJSON.Artist = (string)parsed?.SelectToken("primary_artist.name") ?? "";
            fromJSON.Album = (string)parsed?.SelectToken("album.name") ?? "";
            fromJSON.Header = (string)parsed?.SelectToken("header_image_url") ?? "";
            fromJSON.Cover = (string)parsed?.SelectToken("song_art_image_url") ?? "";
            fromJSON.APIPath = (string)parsed?.SelectToken("api_path") ?? "";
            fromJSON.Path = (string)parsed?.SelectToken("path") ?? "";

            songInfo.Normal = fromJSON;

            if (parsed["featured_artists"].HasValues)
            {
                IList<JToken> parsedList = parsed["featured_artists"].Children().ToList();

                songInfo.Normal.FeaturedArtist = "feat. ";
                foreach (JToken artist in parsedList)
                {
                    if (songInfo.Normal.FeaturedArtist == "feat. ")
                    { songInfo.Normal.FeaturedArtist += artist["name"].ToString(); }
                    else
                    { songInfo.Normal.FeaturedArtist += ", " + artist["name"].ToString(); }
                }

                Log(Type.Processing, "Added featured artists to songInfo");
            }
            else
            {
                songInfo.Normal.FeaturedArtist = "";
            }

            //Exceute all Japanese transliteration tasks at once
            if (prefs.GetBoolean("auto_romanize_details", true) && songInfo.Normal.Title.ContainsJapanese() || songInfo.Normal.Artist.ContainsJapanese() || songInfo.Normal.Album.ContainsJapanese())
            {
                Task<string> awaitTitle = songInfo.Normal.Title.StripJapanese();
                Task<string> awaitArtist = songInfo.Normal.Artist.StripJapanese();
                Task<string> awaitAlbum = songInfo.Normal.Album.StripJapanese();

                await Task.WhenAll(awaitTitle, awaitArtist, awaitAlbum);

                RomanizedSong romanized = new RomanizedSong();
                // This snippet is the same in GetAndShowLyrics
                if (songInfo.Romanized == null) { songInfo.Romanized = romanized; }

                romanized.Title = await awaitTitle;
                romanized.Artist = await awaitArtist;
                romanized.Album = await awaitAlbum;

                romanized.Id = songInfo.Normal.Id;
                songInfo.Romanized = romanized;
                songInfo.Normal.Romanized = true;

                Log(Type.Event, "Romanized song info with ID " + songInfo.Normal.Id);
                Analytics.TrackEvent("Romanized song info", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() }
                });
            }
            else 
            {
                songInfo.Romanized = null;
            }

            await  UpdateSong(songInfo, true, true);
            Log(Type.Info, "Finished getting info from Genius");
            Analytics.TrackEvent("Finished getting info from Genius", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() }
                });
        }

        private async Task GetAndShowLyrics()
        {
            Log(Type.Info, "Started GetAndShowLyrics method");

            #region UI Variables
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView infoTxt = FindViewById<TextView>(Resource.Id.infoTxt);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            ImageButton fabMore = FindViewById<ImageButton>(Resource.Id.fabMore);
            #endregion

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = await web.LoadFromWebAsync("https://genius.com" + songInfo.Normal.Path);

            songInfo.Normal.Lyrics = await HTMLParsing.ParseHTML(doc);
            Log(Type.Processing, "Parsed HTML");

            // Auto-romanize based on preferences
            if (songInfo.Normal.Lyrics.ContainsJapanese() && prefs.GetBoolean("auto_romanize", false))
            {
                //TODO: Add furigana/okurigana support across app
                string romanizedLyrics = await JapaneseTools.GetTransliteration(songInfo.Normal.Lyrics, true);
                RomanizedSong romanized = songInfo.Romanized;

                if (songInfo.Romanized == null) { songInfo.Romanized = new RomanizedSong(); } // This snippet is the same in GetAndShowSongDetails
                romanized.Lyrics = romanizedLyrics;

                //Fill empty info for songs with romanized lyrics and non-romanized details
                if (string.IsNullOrEmpty(romanized.Title) && !string.IsNullOrEmpty(songInfo.Normal.Title))
                    { romanized.Title = songInfo.Normal.Title; }
                if (string.IsNullOrEmpty(romanized.Artist) && !string.IsNullOrEmpty(songInfo.Normal.Artist))
                    { romanized.Artist = songInfo.Normal.Artist; }
                if (string.IsNullOrEmpty(romanized.Album) && !string.IsNullOrEmpty(songInfo.Normal.Album))
                    { romanized.Album = songInfo.Normal.Album; }
                if (string.IsNullOrEmpty(romanized.FeaturedArtist) && !string.IsNullOrEmpty(songInfo.Normal.FeaturedArtist))
                    { romanized.FeaturedArtist = songInfo.Normal.FeaturedArtist; }

                romanized.Id = songInfo.Normal.Id;
                songInfo.Romanized = romanized;
                songInfo.Normal.Romanized = true;

                Log(Type.Event, "Romanized song lyrics with ID " + songInfo.Normal.Id);
                Analytics.TrackEvent("Romanized song lyrics", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() }
                });
            }
            else 
            { 
                if (songInfo.Romanized != null) { songInfo.Romanized.Lyrics = ""; }
            }

            RunOnUiThread(() =>
            {
                fabMore.Visibility = ViewStates.Visible;
                savedView.Visibility = ViewStates.Gone;
                infoTxt.Visibility = ViewStates.Visible;
            });

            await UpdateSong(songInfo, false, false);

            RunOnUiThread(() =>
            {
                lyricsLoadingWheel.Visibility = ViewStates.Gone;
                refreshLayout.Refreshing = false;
            });

            shouldCheck = true;

            Log(Type.Info, "Finished getting lyrics from Genius");
            Analytics.TrackEvent("Finished getting lyrics from Genius", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() }
                });

        }
        #endregion


        #region Song Loading
        //Handles all song loading by itself.
        //Can be called at anytime if songInfo.Normal contains a song path
        private async Task LoadSong()
        {
            Log(Type.Event, "Started LoadSong method");

            if (welcomeView != null)
            {
                dynamicLayout.RemoveView(welcomeView);
                welcomeView = null;

                InflateSong();
            }

            #region UI Variables
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);

            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);

            searchTxt.Visibility = ViewStates.Gone;
            headerTxt.Visibility = ViewStates.Visible;
            #endregion

            //Check if song is downloaded
            if (await Database.GetSongFromTable(songInfo.Normal.Id) == null)
            {
                Log(Type.Event, "Song is not saved, getting data from Genius...");
                Analytics.TrackEvent("Loading song from Genius", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() }
                });

                RunOnUiThread(() =>
                {
                    savedView.Visibility = ViewStates.Gone;
                });

                try //EX: Add error handling to each method
                {
                    Task getDetails = GetAndShowSongDetails();
                    Task getLyrics = GetAndShowLyrics();
                    await Task.WhenAll(getDetails, getLyrics);
                }
                catch (NullReferenceException ex)
                {
                    Crashes.TrackError(ex, new Dictionary<string, string> { 
                        { "SongInfo", JsonConvert.SerializeObject(songInfo) }
                    });
                    Log(Type.Error, "NullReferenceException while getting lyrics for ID " + songInfo.Normal.Id);
                }
            }
            else
            {
                Log(Type.Event, "File for song exists, loading...");
                Analytics.TrackEvent("Loading song from file", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() }
                });

                await ReadFromFile();
            }

            fromNotification = false;
        }

        private async Task ReadFromFile()
        {
            Log(Type.Event, "Started ReadFromFile method");

            #region UI Variables
            ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
            TextView infoTxt = FindViewById<TextView>(Resource.Id.infoTxt);
            ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
            ImageButton fabMore = FindViewById<ImageButton>(Resource.Id.fabMore);
            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            #endregion

            songInfo = await Database.ReadLyrics(songInfo.Normal.Id);
            UpdateSong(songInfo, true, true, imagesOnDisk: true);

            RunOnUiThread(() =>
            {
                lyricsLoadingWheel.Visibility = ViewStates.Gone;
                fabMore.Visibility = ViewStates.Visible;
                savedView.Visibility = ViewStates.Visible;
                infoTxt.Visibility = ViewStates.Visible;

                refreshLayout.Refreshing = false;
            });

            shouldCheck = true;
            Log(Type.Info, "Done reading from file!");
            Analytics.TrackEvent("Finished reading song from file", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() }
                });
        }

        private async Task SaveSong()
        {
            //Simply pass current song info to WriteInfoAndLyrics method and it handles everything
            bool saveSuccessful = await Database.WriteInfoAndLyrics(songInfo);

            //Show Snackbar alerting user of result
            if (saveSuccessful)
            {
                RunOnUiThread(() =>
                {
                    ConstraintLayout layout = FindViewById<
                        ConstraintLayout>(Resource.Id.constraintMain);
                    Snackbar snackbar = Snackbar.Make(layout, Resource.String.savedSuccessfully, Snackbar.LengthLong);
                    snackbar.Show();
                });

                Log(Type.Event, "Song saved successfully");
                Analytics.TrackEvent("Finished saving song", new Dictionary<string, string> {
                    { "SongID", songInfo.Normal.Id.ToString() }
                });
            }
            else
            {
                RunOnUiThread(() =>
                {
                    ConstraintLayout layout = FindViewById<ConstraintLayout>(Resource.Id.constraintMain);
                    Snackbar snackbar = Snackbar.Make(layout, Resource.String.alreadySaved, Snackbar.LengthLong);
                    snackbar.Show();
                });

                Log(Type.Info, "Song already exists");
            }

            RunOnUiThread(() =>
            {
                ImageView savedView = FindViewById<ImageView>(Resource.Id.savedView);
                savedView.Visibility = ViewStates.Visible;
            });
        }
        #endregion


        #region Tools
        internal void InflateWelcome()
        {
            welcomeView = LayoutInflater.Inflate(Resource.Layout.sub_main_welcome, dynamicLayout, false);
            dynamicLayout.AddView(welcomeView);
            Log(Type.Processing, "Inflated welcome layout");
            Analytics.TrackEvent("Inflated welcome layout");

            Button gotoLyricsBtn = FindViewById<Button>(Resource.Id.gotoLyricsBtn);
            gotoLyricsBtn.Click += delegate
            {
                Intent intent = new Intent(this, typeof(SavedLyrics)).SetFlags(ActivityFlags.ReorderToFront);
                StartActivity(intent);
            };
        }

        internal void InflateSong()
        {
            songView = LayoutInflater.Inflate(Resource.Layout.sub_main_song, dynamicLayout, false);
            dynamicLayout.AddView(songView);
            Log(Type.Processing, "Inflated song layout");
            Analytics.TrackEvent("Inflated song layout");

            SwipeRefreshLayout refreshLayout = FindViewById<SwipeRefreshLayout>(Resource.Id.swipeRefreshLayout);
            ImageButton coverView = FindViewById<ImageButton>(Resource.Id.coverView);

            coverView.Click += async delegate
            {
                //TODO: Clicking on the cover an already saved song allows you to delete it
                //Make sure that the song has finished loading before attempting to save
                ProgressBar lyricsLoadingWheel = FindViewById<ProgressBar>(Resource.Id.lyricsLoadingWheel);
                if (!refreshLayout.Refreshing && lyricsLoadingWheel.Visibility != ViewStates.Visible)
                {
                    await SaveSong();
                }
            };
            refreshLayout.Refresh += async delegate
            {
                Log(Type.Info, "Refreshing song...");

                await LoadSong();
                refreshLayout.Refreshing = false;
            };

            shineView = FindViewById<ImageView>(Resource.Id.shineView);
        }

        private void ClearLabels()
        {
            #region UI Variables
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);
            #endregion

            RunOnUiThread(() =>
            {
                songLyrics.Text = "";
                songTitle.Text = "";
                songArtist.Text = "";
                songAlbum.Text = "";
                songFeat.Text = "";
            });
        }

        private async Task UpdateSong(SongBundle song, bool updateImages, bool updateDetails, bool imagesOnDisk = false)
        {
            Log(Type.Info, "Started UpdateSong method for ID " + song.Normal.Id);

            #region UI Variables
            TextView songLyrics = FindViewById<TextView>(Resource.Id.songLyrics);
            TextView songTitle = FindViewById<TextView>(Resource.Id.songTitle);
            TextView songArtist = FindViewById<TextView>(Resource.Id.songArtist);
            TextView songAlbum = FindViewById<TextView>(Resource.Id.songAlbum);
            TextView songFeat = FindViewById<TextView>(Resource.Id.songFeat);
            #endregion

            RunOnUiThread(() =>
            {
                Song toShow = song.Normal;
                if (song.Normal.Romanized && song.Romanized != null && prefs.GetBoolean("auto_romanize_details", false))
                {
                    Log(Type.Processing, $"Song with ID {songInfo.Normal.Id} has romanization data, using it for display");
                    toShow = (Song)song.Romanized;
                }

                if (!string.IsNullOrEmpty(song.Normal.Lyrics))
                {
                    //Make sure to alert user if for some reason the lyrics aren't loaded
                    string lyricsToShow = Resources.GetString(Resource.String.lyricsErrorOcurred);

                    bool romanizedIsValid = songInfo.Romanized != null && !string.IsNullOrEmpty(songInfo.Romanized.Lyrics) && prefs.GetBoolean("auto_romanize", false);
                    lyricsToShow = romanizedIsValid
                        ? songInfo.Romanized.Lyrics
                        : songInfo.Normal.Lyrics;

                    if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                    {
                        songLyrics.TextFormatted = Html.FromHtml(lyricsToShow, FromHtmlOptions.ModeCompact);
                    }
                    else
                    {
                        #pragma warning disable CS0618 // Type or member is obsolete
                        songLyrics.TextFormatted = Html.FromHtml(lyricsToShow);
                        #pragma warning restore CS0618 // Type or member is obsolete
                    }
                }

                if (updateDetails)
                {
                    songTitle.Text = toShow.Title;
                    songArtist.Text = toShow.Artist;
                    songAlbum.Text = toShow.Album;

                    if (string.IsNullOrEmpty(toShow.FeaturedArtist) || string.IsNullOrWhiteSpace(toShow.FeaturedArtist))
                    {
                        songFeat.Visibility = ViewStates.Gone;
                    }
                    else
                    {
                        songFeat.Visibility = ViewStates.Visible;
                        songFeat.Text = toShow.FeaturedArtist;
                    }
                }

                Log(Type.Processing, "Updated labels");
            });

            if (updateImages) //TODO: Add timer to retry after enough time with images not updated
            {
                ImageButton coverView = FindViewById<ImageButton>(Resource.Id.coverView);
                ImageView headerView = FindViewById<ImageView>(Resource.Id.headerView);
                ImageView searchView = FindViewById<ImageView>(Resource.Id.searchView);

                if (imagesOnDisk)
                {
                    string coverPath = Path.Combine(applicationPath + "/" + savedImagesLocation, song.Normal.Id + coverSuffix);
                    string headerPath = Path.Combine(applicationPath + "/" + savedImagesLocation, song.Normal.Id + headerSuffix);

                    RunOnUiThread(() =>
                    {
                        ImageService.Instance.LoadFile(coverPath)
                            .Transform(new RoundedTransformation(coverRadius))
                            .Into(coverView);

                        if (File.Exists(headerPath))
                        {
                            ImageService.Instance.LoadFile(headerPath)
                                .Transform(new BlurredTransformation(headerBlur))
                                .Into(headerView);
                            ImageService.Instance.LoadFile(headerPath)
                                .Transform(new CropTransformation(3, 0, 0))
                                .Transform(new BlurredTransformation(searchBlur))
                                .Transform(new BlurredTransformation(searchBlur))
                                .Into(searchView);
                        }
                        else
                        {
                            ImageService.Instance.LoadFile(coverPath)
                                .Transform(new BlurredTransformation(headerBlur))
                                .Into(headerView);
                            ImageService.Instance.LoadFile(coverPath)
                                .Transform(new CropTransformation(3, 0, 0))
                                .Transform(new BlurredTransformation(searchBlur))
                                .Transform(new BlurredTransformation(searchBlur))
                                .Into(searchView);
                        }
                    });
                    
                    Log(Type.Processing, "Updated images from disk");
                }
                else
                {
                    RunOnUiThread(() =>
                    {
                        ImageService.Instance.LoadUrl(song.Normal.Cover)
                            .Transform(new RoundedTransformation(coverRadius))
                            .Into(coverView);
                        ImageService.Instance.LoadUrl(song.Normal.Header)
                            .Transform(new BlurredTransformation(headerBlur))
                            .Into(headerView);
                        ImageService.Instance.LoadUrl(song.Normal.Header)
                            .Transform(new CropTransformation(3, 0, 0))
                            .Transform(new BlurredTransformation(searchBlur))
                            .Transform(new BlurredTransformation(searchBlur))
                            .Into(searchView);
                    });

                    Log(Type.Processing, "Updated images from the internet");
                }
            }
        }
        #endregion


        #region The stuff that's always on the bottom
        public override void OnBackPressed()
        {
            TextView headerTxt = FindViewById<TextView>(Resource.Id.headerTxt);
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);

            if (searchTxt.Visibility == ViewStates.Visible)
            {
                headerTxt.Visibility = ViewStates.Visible;
                searchTxt.Visibility = ViewStates.Gone;
                searchResults.Visibility = ViewStates.Gone;

                dynamicLayout.Visibility = ViewStates.Visible;
            }
            else
            {
                base.OnBackPressed();
            }
        }
        #endregion


        #region Error Handling
        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
        {
            Exception newExc = new Exception("TaskSchedulerOnUnobservedTaskException", unobservedTaskExceptionEventArgs.Exception);
            LogUnhandledException(newExc);
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            Exception newExc = new Exception("CurrentDomainOnUnhandledException", unhandledExceptionEventArgs.ExceptionObject as Exception);
            LogUnhandledException(newExc);
        }

        internal void LogUnhandledException(Exception exception)
        {
            try
            {
                string errorMessage = string.Format("Time: {0}\r\nError: Unhandled Exception\r\n{1}",
                        DateTime.Now, exception.ToString());

                System.Diagnostics.Debug.WriteLine(errorMessage);
                Log(Type.Error, "Crash Report", errorMessage);
                Crashes.TrackError(exception);
                DisplayCrashReport();
            }
            catch
            {
                //Just suppress any error logging exceptions
            }
        }

        //EX: Finish exception handling
        // If there is an unhandled exception, the exception information is diplayed 
        // on screen the next time the app is started (only in debug configuration)
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
                    //File.Delete(errorFilePath);
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
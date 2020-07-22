using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Plugin.CurrentActivity;
using SmartLyrics.Common;
using SmartLyrics.Toolbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static SmartLyrics.Globals;

namespace SmartLyrics
{
    [Activity(Label = "SavedLyrics", ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.Orientation, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class SavedLyrics : AppCompatActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private List<Artist> artistList = new List<Artist>();
        private List<string> artistName;
        private List<Tuple<string, Song>> allSongs = new List<Tuple<string, Song>>();
        private Dictionary<string, List<Song>> artistSongs = new Dictionary<string, List<Song>>();

        private bool nonGrouped = false;

        //! used to alert a method that called PermissionChecking.CheckAndSetPermissions
        //! that the user made their decision
        //----------------------------
        //! index 0 is the status of the permission (true = arrived, false = didn't arrive)
        //! index 1 is the result (true = granted, false = denied)
        //same on any activity that asks for permissions
        private readonly bool[] permissionGranted = new bool[2] { false, false };


        #region Standard Activity Shit
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main_saved);
            CrossCurrentActivity.Current.Activity = this; //don't remove this, permission stuff needs it

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            ImageButton drawerBtn = FindViewById<ImageButton>(Resource.Id.drawerBtn);

            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;

            ExpandableListView savedList = FindViewById<ExpandableListView>(Resource.Id.savedList);
            ImageButton selectGroupingBtn = FindViewById<ImageButton>(Resource.Id.selectGroupingBtn);
            ListView savedListNonGrouped = FindViewById<ListView>(Resource.Id.savedListNonGrouped);

            drawerBtn.Click += delegate
            {
                drawer.OpenDrawer(navigationView);
            };

            selectGroupingBtn.Click += async delegate
            {
                if (!nonGrouped)
                {
                    nonGrouped = true;
                    savedList.Visibility = ViewStates.Gone;
                    savedListNonGrouped.Visibility = ViewStates.Visible;
                    selectGroupingBtn.SetImageDrawable(GetDrawable(Resource.Drawable.ic_ungrouped));
                    await ShowSavedSongs();
                }
                else
                {
                    nonGrouped = false;
                    savedList.Visibility = ViewStates.Visible;
                    savedListNonGrouped.Visibility = ViewStates.Gone;
                    selectGroupingBtn.SetImageDrawable(GetDrawable(Resource.Drawable.ic_grouped));
                    await ShowSavedSongs();
                }
            };

            savedList.ChildClick += delegate (object sender, ExpandableListView.ChildClickEventArgs e)
            {
                Log.WriteLine(LogPriority.Info, "SavedLyrics", "OnCreate: Clicked on item from grouped list");
                Intent intent = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.ReorderToFront);

                Song _ = artistSongs.ElementAt(e.GroupPosition).Value[e.ChildPosition];

                MainActivity.songInfo = _;
                MainActivity.fromFile = true;

                StartActivityForResult(intent, 1);
            };

            savedListNonGrouped.ItemClick += delegate (object sender, AdapterView.ItemClickEventArgs e)
            {
                Log.WriteLine(LogPriority.Info, "SavedLyrics", "OnCreate: Clicked on item from non-grouped list");
                Intent intent = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.ReorderToFront);

                Song _ = allSongs[e.Position].Item2;

                MainActivity.songInfo = _;
                MainActivity.fromFile = true;

                StartActivityForResult(intent, 1);
            };
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await ShowSavedSongs();
        }
        #endregion


        #region Button Actions
        private void NavigationView_NavigationViewSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);

            e.MenuItem.SetCheckable(true);
            _ = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.ReorderToFront);
            Intent intent;
            switch (e.MenuItem.ItemId)
            {
                case (Resource.Id.nav_search):
                    intent = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.ReorderToFront);
                    StartActivity(intent);
                    break;
                case (Resource.Id.nav_saved):
                    drawer.CloseDrawers();
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


        private async Task ShowSavedSongs()
        {
            //initialize UI variables
            ExpandableListView savedList = FindViewById<ExpandableListView>(Resource.Id.savedList);
            ListView savedListNonGrouped = FindViewById<ListView>(Resource.Id.savedListNonGrouped);
            ProgressBar progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
            //--UI--

            progressBar.Visibility = ViewStates.Visible;

            Log.WriteLine(LogPriority.Info, "SavedLyrics", "ShowSavedSongs: CheckAndSetPermissions returened true, trying to read directory...");
            string path = Path.Combine(applicationPath, savedLyricsLocation);

            await MiscTools.CheckAndCreateAppFolders();
            Log.WriteLine(LogPriority.Verbose, "SavedLyrics", $"ShowSavedSongs: Path is \"{path}\"");

            List<Song> songList = await DatabaseHandling.GetSongList();
            if (songList != null)
            {
                await GetSavedList(songList);

                artistName = artistList.ConvertAll(e => e.Name);
                allSongs = new List<Tuple<string, Song>>();
                artistSongs = new Dictionary<string, List<Song>>();

                foreach (Artist a in artistList)
                {
                    artistSongs.Add(a.Name, a.Songs);

                    foreach (Song s in a.Songs)
                    {
                        allSongs.Add(new Tuple<string, Song>(a.Name, s));
                    }
                }

                Log.WriteLine(LogPriority.Verbose, "SavedLyrics", "ShowSavedSongs: Setted up adapter data");

                if (!nonGrouped)
                {
                    savedList.SetAdapter(new ExpandableListAdapter(this, artistName, artistSongs));
                    Log.WriteLine(LogPriority.Info, "SavedLyrics", "ShowSavedSongs: Showing adapter for grouped view");
                    progressBar.Visibility = ViewStates.Gone;
                }
                else
                {
                    savedListNonGrouped.Adapter = new SavedLyricsAdapter(this, allSongs);
                    Log.WriteLine(LogPriority.Info, "SavedLyrics", "ShowSavedSongs: Showing adapter for non grouped view");
                    progressBar.Visibility = ViewStates.Gone;
                }
            }
            else
            {
                Log.WriteLine(LogPriority.Info, "SavedLyrics", "ShowSavedSongs: No files found!");
                progressBar.Visibility = ViewStates.Gone;
            }
        }

        //makes a list with all artists and songs saved
        private async Task GetSavedList(List<Song> songList)
        {
            //initializing UI variables
            ExpandableListView savedList = FindViewById<ExpandableListView>(Resource.Id.savedList);
            //--UI--

            artistList = new List<Artist>();

            Log.WriteLine(LogPriority.Verbose, "SavedLyrics", "GetSavedList: Starting foreach loop");
            foreach (Song s in songList)
            {
                Log.WriteLine(LogPriority.Verbose, "SavedLyrics", "GetSavedList: " + s.Artist + " - " + s.Title);

                //finds the first Artist that matches the artist name from the song
                Artist existingArtist = artistList?.SingleOrDefault(x => x.Name == s.Artist);

                //! I think this is from StackOverflow, in a question I asked...
                //checks if the Artist was found in "artistList", adds them if it wasn't
                if (existingArtist != null)
                {
                    existingArtist.Songs.Add(s);
                    Log.WriteLine(LogPriority.Verbose, "SavedLyrics", "GetSavedList: Artist exists, adding song to list...");
                }
                else
                {
                    Artist artist = new Artist
                    {
                        Name = s.Artist,
                        Songs = new List<Song>()
                    };

                    artist.Songs.Add(s);
                    artistList.Add(artist);
                    Log.WriteLine(LogPriority.Verbose, "SavedLyrics", "GetSavedList: Artist doesn't exist, creating artist...");
                }
            }
        }

        //same on any activity that asks for permissions
        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Log.WriteLine(LogPriority.Verbose, "SavedLyrics", "OnRequestPermissionsResult: Permission: " + permissions[0] + " | Result: " + grantResults[0].ToString());

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
    }
}
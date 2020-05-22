using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

using Android;
using Android.App;
using Android.OS;
using Android.Content;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Widget;
using Android.Runtime;
using Android.Views;
using Android.Util;
using Android.Support.V4.App;
using Android.Support.V4.Content;

using Plugin.Permissions.Abstractions;
using Plugin.Permissions;
using Plugin.CurrentActivity;

using Microsoft.AppCenter.Crashes;

using static SmartLyrics.Globals;
using SmartLyrics.Common;

namespace SmartLyrics
{
    [Activity(Label = "SavedLyrics", ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.Orientation, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class SavedLyrics : AppCompatActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private List<Artist> artistList = new List<Artist>();

        List<string> artistName;
        List<Tuple<string, Song>> allSongs = new List<Tuple<string, Song>>();
        Dictionary<string, List<Song>> artistSongs = new Dictionary<string, List<Song>>();

        private bool nonGrouped = false;

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

            savedList.ChildClick += delegate (object sender, ExpandableListView.ChildClickEventArgs e) {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Clicked on item from grouped list");
                var intent = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.ReorderToFront);

                Song _ = artistSongs.ElementAt(e.GroupPosition).Value[e.ChildPosition];

                MainActivity.songInfo = _;
                MainActivity.fromFile = true;

                StartActivityForResult(intent, 1);
            };

            savedListNonGrouped.ItemClick += delegate (object sender, AdapterView.ItemClickEventArgs e)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Clicked on item from non-grouped list");
                var intent = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.ReorderToFront);

                Song _ = allSongs[e.Position].Item2;

                MainActivity.songInfo = _;
                MainActivity.fromFile = true;

                StartActivityForResult(intent, 1);
            };
        }

        protected override async void OnResume()
        {
            base.OnResume();

            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.ReadExternalStorage) == (int)Android.Content.PM.Permission.Granted)
            {
                await ShowSavedSongs();
            }
            else
            {
                await CheckAndSetPermissions(Manifest.Permission.ReadExternalStorage);
            }
        }

        private async Task ShowSavedSongs()
        {
            //initialize UI variables
            ExpandableListView savedList = FindViewById<ExpandableListView>(Resource.Id.savedList);
            ListView savedListNonGrouped = FindViewById<ListView>(Resource.Id.savedListNonGrouped);
            ProgressBar progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
            //--UI--

            progressBar.Visibility = ViewStates.Visible;

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: CheckAndSetPermissions returened true, trying to read directory...");
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation);
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", $"SavedLyrics.cs: Path is \"{path}\"");

            await CheckAndCreateAppFolders();

            List<Song> songList = await DatabaseHandling.GetSongList();
            if (songList != null)
            {
                await GetSavedList(songList);

                artistName = artistList.ConvertAll(e => e.name);
                allSongs = new List<Tuple<string, Song>>();
                artistSongs = new Dictionary<string, List<Song>>();

                foreach (Artist a in artistList)
                {
                    artistSongs.Add(a.name, a.songs);

                    foreach (Song s in a.songs)
                    {
                        allSongs.Add(new Tuple<string, Song>(a.name, s));
                    }
                }

                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Setted up adapter data");

                if (!nonGrouped)
                {
                    savedList.SetAdapter(new ExpandableListAdapter(this, artistName, artistSongs));
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Showing adapter for grouped view");
                    progressBar.Visibility = ViewStates.Gone;
                }
                else
                {
                    savedListNonGrouped.Adapter = new SavedLyricsAdapter(this, allSongs);
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Showing adapter for non grouped view");
                    progressBar.Visibility = ViewStates.Gone;
                }
            }
            else
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: No files found!");
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

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Starting foreach loop");
            foreach (Song s in songList)
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: " + s.artist + " - " + s.title);

                //finds the first Artist that matches the artist name from the song
                Artist existingArtist = artistList?.SingleOrDefault(x => x.name == s.artist);

                //! I think this is from StackOverflow, in a question I asked...
                //checks if the Artist was found in "artistList", adds them if it wasn't
                if (existingArtist != null)
                {
                    existingArtist.songs.Add(s);
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Artist exists, adding song to list...");
                }
                else
                {
                    Artist artist = new Artist
                    {
                        name = s.artist,
                        songs = new List<Song>()
                    };

                    artist.songs.Add(s);
                    artistList.Add(artist);
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Artist doesn't exist, creating artist...");
                }
            }
        }

        //this method isn't on Globals because it would be too hard to
        //show the snackbar without proper context (Activity references)
        public async Task CheckAndSetPermissions(string permission)
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Starting CheckAndSetPermissions...");

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    //SetContentView(Resource.Layout.main);
                    
                    if (ContextCompat.CheckSelfPermission(this, permission) == (int)Android.Content.PM.Permission.Granted)
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Permission for" + permission + " already granted");

                        await ShowSavedSongs();
                    }
                    else
                    {
                        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Storage))
                        {
                            Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: 'My lord, is this legal?' 'I will make it legal...'");

                            LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.linearFullscreen2);
                            string[] p = { permission };
                            var snackbar = Snackbar.Make(layout, this.Resources.GetString(Resource.String.needStoragePermission), Snackbar.LengthIndefinite)
                                .SetAction(Android.Resource.String.Ok, new Action<View>(delegate (View obj)
                                {
                                    ActivityCompat.RequestPermissions(this, p, 1);
                                }));
                            snackbar.Show();
                        }
                        else
                        {
                            Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: No need to ask user, trying to get permission...");

                            string[] p = { permission };
                            ActivityCompat.RequestPermissions(this, p, 1);
                        }
                    }
                }
                else
                {
                    await ShowSavedSongs();
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "SavedLyrics.cs: Exception caught! " + ex.Message);
                Crashes.TrackError(ex);
            }
        }

        void NavigationView_NavigationViewSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
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
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Test for Settings button on drawer");
                    break;
            }

            e.MenuItem.SetCheckable(false);
            drawer.CloseDrawers();
        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Permission: " + permissions[0] + " | Result: " + grantResults[0].ToString());

            if (grantResults[0] == Android.Content.PM.Permission.Granted)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Write permission granted!");
                await ShowSavedSongs();
            }
            else if (grantResults[0] == Android.Content.PM.Permission.Denied)
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "SavedLyrics.cs: Permission denied");

                LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.linearFullscreen2);
                var snackbar = Snackbar.Make(layout, Resource.String.permissionDenied, Snackbar.LengthLong);
                snackbar.Show();
            }
        }
    }
}
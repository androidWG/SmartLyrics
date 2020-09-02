using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;

using Newtonsoft.Json;
using Plugin.CurrentActivity;

using SmartLyrics.Common;
using SmartLyrics.Toolbox;
using static SmartLyrics.Globals;
using static SmartLyrics.Common.Logging;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AppCenter.Analytics;
using SmartLyrics.IO;

namespace SmartLyrics
{
    [Activity(Label = "SavedLyrics", ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.Orientation, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public class SavedLyrics : AppCompatActivity
    {
        private List<Artist> artistList = new List<Artist>();
        private List<SongBundle> allSongs = new List<SongBundle>();
        private Dictionary<Artist, List<SongBundle>> artistSongs = new Dictionary<Artist, List<SongBundle>>();

        private bool nonGrouped; //TODO: Add persistency to grouping option

        #region Standard Activity Shit
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main_saved);
            CrossCurrentActivity.Current.Activity = this; //don't remove this, permission stuff needs it

            #region UI Variables
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            ImageButton drawerBtn = FindViewById<ImageButton>(Resource.Id.drawerBtn);

            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);

            ExpandableListView savedList = FindViewById<ExpandableListView>(Resource.Id.savedList);
            ImageButton selectGroupingBtn = FindViewById<ImageButton>(Resource.Id.selectGroupingBtn);
            ListView savedListNonGrouped = FindViewById<ListView>(Resource.Id.savedListNonGrouped);
            #endregion

            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;
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

            savedList.ChildClick += async delegate (object sender, ExpandableListView.ChildClickEventArgs e)
            {
                SongBundle selection = artistSongs.ElementAt(e.GroupPosition).Value[e.ChildPosition];

                Log(Type.Action, "Clicked on item from grouped list");
                Analytics.TrackEvent("Clicked on item from non-grouped list", new Dictionary<string, string> {
                    { "SongID", selection.Normal.Id.ToString() }
                });

                await OpenInMainActivity(selection);
            };

            savedListNonGrouped.ItemClick += async delegate (object sender, AdapterView.ItemClickEventArgs e)
            {
                SongBundle selection = allSongs[e.Position];

                Log(Type.Action, "Clicked on item from non-grouped list");
                Analytics.TrackEvent("Clicked on item from non-grouped list", new Dictionary<string, string> {
                    { "SongID", selection.Normal.Id.ToString() }
                });

                await OpenInMainActivity(selection);
            };
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await ShowSavedSongs();
        }
        #endregion


        #region Button Actions
        private async Task OpenInMainActivity(SongBundle song)
        {
            Intent intent = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.ReorderToFront);
            intent.PutExtra("SavedSong", JsonConvert.SerializeObject(song));
            StartActivityForResult(intent, 1);
        }

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

            string path = Path.Combine(ApplicationPath, SavedLyricsLocation);

            await MiscTools.CheckAndCreateAppFolders();
            Log(Type.Info, $"Saved lyrics location is '{path}'");

            List<SongBundle> songList = await Database.GetSongList();
            if (songList != null)
            {
                await GetSavedList(songList);

                allSongs = new List<SongBundle>();
                artistSongs = new Dictionary<Artist, List<SongBundle>>();

                foreach (Artist a in artistList)
                {
                    artistSongs.Add(a, a.Songs);

                    foreach (SongBundle s in a.Songs)
                    {
                        allSongs.Add(s);
                    }
                }

                Log(Type.Processing, "Setted up adapter data");

                if (nonGrouped)
                {
                    savedListNonGrouped.Adapter = new SavedLyricsAdapter(this, allSongs);
                    Log(Type.Info, "Showing adapter for non grouped view");
                    progressBar.Visibility = ViewStates.Gone;
                }
                else
                {
                    savedList.SetAdapter(new ExpandableListAdapter(this, artistList, artistSongs));
                    Log(Type.Info, "Showing adapter for grouped view");
                    progressBar.Visibility = ViewStates.Gone;
                }
            }
            else
            {
                Log(Type.Info, "No files found!");
                progressBar.Visibility = ViewStates.Gone;
            }
        }

        //makes a list with all artists and songs saved
        private async Task GetSavedList(List<SongBundle> songList)
        {
            artistList = new List<Artist>();

            Log(Type.Processing, "Starting foreach loop");
            foreach (SongBundle s in songList)
            {
                //finds the first Artist that matches the artist name from the song
                Artist existingArtist = artistList?.SingleOrDefault(x => x.Name == s.Normal.Artist);

                //! I think this is from StackOverflow, in a question I asked...
                //checks if the Artist was found in "artistList", adds them if it wasn't
                if (existingArtist != null)
                {
                    existingArtist.Songs.Add(s);
                    if (s.Normal.Romanized && s.Romanized != null && string.IsNullOrEmpty(s.Romanized.Artist))
                    { existingArtist.RomanizedName = s.Romanized.Artist; }
                }
                else
                {
                    Artist artist = new Artist
                    {
                        Name = s.Normal.Artist,
                        Songs = new List<SongBundle>()
                    };

                    if (s.Normal.Romanized && s.Romanized != null && !string.IsNullOrEmpty(s.Romanized.Artist))
                    { artist.RomanizedName = s.Romanized.Artist; }

                    artist.Songs.Add(s);
                    artistList.Add(artist);
                }
            }
        }
    }
}
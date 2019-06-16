using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using Android.App;
using Android.OS;
using Android.Content;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Widget;
using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using Android.Util;
using Android.Support.V4.App;

using Plugin.Permissions.Abstractions;
using Plugin.Permissions;
using Plugin.CurrentActivity;
using Android.Support.V4.Content;
using Android;

namespace SmartLyrics
{
    [Activity(Label = "SavedLyrics")]
    public class SavedLyrics : AppCompatActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        List<Extensions.Artist> artistClassList = new List<Extensions.Artist>();
        string savedLyricsLocation = "SmartLyrics/Saved Lyrics/";
        string savedSeparator = @"!@=-@!";
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main_saved);
            CrossCurrentActivity.Current.Activity = this; //don't remove this, permission stuff needs it

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar2);
            SetSupportActionBar(toolbar);
            SupportActionBar.Title = "";
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout2);
            Android.Support.V7.App.ActionBarDrawerToggle toggle = new Android.Support.V7.App.ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
            drawer.AddDrawerListener(toggle);
            toggle.SyncState();

            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;

            ExpandableListView savedList = FindViewById<ExpandableListView>(Resource.Id.savedList);

            await checkAndSetPermissions(Manifest.Permission.ReadExternalStorage);

            savedList.ChildClick += delegate (object sender, ExpandableListView.ChildClickEventArgs e) {
                var intent = new Intent(this, typeof(MainActivity));

                intent.PutExtra("From File?", "true");
                intent.PutExtra("Title", e.ClickedView.FindViewById<TextView>(Resource.Id.listChild).Text);
                intent.PutExtra("Artist", artistClassList.ElementAt(e.GroupPosition).name);

                StartActivity(intent);
            };
        }

        private async Task getSavedSongList()
        {
            ExpandableListView savedList = FindViewById<ExpandableListView>(Resource.Id.savedList);

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: checkAndSetPermissions returened true, reading directory...");
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation);
            string[] filesList = Directory.GetFiles(path);

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Starting foreach loop");
            foreach (string s in filesList)
            {
                string newS = s.Replace(path.ToString(), "");
                newS = newS.Replace(".txt","");
                string[] splitted = newS.Split(savedSeparator);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: " + splitted[0] + " - " + splitted[1]);

                var existingArtist = artistClassList?.SingleOrDefault(x => x.name == splitted[0]);

                //^^^ FirstOrDefault clause while give you instance of Artist based on condition/predicate
                //Null check for checking Artist is already exist in list or not.
                if (existingArtist != null)
                {
                    existingArtist.songs.Add(splitted[1]);
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Artist exists, adding song to list...");
                }
                else
                {
                    Extensions.Artist artist = new Extensions.Artist();
                    artist.name = splitted[0];
                    artist.songs = new List<string>();
                    artist.songs.Add(splitted[1]);
                    artistClassList.Add(artist);
                    Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Artist doesn't exist, creating artist...");
                }
            }

            List<string> artistName = artistClassList.ConvertAll(e => e.name);
            Dictionary<string, List<string>> artistSongs = new Dictionary<string, List<string>>();

            foreach (Extensions.Artist a in artistClassList)
            {
                artistSongs.Add(a.name, a.songs);
            }

            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "SavedLyrics.cs: Setted up adapter data");
            var listAdapter = new Extensions.ExpandableListAdapter(this, artistName, artistSongs);
            savedList.SetAdapter(listAdapter);
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Showing adapter");
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

                        await getSavedSongList();
                    }
                    else
                    {
                        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Storage))
                        {
                            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: 'My lord, is this legal?' 'I will make it legal...'");

                            LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.linearFullscreen2);
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
                    await getSavedSongList();
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "MainActivity.cs: Exception caught! " + ex.Message);
            }
        }

        void NavigationView_NavigationViewSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            switch (e.MenuItem.ItemId)
            {
                case (Resource.Id.nav_search):
                    StartActivity(typeof(MainActivity));
                    break;
                case (Resource.Id.nav_saved):
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Test for Saved button on drawer");
                    break;
                case (Resource.Id.nav_settings):
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Test for Settings button on drawer");
                    break;
                case (Resource.Id.nav_about):
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "SavedLyrics.cs: Test for About button on drawer");
                    break;
            }
        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Permission: " + permissions[0] + " | Result: " + grantResults[0].ToString());

            if (grantResults[0] == Android.Content.PM.Permission.Granted)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "MainActivity.cs: Write permission granted!");
                await getSavedSongList();
            }
            else if (grantResults[0] == Android.Content.PM.Permission.Denied)
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "MainActivity.cs: Permission denied");

                LinearLayout layout = FindViewById<LinearLayout>(Resource.Id.linearFullscreen2);
                var snackbar = Snackbar.Make(layout, Resource.String.permissionDenied, Snackbar.LengthLong);
                snackbar.Show();
            }
        }
    }
}
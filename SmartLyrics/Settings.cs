
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Util;
using Android.Widget;
using AndroidX.Preference;

namespace SmartLyrics
{
    public class SettingsFragment : PreferenceFragmentCompat
    {
        public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
        {
            AddPreferencesFromResource(Resource.Xml.perfs);
        }
    }

    [Activity(Label = "SettingsActivity", ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.Orientation, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class SettingsActivity : AndroidX.AppCompat.App.AppCompatActivity
    {
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main_settings);

            ImageButton drawerBtn = FindViewById<ImageButton>(Resource.Id.drawerBtn);
            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;

            SupportFragmentManager
                .BeginTransaction()
                .Replace(Resource.Id.settingsContainer, new SettingsFragment())
                .Commit();

            drawerBtn.Click += delegate
            {
                drawer.OpenDrawer(navigationView);
            };

            Log.WriteLine(LogPriority.Info, "Settings", "OnCreate: Finished OnCreate");
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
                    intent = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.ReorderToFront);
                    StartActivity(intent);
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
                    drawer.CloseDrawers();
                    break;
            }

            e.MenuItem.SetCheckable(false);
            drawer.CloseDrawers();
        }
    }
}
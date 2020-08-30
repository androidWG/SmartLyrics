using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Widget;
using AndroidX.Preference;
using Java.Util;
using Microsoft.AppCenter.Analytics;
using System.Collections.Generic;
using static SmartLyrics.Common.Logging;

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
    public class SettingsActivity : AndroidX.AppCompat.App.AppCompatActivity, ISharedPreferencesOnSharedPreferenceChangeListener
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

            Log(Type.Info, "Finished OnCreate");
        }

        protected override void OnResume()
        {
            base.OnResume();
            PreferenceManager.GetDefaultSharedPreferences(this).RegisterOnSharedPreferenceChangeListener(this);
        }

        protected override void OnPause()
        {
            base.OnPause();
            PreferenceManager.GetDefaultSharedPreferences(this).UnregisterOnSharedPreferenceChangeListener(this);
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

        public async void OnSharedPreferenceChanged(ISharedPreferences sharedPreferences, string key)
        {
            if (key == "sendAnalytics")
            {
                await Analytics.SetEnabledAsync(sharedPreferences.GetBoolean("sendAnalytics", true));
                bool currentSetting = await Analytics.IsEnabledAsync();
                Log(Type.Action, "Changed send analytics to " + currentSetting.ToString());
            }
        }
    }
}
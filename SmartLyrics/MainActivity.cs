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
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartLyrics
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        List<Genius.Song> resultsToView;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            //SetSupportActionBar(toolbar);
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            ActionBarDrawerToggle toggle = new ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
            drawer.AddDrawerListener(toggle);
            toggle.SyncState();

            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            navigationView.NavigationItemSelected += NavigationView_NavigationViewSelected;
            //navigationView.SetNavigationItemSelectedListener(this);

            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);

            Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "SmartLyrics", "MainActivity.cs: Loaded view");

            searchTxt.EditorAction += async delegate { await searchBtn_Click(); };

            searchResults.ItemClick += searchResults_ItemClick;
        }

        async Task searchBtn_Click()
        {
            ProgressBar searchLoadingWheel = FindViewById<ProgressBar>(Resource.Id.searchLoadingWheel);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);

            if (searchTxt.Text != "")
            {
                searchLoadingWheel.Visibility = ViewStates.Visible;
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "SmartLyrics", "MainActivity.cs: Started 'await getAndShowSearchResultsAsync' task");
                InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);
                imm.HideSoftInputFromWindow(searchTxt.WindowToken, 0);
                await getAndShowSearchResultsAsync();
                searchLoadingWheel.Visibility = ViewStates.Gone;
            }
        }

        void searchResults_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "SmartLyrics", "MainActivity.cs: Attempting to start activity");
            var intent = new Intent(this, typeof(LyricsViwerActivity));
            intent.PutExtra("Title", resultsToView.ElementAt(e.Position).title.ToString());
            intent.PutExtra("Artist", resultsToView.ElementAt(e.Position).artist.ToString());
            intent.PutExtra("Header", resultsToView.ElementAt(e.Position).header.ToString());
            intent.PutExtra("Cover", resultsToView.ElementAt(e.Position).cover.ToString());
            intent.PutExtra("APIPath", resultsToView.ElementAt(e.Position).APIPath.ToString());
            intent.PutExtra("Path", resultsToView.ElementAt(e.Position).path.ToString());
            intent.PutExtra("FromFile", "false");
            StartActivity(intent);
        }

        void NavigationView_NavigationViewSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            switch (e.MenuItem.ItemId)
            {
                case (Resource.Id.nav_search):
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "SmartLyrics", "MainActivity.cs: Test for Search button on drawer");
                    break;
                case (Resource.Id.nav_saved):
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "SmartLyrics", "MainActivity.cs: Test for Saved button on drawer");
                    break;
                case (Resource.Id.nav_settings):
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "SmartLyrics", "MainActivity.cs: Test for Settings button on drawer");
                    break;
                case (Resource.Id.nav_about):
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "SmartLyrics", "MainActivity.cs: Test for About button on drawer");
                    break;
            }
        }

        public async Task getAndShowSearchResultsAsync()
        {
            ListView searchResults = FindViewById<ListView>(Resource.Id.searchResults);
            EditText searchTxt = FindViewById<EditText>(Resource.Id.searchTxt);

            Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Starting async GetSearchResults operation");
            string results = await Genius.GetSearchResults(searchTxt.Text, "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28");
            JObject parsed = JObject.Parse(results);
            Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Results parsed into JObject");

            IList<JToken> parsedList = parsed["response"]["hits"].Children().ToList();
            Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Parsed results into list");
            resultsToView = new List<Genius.Song>();
            foreach (JToken result in parsedList)
            {
                Genius.Song song = new Genius.Song()
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
            Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "MainActivity.cs: Created results list for listVew");

            var adapter = new Genius.SearchResultAdapter(this, resultsToView);
            searchResults.Adapter = adapter;

            Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "SmartLyrics", "MainActivity.cs: Added results to activity view");
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            int id = item.ItemId;

            Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "MainActivity.cs: Test for random button on drawer");

            if (id == Resource.Id.nav_search)
            {
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "MainActivity.cs: Test for Search button on drawer");
            }
            else if (id == Resource.Id.nav_saved)
            {
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "MainActivity.cs: Test for Saved button on drawer");
            }
            else if (id == Resource.Id.nav_settings)
            {
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "MainActivity.cs: Test for Settings button on drawer");
            }
            else if (id == Resource.Id.nav_about)
            {
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "MainActivity.cs: Test for About button on drawer");
            }

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer.CloseDrawer(GravityCompat.Start);
            return true;
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
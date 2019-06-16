using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using Android.Widget;
using Android.Views;
using Android.App;
using System.Collections.Generic;
using System.Net.Http;
using Android.Graphics;
using FFImageLoading;
using FFImageLoading.Transformations;

namespace SmartLyrics
{
    class Genius
    {
        public static async Task<string> GetSearchResults(string searchTerm, string authHeader)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "GeniusRequests.cs: Adding Auth headers to HttpClient");
                    client.DefaultRequestHeaders.Add("Authorization", authHeader);
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "Url sent to HttpClient: "+new Uri("https://api.genius.com/search?q=") + Uri.EscapeDataString(searchTerm));
                    HttpResponseMessage responseAsync = await client.GetAsync(new Uri("https://api.genius.com/search?q=") + Uri.EscapeDataString(searchTerm));

                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "GeniusRequests.cs: Reading content stream...");
                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (HttpRequestException e)
                {
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "SmartLyrics", "Exception Caught:"+e.Message);
                    return null;
                }
            }
        }

        public static async Task<string> GetSongDetails(string APIPath, string authHeader)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "GeniusRequests.cs: Adding Auth headers to HttpClient");
                    client.DefaultRequestHeaders.Add("Authorization", authHeader);
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "Url sent to HttpClient: " + new Uri("https://api.genius.com" + APIPath));
                    HttpResponseMessage responseAsync = await client.GetAsync(new Uri("https://api.genius.com" + APIPath));

                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "GeniusRequests.cs: Reading content stream...");
                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (HttpRequestException e)
                {
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "SmartLyrics", "Exception Caught:" + e.Message);
                    return null;
                }
            }
        }

        public static async Task<string> GetSongLyrics(string path, string authHeader)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "GeniusRequests.cs: Adding Auth headers to HttpClient");
                    client.DefaultRequestHeaders.Add("Authorization", authHeader);
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "Url sent to HttpClient: " + new Uri("https://genius.com" + path));
                    HttpResponseMessage responseAsync = await client.GetAsync(new Uri("https://api.genius.com" + path));

                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "GeniusRequests.cs: Reading content stream...");
                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (HttpRequestException e)
                {
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "SmartLyrics", "Exception Caught:" + e.Message);
                    return null;
                }
            }
        }

        public class Song
        {
            public string title { get; set; }
            public string artist { get; set; }
            public string album { get; set; }
            public string featuredArtist { get; set; }
            public string cover { get; set; }
            public string header { get; set; }
            public string APIPath { get; set; }
            public string path { get; set; }
            public int id { get; set; }
        }

        public class ViewHolder: Java.Lang.Object
        {
            public TextView titleTxt { get; set; }
            public TextView artistTxt { get; set; }
            public ImageView coverImg { get; set; }
        }

        public class SearchResultAdapter : BaseAdapter<Song>
        {
            private Activity activity;
            private List<Song> songs;

            public override Song this[int position] => throw new NotImplementedException();

            public SearchResultAdapter(Activity activity,List<Song> songs)
            {
                this.activity = activity;
                this.songs = songs;
            }

            public override int Count 
            {
                get { return songs.Count; }
            }

            public override long GetItemId(int position)
            {
                return songs[position].id;
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                var view = convertView ?? activity.LayoutInflater.Inflate(Resource.Layout.list_item, parent, false);
                var titleTxt = view.FindViewById<TextView>(Resource.Id.title);
                var artistTxt = view.FindViewById<TextView>(Resource.Id.artist);
                var coverImg = view.FindViewById<ImageView>(Resource.Id.cover);

                titleTxt.Text = songs[position].title;
                artistTxt.Text = songs[position].artist;
                ImageService.Instance.LoadUrl(songs[position].cover).Transform(new RoundedTransformation(20)).Into(coverImg);

                return view;
            }
        }
    }
}
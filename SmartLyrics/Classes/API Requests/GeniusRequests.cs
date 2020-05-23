using Android.Widget;

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartLyrics.APIRequests
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
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "SmartLyrics", "Exception caught while getting search results!\n" + e.ToString());
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
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "SmartLyrics", "Exception caught while getting song details!\n" + e.ToString());
                    return null;
                }
            }
        }

        public class ViewHolder: Java.Lang.Object
        {
            public TextView titleTxt { get; set; }
            public TextView artistTxt { get; set; }
            public ImageView coverImg { get; set; }
        }
    }
}
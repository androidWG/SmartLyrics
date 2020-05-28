using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartLyrics.APIRequests
{
    internal class Spotify
    {
        public static async Task<string> GetSavedSongs(string authHeader, string url)
        {

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Add("Authorization", authHeader);

                    Uri urlToSend = new Uri(url);

                    HttpResponseMessage responseAsync = await client.GetAsync(urlToSend);
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "SpotifyRequests.cs: Url sent to HttpClient: " + urlToSend);

                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "SpotifyRequests.cs: Reading content stream...");
                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (HttpRequestException e)
                {
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "SmartLyrics", "SpotifyRequests.cs: Exception caught while getting song list from Spotify!\n" + e.ToString());
                    return null;
                }
            }
        }
    }
}
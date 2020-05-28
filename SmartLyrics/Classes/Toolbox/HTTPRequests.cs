using Microsoft.AppCenter.Crashes;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SmartLyrics.Toolbox
{
    class HTTPRequests
    {
        public static async Task<string> GetRequest(string url, string authHeader)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", authHeader);
                    }

                    Uri urlToSend = new Uri(url);

                    HttpResponseMessage responseAsync = await client.GetAsync(urlToSend);
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "HTTPRequests.cs: Url sent to HttpClient: " + urlToSend);

                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "HTTPRequests.cs: Reading content stream...");
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (HttpRequestException e)
            {
                Crashes.TrackError(e);

                Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "SmartLyrics", $"Exception caught while getting URL {url}! \n{e}");
                return null;
            }
        }

        public static async Task<string> PostRequest(string url, string body, string authHeader = "")
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", authHeader);
                    }

                    HttpContent content = new StringContent(body, Encoding.UTF8, "text/plain");

                    HttpResponseMessage responseAsync = await client.PostAsync(url, content);
                    Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "SmartLyrics", "HTTPRequests.cs: Url sent to HttpClient: " + url);

                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Android.Util.Log.WriteLine(Android.Util.LogPriority.Verbose, "SmartLyrics", "HTTPRequests.cs: Reading content stream...");
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (HttpRequestException e)
            {
                Crashes.TrackError(e);

                Android.Util.Log.WriteLine(Android.Util.LogPriority.Error, "SmartLyrics", $"Exception caught while getting URL {url}! \n{e}");
                return null;
            }
        }
    }
}
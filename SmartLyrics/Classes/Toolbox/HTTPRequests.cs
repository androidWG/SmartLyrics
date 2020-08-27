using Android.Util;

using Microsoft.AppCenter.Crashes;

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SmartLyrics.Toolbox
{
    internal class HTTPRequests
    {
        //TODO: Add network error handling (fix Null Reference errors when these methods catch an Exception)
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
                    Log.WriteLine(LogPriority.Verbose, "HTTPRequests", "GetRequest: Url sent to HttpClient: " + urlToSend);

                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Log.WriteLine(LogPriority.Verbose, "HTTPRequests", "GetRequest: Reading content stream...");
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                Crashes.TrackError(e);

                Log.WriteLine(LogPriority.Error, "HTTPRequests", $"GetRequest: Exception caught while getting URL {url}! \n{e}");
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
                    Log.WriteLine(LogPriority.Verbose, "HTTPRequests", "PostRequest: Url sent to HttpClient: " + url);

                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Log.WriteLine(LogPriority.Verbose, "HTTPRequests", "PostRequest: Reading content stream...");
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                Crashes.TrackError(e);

                Log.WriteLine(LogPriority.Error, "HTTPRequests", $"PostRequest: Exception caught while getting URL {url}! \n{e}");
                return null;
            }
        }
    }
}
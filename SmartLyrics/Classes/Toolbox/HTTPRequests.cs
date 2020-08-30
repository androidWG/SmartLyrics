using Microsoft.AppCenter.Crashes;
using static SmartLyrics.Common.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Type = SmartLyrics.Common.Logging.Type;

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

                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Log(Type.Processing, "Reading content stream...");
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                Crashes.TrackError(e);

                Log(Type.Error, $"Exception caught while getting URL {url}! \n{e}");
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
                    Log(Type.Info, "" + url);

                    using (Stream stream = await responseAsync.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Log(Type.Info, "Reading content stream...");
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                Crashes.TrackError(e);

                Log(Type.Error, $"Exception caught while getting URL {url}! \n{e}");
                return null;
            }
        }
    }
}
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static Android.App.ActivityManager;
using Path = System.IO.Path;

namespace SmartLyrics
{
    class GlobalMethods
    {
        public static string savedLyricsLocation = "SmartLyrics/Saved Lyrics/";
        public static string savedImagesLocation = "SmartLyrics/Saved Lyrics/Image Cache/";

        public static async Task<int> Distance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                if (string.IsNullOrEmpty(t))
                    return 0;
                return t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // initialize the top and right of the table to 0, 1, 2, ...
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 1; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    int min1 = d[i - 1, j] + 1;
                    int min2 = d[i, j - 1] + 1;
                    int min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }
            return d[n, m];
        }

        public static bool IsInForeground()
        {
            RunningAppProcessInfo myProcess = new RunningAppProcessInfo();
            GetMyMemoryState(myProcess);

            return myProcess.Importance == Importance.Foreground;
        }

        public static float ConvertRange(
            float originalStart, float originalEnd, // original range
            float newStart, float newEnd, // desired range
            float value) // value to convert
        {
            double scale = (double)(newEnd - newStart) / (originalEnd - originalStart);
            return (float)(newStart + ((value - originalStart) * scale));
        }

        public double GetBrightness(Color color)
        {
            return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B);
        }

        public async Task<double> GetAverageBrightness(IEnumerable<Color> colors)
        {
            int count = 0;
            double sumBrightness = 0;

            foreach (var color in colors)
            {
                count++;
                sumBrightness += GetBrightness(color);
            }

            return sumBrightness / count;
        }

        public static async Task CheckIfAppFolderExists()
        {
            var path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation);
            var pathImg = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedImagesLocation);

            //TODO: add IOException handling

            if (Directory.Exists(path))
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GlobalMethods: /SmartLyrics/Saved Lyrics directory exists!");
            }
            else
            {
                Directory.CreateDirectory(path);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GlobalMethods: /SmartLyrics/Saved Lyrics directory doesn't exist, creating...");
            }

            if (Directory.Exists(pathImg))
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GlobalMethods: /SmartLyrics/Saved Lyrics/ImageCache directory exists!");
            }
            else
            {
                Directory.CreateDirectory(pathImg);
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "GlobalMethods: /SmartLyrics/Saved Lyrics/ImageCache directory doesn't exist, creating...");
            }
        }
    }
}
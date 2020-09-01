using Android.App;

using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using static Android.App.ActivityManager;
using static SmartLyrics.Globals;
using static SmartLyrics.Common.Logging;

namespace SmartLyrics.Toolbox
{
    internal static class MiscTools
    {
        public static float ConvertRange(
            float originalStart, float originalEnd, // original range
            float newStart, float newEnd, // desired range
            float value) // value to convert
        {
            double scale = (double)(newEnd - newStart) / (originalEnd - originalStart);
            return (float)(newStart + ((value - originalStart) * scale));
        }

        public static bool IsInForeground()
        {
            RunningAppProcessInfo myProcess = new RunningAppProcessInfo();
            GetMyMemoryState(myProcess);

            return myProcess.Importance == Importance.Foreground;
        }

        public static async Task CheckAndCreateAppFolders()
        {
            string path = Path.Combine(applicationPath, savedLyricsLocation);
            string pathImg = Path.Combine(applicationPath, savedImagesLocation);
            string pathLog = Path.Combine(applicationPath, logsLocation);
            //TODO: Add IOException handling

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (!Directory.Exists(pathImg))
            {
                Directory.CreateDirectory(pathImg);
            }

            if (!Directory.Exists(pathLog))
            {
                Directory.CreateDirectory(pathLog);
            }
        }

        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
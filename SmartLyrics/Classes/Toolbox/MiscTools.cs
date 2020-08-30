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
            string path = Path.Combine(applicationPath, Globals.savedLyricsLocation);
            string pathImg = Path.Combine(applicationPath, Globals.savedImagesLocation);
            //TODO: Add IOException handling

            if (Directory.Exists(path))
            {
                Log(Type.Info, "/Saved Lyrics directory exists!");
            }
            else
            {
                Directory.CreateDirectory(path);
                Log(Type.Info, "/Saved Lyrics directory doesn't exist, creating...");
            }

            if (Directory.Exists(pathImg))
            {
                Log(Type.Info, "/Saved Lyrics/ImageCache directory exists!");
            }
            else
            {
                Directory.CreateDirectory(pathImg);
                Log(Type.Info, "/Saved Lyrics/ImageCache directory doesn't exist, creating...");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetCurrentMethod()
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);

            return sf.GetMethod().Name;
        }
    }
}
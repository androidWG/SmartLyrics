using Android.Content;
using Android.OS;
using Android.Util;

namespace SmartLyrics.Services
{
    public class ProgressBinder : Binder
    {
        public ProgressBinder(DownloadService service)
        {
            Service = service;
        }

        public DownloadService Service { get; private set; }

        public int GetProgress()
        {
            return Service.GetProgress();
        }
    }

    public class DownloadServiceConnection : Java.Lang.Object, IServiceConnection
    {
        public bool IsConnected { get; private set; }
        public ProgressBinder Binder { get; private set; }

        private readonly SpotifyDownload mainActivity;

        public DownloadServiceConnection(SpotifyDownload activity)
        {
            IsConnected = false;
            Binder = null;
            mainActivity = activity;
        }

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            Log.WriteLine(LogPriority.Warn, "SmartLyrics", "ProgressBinder: Service connected");

            Binder = service as ProgressBinder;
            IsConnected = this.Binder != null;

            if (IsConnected)
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "ProgressBinder: Bound to " + name.ClassName);
            }
            else
            {
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "ProgressBinder: Unbound to " + name.ClassName);
            }
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            Log.WriteLine(LogPriority.Warn, "SmartLyrics", "ProgressBinder: Service disconnected");

            IsConnected = false;
            Binder = null;
        }

        public int GetProgress()
        {
            if (!IsConnected)
            {
                return 0;
            }

            return Binder.GetProgress();
        }
    }

}
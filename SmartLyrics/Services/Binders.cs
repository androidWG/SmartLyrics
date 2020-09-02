using Android.Content;
using Android.OS;
using static SmartLyrics.Common.Logging;

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

        public DownloadServiceConnection(SpotifyDownload activity)
        {
            IsConnected = false;
            Binder = null;
        }

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            Log(Type.Event, "Service connected");

            Binder = service as ProgressBinder;
            IsConnected = this.Binder != null;

            if (IsConnected)
            {
                Log(Type.Info, "Bound to " + name.ClassName);
            }
            else
            {
                Log(Type.Info, "Unbound to " + name.ClassName);
            }
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            Log(Type.Event, "Service disconnected");

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
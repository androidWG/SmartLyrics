using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.AppCenter.Analytics;

namespace SmartLyrics
{
    class Logging
    {
        public static void Log(LogPriority priority, string category, string message)
        {
            Android.Util.Log.WriteLine(priority, category, message);
            Analytics.TrackEvent(message);
        }
    }
}
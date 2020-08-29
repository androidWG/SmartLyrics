using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;

namespace SmartLyrics.Common
{
    class Logging
    {
        public static void Log(LogPriority priority, string tag, string message)
        {
            Android.Util.Log.WriteLine(LogPriority.Info, "SmartLyrics", "file_name_here.cs: ");
        }
    }
}
using Android.Content;
using Android.OS;
using Android.Support.V4.Content;
using Android.Util;

using Microsoft.AppCenter.Crashes;

using System;
using System.Threading.Tasks;

namespace SmartLyrics.Common
{
    class PermissionChecking
    {
        //! non-static method independent of Activity. I finally made it. it probably doesn't work
        //! returns 0 if successfully granted, 1 if the code calling this method
        //! needs to wait and 2 if an exception was cought
        public static async Task<int> CheckAndSetPermissions(string permission, Context context)
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "PermissionChecking.cs: Started CheckAndSetPermissions method");

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    if (ContextCompat.CheckSelfPermission(context, permission) == (int)Android.Content.PM.Permission.Granted)
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "PermissionChecking.cs: Permission for" + permission + " already granted");
                        return 0;
                    }
                    else
                    {
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "PermissionChecking.cs: Rationale needed, trying to get permission...");
                        return 1;
                    }
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "PermissionChecking.cs: Exception caught! " + ex.Message);
                Crashes.TrackError(ex);

                return 2;
            }
        }
    }
}
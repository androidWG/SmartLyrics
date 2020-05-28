using Android.Content;
using Android.OS;
using Android.Support.V4.Content;
using Android.Util;

using Microsoft.AppCenter.Crashes;

using System;
using System.Threading.Tasks;

namespace SmartLyrics.Common
{
    internal class PermissionChecking
    {
        //! non-static method independent of Activity. I finally made it. it probably doesn't work
        //! returns 0 if successfully granted, 1 if the code calling this method
        //! needs to wait and 2 if an exception was cought
        public static async Task<int> CheckAndSetPermissions(string permission, Context context)
        {
            Log.WriteLine(LogPriority.Verbose, "PermissionChecking", "CheckAndSetPermissions: Started CheckAndSetPermissions method");

            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    if (ContextCompat.CheckSelfPermission(context, permission) == (int)Android.Content.PM.Permission.Granted)
                    {
                        Log.WriteLine(LogPriority.Info, "PermissionChecking", "CheckAndSetPermissions: Permission for" + permission + " already granted");
                        return 0;
                    }
                    else
                    {
                        Log.WriteLine(LogPriority.Info, "PermissionChecking", "CheckAndSetPermissions: Rationale needed, trying to get permission...");
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
                Log.WriteLine(LogPriority.Error, "PermissionChecking", "CheckAndSetPermissions: Exception caught while checking or getting permission!\n" + ex.ToString());
                Crashes.TrackError(ex);

                return 2;
            }
        }
    }
}
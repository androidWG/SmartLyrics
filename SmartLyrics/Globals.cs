using Android.App;
using Android.Content;

namespace SmartLyrics
{
    internal static class Globals
    {
        public static ISharedPreferences Prefs;
        
        public static readonly string ApplicationPath = Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
        public const string SavedLyricsLocation = "saved/";
        public const string SavedImagesLocation = "saved/imagecache/";

        #region Song Saving
        public const string DbFilename = ".lyricsdatabase";
        public const string RomanizedDbFilename = ".romanizeddatabase";

        public const string LyricsExtension = ".lyrics";
        public const string RomanizedExtension = ".romanized";
        public const string LogDatabaseExtension = ".logdb";

        public const string HeaderSuffix = "-header.jpg";
        public const string CoverSuffix = "-cover.jpg";
        #endregion

        #region API Default URLs
        public const string GeniusAuthHeader = "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28";
        public const string GeniusSearchUrl = "https://api.genius.com/search?q=";
        public const string GeniusApiUrl = "https://api.genius.com";

        public const string RomanizeConvertUrl = "https://romanization-service.herokuapp.com/convert";
        #endregion

        #region Song View
        public const int CoverRadius = 16;
        public const int HeaderBlur = 25;
        public const int SearchBlur = 25;
        #endregion

        #region Notification
        public const int NotificationId = 177013;
        public const string ChannelId = "auto_lyrics_detect_sl";
        public const int MaxLikeness = 12;
        #endregion

        #region Logging
        public const string LogsLocation = "logs/";
        public const string LogDateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        public const int LogFileMaxSize = 5000000;
        public const int LogFileMaxAge = -2;
        #endregion
    }
}
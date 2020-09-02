using Android.App;
using Android.Content;

namespace SmartLyrics
{
    internal class Globals
    {
        public static readonly string ApplicationPath = Application.Context.GetExternalFilesDir(null)?.AbsolutePath;

        public const string SavedLyricsLocation = "saved/";
        public const string SavedImagesLocation = "saved/imagecache/";
        public const string LogsLocation = "logs/";

        public const string DbFilename = ".lyricsdatabase";
        public const string RomanizedDbFilename = ".romanizeddatabase";

        public const string LyricsExtension = ".lyrics";
        public const string RomanizedExtension = ".romanized";
        public const string LogDatabaseExtension = ".logdb";

        public const string HeaderSuffix = "-header.jpg";
        public const string CoverSuffix = "-cover.jpg";

        public const string LogDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public const string GeniusAuthHeader = "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28";
        public const string GeniusSearchUrl = "https://api.genius.com/search?q=";
        public const string GeniusApiUrl = "https://api.genius.com";

        public const string RomanizeConvertUrl = "https://romanization-service.herokuapp.com/convert";

        public static ISharedPreferences Prefs;
        
        public const int CoverRadius = 16;
        public const int HeaderBlur = 25;
        public const int SearchBlur = 25;
    }
}
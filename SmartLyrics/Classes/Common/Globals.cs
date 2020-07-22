using Android.Content;

namespace SmartLyrics
{
    internal class Globals
    {
        public static readonly string applicationPath = Android.App.Application.Context.GetExternalFilesDir(null).AbsolutePath;

        public const string savedLyricsLocation = "saved/";
        public const string savedImagesLocation = "saved/imagecache/";
        public const string databaseLocation = "/.lyricsdatabase";
        public const string lyricsExtension = ".lyrics";
        public const string romanizedExtension = ".romanized";

        public const string headerSuffix = "-header.jpg";
        public const string coverSuffix = "-cover.jpg";

        public const string geniusAuthHeader = "Bearer nRYPbfZ164rBLiqfjoHQfz9Jnuc6VgFc2PWQuxIFVlydj00j4yqMaFml59vUoJ28";
        public const string geniusSearchURL = "https://api.genius.com/search?q=";
        public const string geniusAPIURL = "https://api.genius.com";

        public const string romanizeConvertURL = "https://romanization-service.herokuapp.com/convert";

        public static ISharedPreferences preferences;
    }
}
using Android.Util;

using Microsoft.AppCenter.Crashes;

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

using static SmartLyrics.Globals;

namespace SmartLyrics.Toolbox
{
    class DatabaseHandling
    {
        private static DataTable db = new DataTable("db");
        private static string path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation + databaseLocation);
        private static string lyricsPath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation);

        #region Toolbox
        //clear table and add correct columns
        internal static void InitializeTable()
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "DatabaseHandling.cs: Initializing DataTable...");

            db.Clear();
            db.Columns.Clear();

            db.Columns.Add("id", typeof(int));
            db.Columns.Add("title", typeof(string));
            db.Columns.Add("artist", typeof(string));
            db.Columns.Add("album", typeof(string));
            db.Columns.Add("featuredArtist", typeof(string));
            db.Columns.Add("cover", typeof(string));
            db.Columns.Add("header", typeof(string));
            db.Columns.Add("APIPath", typeof(string));
            db.Columns.Add("path", typeof(string));

            Log.WriteLine(LogPriority.Info, "SmartLyrics", "DatabaseHandling.cs: Finished initializing!");
        }

        internal static Common.Song DataRowToSong(DataRow dr)
        {
            Common.Song song = new Common.Song
            {
                id = (int)dr["id"],
                title = (string)dr["title"],
                artist = (string)dr["artist"],
                album = (string)dr["album"],
                featuredArtist = (string)dr["featuredArtist"],
                cover = (string)dr["cover"],
                header = (string)dr["header"],
                APIPath = (string)dr["APIPath"],
                path = (string)dr["path"]
            };

            return song;
        }

        internal static async Task<DataTable> ReadFromDatabaseFile(string path)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "DatabaseHandling.cs: Reading database from file...");
            DataTable _dt = new DataTable("db"); //name needs to be the same as the "db" variable
            
            //TODO: better error handling
            try
            {
                //initialize temp DataTable to import XML
                _dt.Columns.Add("id", typeof(int));
                _dt.Columns.Add("title", typeof(string));
                _dt.Columns.Add("artist", typeof(string));
                _dt.Columns.Add("album", typeof(string));
                _dt.Columns.Add("featuredArtist", typeof(string));
                _dt.Columns.Add("cover", typeof(string));
                _dt.Columns.Add("header", typeof(string));
                _dt.Columns.Add("APIPath", typeof(string));
                _dt.Columns.Add("path", typeof(string));

                if (File.Exists(path))
                {
                    using Stream s = new FileStream(path, FileMode.Open, FileAccess.Read);
                    _dt.ReadXml(s);
                }
            }
            catch (XmlException ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "DatabaseHandling.cs: Exception cought while trying reading database!\n" + ex.ToString());

                return null;
            }

            return _dt;
        }

        internal static async Task WriteLyrics(Common.Song songInfo)
        {
            Log.WriteLine(LogPriority.Verbose, "SmartLyrics", "DatabaseHandling.cs: Preparing to write lyrics to disk...");
            string _filepath = Path.Combine(lyricsPath, songInfo.id + lyricsExtension);

            try 
            {
                File.WriteAllText(_filepath, songInfo.lyrics);
                Log.WriteLine(LogPriority.Info, "SmartLyrics", "DatabaseHandling.cs: Wrote lyrics for {songInfo.title} to disk!");
            }
            catch (IOException ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Error, "SmartLyrics", "DatabaseHandling.cs: Error while writing lyrics to disk!\n" + ex.ToString());
            }
        }
        #endregion

        //writes a song to the saved lyrics database
        //returns true if successful
        public static async Task<bool> WriteInfoAndLyrics(Common.Song songInfo)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "DatabaseHandling.cs: Started WriteInfoAndLyrics method");

            InitializeTable();
            db = await ReadFromDatabaseFile(path);

            if (await GetSongFromTable(songInfo.id) == null)
            {
                db.Rows.Add(
                    songInfo.id,
                    songInfo.title,
                    songInfo.artist,
                    songInfo.album,
                    songInfo.featuredArtist,
                    songInfo.cover,
                    songInfo.header,
                    songInfo.APIPath,
                    songInfo.path);

                db.WriteXml(path);

                await WriteLyrics(songInfo);

                return true;
            }
            else
            {
                return false;
            }
        }

        public static async Task<List<Common.Song>> GetSongList()
        {
            if (File.Exists(path))
            {
                InitializeTable();
                db = await ReadFromDatabaseFile(path);

                List<Common.Song> _songs = new List<Common.Song>();

                foreach (DataRow dr in db.Rows)
                {
                    Common.Song _ = DataRowToSong(dr);
                    _songs.Add(_);
                }

                return _songs;
            }
            else
            {
                return null;
            }
        }

        public static async Task<Common.Song> GetSongFromTable(int id)
        {
            //Genius does not have a song with ID 0, if we recieve a request with
            //ID 0, immediately return null
            if (id == 0)
            {
                Log.WriteLine(LogPriority.Warn, "SmartLyrics", "DatabaseHandling.cs: Common.Song ID is 0, returning null...");
                return null;
            }
            else
            {
                Log.WriteLine(LogPriority.Verbose, "SmartLyrics", $"DatabaseHandling.cs: Attempting to find song with ID {id} on table...");

                DataRow[] _;
                try
                {
                    _ = db.Select("id = " + id);
                }
                catch (EvaluateException)
                {
                    return null;
                }

                if (_ != null && _.Length != 0)
                {
                    DataRow dr = _[0];

                    Common.Song song = new Common.Song
                    {
                        id = (int)dr["id"],
                        title = (string)dr["title"],
                        artist = (string)dr["artist"],
                        album = (string)dr["album"],
                        featuredArtist = (string)dr["featuredArtist"],
                        cover = (string)dr["cover"],
                        header = (string)dr["header"],
                        APIPath = (string)dr["APIPath"],
                        path = (string)dr["path"]
                    };

                    return song;
                }
                else
                {
                    Log.WriteLine(LogPriority.Info, "SmartLyrics", "DatabaseHandling.cs: Did not find song, returning null...");
                    return null;
                }
            }
        }
    }
}
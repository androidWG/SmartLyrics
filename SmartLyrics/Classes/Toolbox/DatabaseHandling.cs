using Android.Util;

using Microsoft.AppCenter.Crashes;

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

using SmartLyrics.Common;
using static SmartLyrics.Globals;

namespace SmartLyrics.Toolbox
{
    internal class DatabaseHandling
    {
        private static DataTable db = new DataTable("db");
        private static readonly string path = Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null).AbsolutePath, savedLyricsLocation + databaseLocation);
        private static readonly string lyricsPath = Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null).AbsolutePath, savedLyricsLocation);

        #region Toolbox
        //clear table and add correct columns
        internal static void InitializeTable()
        {
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

            Log.WriteLine(LogPriority.Info, "DatabaseHandling", "InitializeTable: Finished initializing datatable!");
        }

        internal static Song DataRowToSong(DataRow dr)
        {
            Song song = new Song
            {
                Id = (int)dr["id"],
                Title = (string)dr["title"],
                Artist = (string)dr["artist"],
                Album = (string)dr["album"],
                FeaturedArtist = (string)dr["featuredArtist"],
                Cover = (string)dr["cover"],
                Header = (string)dr["header"],
                APIPath = (string)dr["APIPath"],
                Path = (string)dr["path"]
            };

            return song;
        }

        internal static async Task<DataTable> ReadFromDatabaseFile(string path)
        {
            Log.WriteLine(LogPriority.Info, "DatabaseHandling", "ReadFromDatabaseFile: Reading database from file...");
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
                Log.WriteLine(LogPriority.Info, "DatabaseHandling", "ReadFromDatabaseFile: Exception cought while trying reading database!\n" + ex.ToString());

                return null;
            }

            return _dt;
        }

        internal static async Task WriteLyrics(Song songInfo)
        {
            Log.WriteLine(LogPriority.Verbose, "DatabaseHandling", "WriteLyrics: Preparing to write lyrics to disk...");
            string _filepath = Path.Combine(lyricsPath, songInfo.Id + lyricsExtension);

            try 
            {
                File.WriteAllText(_filepath, songInfo.Lyrics);
                Log.WriteLine(LogPriority.Info, "DatabaseHandling", "WriteLyrics: Wrote lyrics for {songInfo.title} to disk!");
            }
            catch (IOException ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Error, "DatabaseHandling", "WriteLyrics: Error while writing lyrics to disk!\n" + ex.ToString());
            }
        }
        #endregion

        //writes a song to the saved lyrics database
        //returns true if successful
        public static async Task<bool> WriteInfoAndLyrics(Song songInfo)
        {
            Log.WriteLine(LogPriority.Info, "DatabaseHandling", "WriteInfoAndLyrics: Started WriteInfoAndLyrics method");

            InitializeTable();
            db = await ReadFromDatabaseFile(path);

            if (await GetSongFromTable(songInfo.Id) == null)
            {
                db.Rows.Add(
                    songInfo.Id,
                    songInfo.Title,
                    songInfo.Artist,
                    songInfo.Album,
                    songInfo.FeaturedArtist,
                    songInfo.Cover,
                    songInfo.Header,
                    songInfo.APIPath,
                    songInfo.Path);

                db.WriteXml(path);

                await WriteLyrics(songInfo);

                return true;
            }
            else
            {
                return false;
            }
        }

        public static async Task<List<Song>> GetSongList()
        {
            if (File.Exists(path))
            {
                InitializeTable();
                db = await ReadFromDatabaseFile(path);

                List<Song> _songs = new List<Song>();

                foreach (DataRow dr in db.Rows)
                {
                    Song _ = DataRowToSong(dr);
                    _songs.Add(_);
                }

                return _songs;
            }
            else
            {
                return null;
            }
        }

        public static async Task<Song> GetSongFromTable(int id)
        {
            //Genius does not have a song with ID 0, if we recieve a request with
            //ID 0, immediately return null
            if (id == 0)
            {
                Log.WriteLine(LogPriority.Warn, "DatabaseHandling", "GetSongFromTable: Song ID is 0, returning null...");
                return null;
            }
            else
            {
                Log.WriteLine(LogPriority.Verbose, "DatabaseHandling", $"GetSongFromTable: Attempting to find song with ID {id} on table...");

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

                    Song song = new Song
                    {
                        Id = (int)dr["id"],
                        Title = (string)dr["title"],
                        Artist = (string)dr["artist"],
                        Album = (string)dr["album"],
                        FeaturedArtist = (string)dr["featuredArtist"],
                        Cover = (string)dr["cover"],
                        Header = (string)dr["header"],
                        APIPath = (string)dr["APIPath"],
                        Path = (string)dr["path"]
                    };

                    return song;
                }
                else
                {
                    Log.WriteLine(LogPriority.Info, "DatabaseHandling", "GetSongFromTable: Did not find song, returning null...");
                    return null;
                }
            }
        }
    }
}
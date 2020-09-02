using FFImageLoading;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

using SmartLyrics.Common;
using SmartLyrics.Toolbox;

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace SmartLyrics.IO
{
    internal class Database
    {
        private static DataTable db = new DataTable("db");
        private static DataTable rdb = new DataTable("rdb");

        private static readonly string DbPath = Path.Combine(Globals.ApplicationPath, Globals.SavedLyricsLocation + Globals.DbFilename);
        private static readonly string RomanizedDbPath = Path.Combine(Globals.ApplicationPath, Globals.SavedLyricsLocation + Globals.RomanizedDbFilename);
        private static readonly string PathImg = Path.Combine(Globals.ApplicationPath, Globals.SavedImagesLocation);
        private static readonly string LyricsPath = Path.Combine(Globals.ApplicationPath, Globals.SavedLyricsLocation);

        //! See https://github.com/AndroidWG/SmartLyrics/wiki/Saved-Lyrics-Database for more information on how the database works.

        #region Toolbox
        //Clear table and add correct columns
        internal static void InitializeTables()
        {
            //TODO: Add error handling
            db.Clear();
            db.Columns.Clear();

            rdb.Clear();
            rdb.Columns.Clear();

            db.Columns.Add("id", typeof(int));
            db.Columns.Add("title", typeof(string));
            db.Columns.Add("artist", typeof(string));
            db.Columns.Add("album", typeof(string));
            db.Columns.Add("featuredArtist", typeof(string));
            db.Columns.Add("cover", typeof(string));
            db.Columns.Add("header", typeof(string));
            db.Columns.Add("romanized", typeof(bool));
            db.Columns.Add("APIPath", typeof(string));
            db.Columns.Add("path", typeof(string));

            rdb.Columns.Add("id", typeof(int));
            rdb.Columns.Add("title", typeof(string));
            rdb.Columns.Add("artist", typeof(string));
            rdb.Columns.Add("album", typeof(string));
            rdb.Columns.Add("featuredArtist", typeof(string));

            Logging.Log(Logging.Type.Info, "Finished initializing datatable!");
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
                Romanized = (bool)dr["romanized"],
                ApiPath = (string)dr["APIPath"],
                Path = (string)dr["path"]
            };

            return song;
        }

        internal static RomanizedSong DataRowToRomanizedSong(DataRow dr)
        {
            RomanizedSong song = new RomanizedSong
            {
                Id = (int)dr["id"],
                Title = (string)dr["title"],
                Artist = (string)dr["artist"],
                Album = (string)dr["album"],
                FeaturedArtist = (string)dr["featuredArtist"]
            };

            return song;
        }
        #endregion

        #region Reading
        internal static async Task<DataTable> ReadDatabaseFile(string path)
        {
            Logging.Log(Logging.Type.Info, "Reading database from file...");
            DataTable dt = new DataTable("db"); //name needs to be the same as the "db" variable

            //TODO: Better error hadnling
            try
            {
                //initialize temp DataTable to import XML
                dt.Columns.Add("id", typeof(int));
                dt.Columns.Add("title", typeof(string));
                dt.Columns.Add("artist", typeof(string));
                dt.Columns.Add("album", typeof(string));
                dt.Columns.Add("featuredArtist", typeof(string));
                dt.Columns.Add("cover", typeof(string));
                dt.Columns.Add("header", typeof(string));
                dt.Columns.Add("romanized", typeof(bool));
                dt.Columns.Add("APIPath", typeof(string));
                dt.Columns.Add("path", typeof(string));

                if (File.Exists(path))
                {
                    using Stream s = new FileStream(path, FileMode.Open, FileAccess.Read);
                    dt.ReadXml(s);
                }
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string> {
                    { "DBPath", path },
                    { "Exception", ex.ToString() }
                });
                Logging.Log(Logging.Type.Error, $"Exception cought while trying to read database in path {path}!\n{ex}");

                return null;
            }

            return dt;
        }

        internal static async Task<DataTable> ReadRomanizedDatabaseFile(string path)
        {
            Logging.Log(Logging.Type.Info, "Reading database from file...");
            DataTable rdt = new DataTable("db"); //name needs to be the same as the "db" variable

            //TODO: Better error hadnling
            try
            {
                //initialize temp DataTable to import XML
                rdt.Columns.Add("id", typeof(int));
                rdt.Columns.Add("title", typeof(string));
                rdt.Columns.Add("artist", typeof(string));
                rdt.Columns.Add("album", typeof(string));
                rdt.Columns.Add("featuredArtist", typeof(string));

                if (File.Exists(path))
                {
                    using Stream s = new FileStream(path, FileMode.Open, FileAccess.Read);
                    rdt.ReadXml(s);
                }
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string> {
                    { "DBPath", path },
                    { "Exception", ex.ToString() }
                });
                Logging.Log(Logging.Type.Error, $"Exception cought while trying to read database in path {path}!\n{ex}");

                return null;
            }

            return rdt;
        }

        public static async Task<SongBundle> ReadLyrics(int id)
        {
            SongBundle song = await GetSongFromTable(id);
            string path = Path.Combine(Globals.ApplicationPath, Globals.SavedLyricsLocation + song.Normal.Id);

            //songInfo.Normal is already loaded (from SavedLyrics activity), load lyrics and images from disk
            //Load songInfo.Romanized lyrics if songInfo.Romanized lyrics were saved
            StreamReader sr = File.OpenText(path + Globals.LyricsExtension);
            song.Normal.Lyrics = await sr.ReadToEndAsync();

            if (song.Romanized != null)
            {
                sr.Dispose();
                sr = File.OpenText(path + Globals.RomanizedExtension);

                song.Romanized.Lyrics = await sr.ReadToEndAsync();
            }

            Logging.Log(Logging.Type.Event, "Read lyrics from file");
            sr.Dispose(); //Dispose/close manually since we're not using "using"

            return song;
        }

        public static async Task<List<SongBundle>> GetSongList()
        {
            if (File.Exists(DbPath))
            {
                InitializeTables();
                db = await ReadDatabaseFile(DbPath);
                rdb = await ReadRomanizedDatabaseFile(RomanizedDbPath);

                List<SongBundle> songs = new List<SongBundle>();

                foreach (DataRow dr in db.Rows)
                {
                    Song song = DataRowToSong(dr);
                    RomanizedSong rSong = null;

                    if (song.Romanized)
                    {
                        rSong = await GetRomanizedSongFromTable(song.Id);
                    }

                    songs.Add(new SongBundle(song, rSong));
                }

                return songs;
            }
            else
            {
                return null;
            }
        }

        public static async Task<SongBundle> GetSongFromTable(int id)
        {
            InitializeTables();
            db = await ReadDatabaseFile(DbPath);
            rdb = await ReadRomanizedDatabaseFile(RomanizedDbPath);

            //Genius does not have a song with ID 0, if we recieve a request with
            //ID 0, immediately return null
            if (id == 0)
            {
                return null;
            }
            else
            {
                Logging.Log(Logging.Type.Info, $"Attempting to find song with ID {id} on table...");

                DataRow[] _;
                try
                {
                    _ = db.Select("id = " + id);

                    if (_.Length != 0)
                    {
                        DataRow dr = _[0];
                        Song song = DataRowToSong(dr);
                        RomanizedSong romanized = null;

                        if (song.Romanized)
                        {
                            romanized = await GetRomanizedSongFromTable(id);
                        }

                        return new SongBundle(song, romanized);
                    }
                    else
                    {
                        Logging.Log(Logging.Type.Info, "Did not find song, returning null...");
                        return null;
                    }
                }
                catch (EvaluateException)
                {
                    //TODO: Add error hadnling
                    return null;
                }
            }
        }

        public static async Task<RomanizedSong> GetRomanizedSongFromTable(int id)
        {
            Logging.Log(Logging.Type.Processing, $"Attempting to find romanized song with ID {id} on table...");

            DataRow[] _;
            try
            {
                _ = rdb.Select("id = " + id);

                if (_.Length != 0)
                {
                    DataRow dr = _[0];
                    RomanizedSong song = DataRowToRomanizedSong(dr);

                    return song;
                }
                else
                {
                    Logging.Log(Logging.Type.Processing, "Did not find song, returning null...");
                    return null;
                }
            }
            catch (EvaluateException)
            {
                //TODO: Add error hadnling
                return null;
            }
        }
        #endregion

        #region Writing
        internal static async Task WriteImages(Song songInfo)
        {
            //Header and cover images are always saved on a separate folder with
            //the song's ID to identify it.
            string pathHeader = Path.Combine(PathImg, songInfo.Id + Globals.HeaderSuffix);
            string pathCover = Path.Combine(PathImg, songInfo.Id + Globals.CoverSuffix);

            if (Globals.Prefs.GetBoolean("save_header", true))
            {
                using (FileStream fs = File.Create(pathHeader))
                {
                    Stream header = await ImageService.Instance.LoadUrl(songInfo.Header).AsJPGStreamAsync();
                    header.Seek(0, SeekOrigin.Begin);
                    header.CopyTo(fs);

                    Logging.Log(Logging.Type.Info, "Saved header image");
                }
            }

            using (FileStream fs = File.Create(pathCover))
            {
                Stream cover = await ImageService.Instance.LoadUrl(songInfo.Cover).AsJPGStreamAsync();
                cover.Seek(0, SeekOrigin.Begin);
                cover.CopyTo(fs);

                Logging.Log(Logging.Type.Info, "Saved cover image");
            }
        }

        // Writes a song to the saved lyrics database.
        // Returns true if successful.
        //TODO: Add ability to return an error, song already saved or saved successfully messages
        public static async Task<bool> WriteInfoAndLyrics(SongBundle song)
        {
            await MiscTools.CheckAndCreateAppFolders();

            InitializeTables();
            db = await ReadDatabaseFile(DbPath);
            rdb = await ReadRomanizedDatabaseFile(RomanizedDbPath);

            try
            {
                if (await GetSongFromTable(song.Normal.Id) == null)
                {
                    // Write lyrics to file
                    string filepath = Path.Combine(LyricsPath, song.Normal.Id + Globals.LyricsExtension);
                    File.WriteAllText(filepath, song.Normal.Lyrics);

                    if (song.Romanized != null)
                    {
                        string romanizedFilepath = Path.Combine(LyricsPath, song.Normal.Id + Globals.RomanizedExtension);
                        File.WriteAllText(romanizedFilepath, song.Romanized.Lyrics);

                        song.Normal.Romanized = true;

                        rdb.Rows.Add(
                            song.Romanized.Id,
                            song.Romanized.Title,
                            song.Romanized.Artist,
                            song.Romanized.Album,
                            song.Romanized.FeaturedArtist);
                        rdb.WriteXml(RomanizedDbPath);
                    }
                    
                    await WriteImages(song.Normal);

                    db.Rows.Add(
                        song.Normal.Id,
                        song.Normal.Title,
                        song.Normal.Artist,
                        song.Normal.Album,
                        song.Normal.FeaturedArtist,
                        song.Normal.Cover,
                        song.Normal.Header,
                        song.Normal.Romanized,
                        song.Normal.ApiPath,
                        song.Normal.Path);
                    db.WriteXml(DbPath);

                    Logging.Log(Logging.Type.Event, $"Wrote song {song.Normal.Id} to file");
                    Analytics.TrackEvent("Wrote song to file", new Dictionary<string, string> {
                        { "SongID", song.Normal.Id.ToString() },
                        { "DBSize", db.Rows.Count.ToString() },
                        { "RomanizedDBSize", rdb.Rows.Count.ToString() }
                    });
                    return true;
                }
                else { return false; }
            }
            catch (IOException ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string> {
                    { "SongID", song.Normal.Id.ToString() },
                    { "Exception", ex.ToString() }
                });
                Logging.Log(Logging.Type.Error, "Exception while writing lyrics to disk!\n" + ex);

                return false;
            }
            catch (NullReferenceException ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string> {
                    { "SongID", song.Normal.Id.ToString() },
                    { "Exception", ex.ToString() }
                });
                Logging.Log(Logging.Type.Error, "NullReferenceException while writing lyrics to disk! Reload song and try again.\n" + ex);

                return false;
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex, new Dictionary<string, string> {
                    { "SongID", song.Normal.Id.ToString() },
                    { "Exception", ex.ToString() }
                });
                Logging.Log(Logging.Type.Error, "Unkown error while writing song to disk!\n" + ex);

                return false;
            }
        }
        #endregion
    }
}
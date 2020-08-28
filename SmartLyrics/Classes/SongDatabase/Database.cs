using Android.Util;

using FFImageLoading;
using Microsoft.AppCenter.Crashes;

using SmartLyrics.Common;

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

using static SmartLyrics.Globals;
using SmartLyrics.Toolbox;

namespace SmartLyrics.IO
{
    internal class Database
    {
        private static DataTable db = new DataTable("db");
        private static DataTable rdb = new DataTable("rdb");

        private static readonly string DBPath = Path.Combine(applicationPath, savedLyricsLocation + DBLocation);
        private static readonly string romanizedDBPath = Path.Combine(applicationPath, savedLyricsLocation + romanizedDBLocation);
        private static readonly string pathImg = Path.Combine(applicationPath, savedImagesLocation);
        private static readonly string lyricsPath = Path.Combine(applicationPath, savedLyricsLocation);

        //! See https://github.com/AndroidWG/SmartLyrics/wiki/Saved-Lyrics-Database for more information on how the database works.

        #region Toolbox
        //Clear table and add correct columns
        internal static void InitializeTables()
        {
            //EX: Add error handling
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
                Romanized = (bool)dr["romanized"],
                APIPath = (string)dr["APIPath"],
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
            Log.WriteLine(LogPriority.Info, "DatabaseHandling", "ReadFromDatabaseFile: Reading database from file...");
            DataTable _dt = new DataTable("db"); //name needs to be the same as the "db" variable

            //EX: Better error hadnling
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
                _dt.Columns.Add("romanized", typeof(bool));
                _dt.Columns.Add("APIPath", typeof(string));
                _dt.Columns.Add("path", typeof(string));

                if (File.Exists(path))
                {
                    using Stream s = new FileStream(path, FileMode.Open, FileAccess.Read);
                    _dt.ReadXml(s);
                }
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Info, "DatabaseHandling", "ReadFromDatabaseFile: Exception cought while trying reading database!\n" + ex.ToString());

                return null;
            }

            return _dt;
        }

        internal static async Task<DataTable> ReadRomanizedDatabaseFile(string path)
        {
            Log.WriteLine(LogPriority.Info, "DatabaseHandling", "ReadFromDatabaseFile: Reading database from file...");
            DataTable _rdt = new DataTable("db"); //name needs to be the same as the "db" variable

            //EX: Better error hadnling
            try
            {
                //initialize temp DataTable to import XML
                _rdt.Columns.Add("id", typeof(int));
                _rdt.Columns.Add("title", typeof(string));
                _rdt.Columns.Add("artist", typeof(string));
                _rdt.Columns.Add("album", typeof(string));
                _rdt.Columns.Add("featuredArtist", typeof(string));

                if (File.Exists(path))
                {
                    using Stream s = new FileStream(path, FileMode.Open, FileAccess.Read);
                    _rdt.ReadXml(s);
                }
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Info, "DatabaseHandling", "ReadFromDatabaseFile: Exception cought while trying reading database!\n" + ex.ToString());

                return null;
            }

            return _rdt;
        }

        public static async Task<SongBundle> ReadLyrics(int id)
        {
            SongBundle song = await GetSongFromTable(id);
            string path = Path.Combine(applicationPath, savedLyricsLocation + song.Normal.Id);

            //songInfo.Normal is already loaded (from SavedLyrics activity), load lyrics and images from disk
            //Load songInfo.Romanized lyrics if songInfo.Romanized lyrics were saved
            StreamReader sr = File.OpenText(path + lyricsExtension);
            song.Normal.Lyrics = await sr.ReadToEndAsync();

            if (song.Romanized != null)
            {
                sr.Dispose();
                sr = File.OpenText(path + romanizedExtension);

                song.Romanized.Lyrics = await sr.ReadToEndAsync();
            }

            Log.WriteLine(LogPriority.Verbose, "MainActivity", "ReadFromFile: Read lyrics from file(s)");
            sr.Dispose(); //Dispose/close manually since we're not using "using"

            return song;
        }

        public static async Task<List<SongBundle>> GetSongList()
        {
            if (File.Exists(DBPath))
            {
                InitializeTables();
                db = await ReadDatabaseFile(DBPath);
                rdb = await ReadRomanizedDatabaseFile(romanizedDBPath);

                List<SongBundle> _songs = new List<SongBundle>();

                foreach (DataRow dr in db.Rows)
                {
                    Song song = DataRowToSong(dr);
                    RomanizedSong rSong = null;

                    if (song.Romanized)
                    {
                        rSong = await GetRomanizedSongFromTable(song.Id);
                    }

                    _songs.Add(new SongBundle(song, rSong));
                }

                return _songs;
            }
            else
            {
                return null;
            }
        }

        public static async Task<SongBundle> GetSongFromTable(int id)
        {
            InitializeTables();
            db = await ReadDatabaseFile(DBPath);
            rdb = await ReadRomanizedDatabaseFile(romanizedDBPath);

            //Genius does not have a song with ID 0, if we recieve a request with
            //ID 0, immediately return null
            if (id == 0)
            {
                return null;
            }
            else
            {
                Log.WriteLine(LogPriority.Verbose, "DatabaseHandling", $"GetSongFromTable: Attempting to find song with ID {id} on table...");

                DataRow[] _;
                try
                {
                    _ = db.Select("id = " + id);

                    if (_ != null && _.Length != 0)
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
                        Log.WriteLine(LogPriority.Info, "DatabaseHandling", "GetSongFromTable: Did not find song, returning null...");
                        return null;
                    }
                }
                catch (EvaluateException)
                {
                    //EX: Add error hadnling
                    return null;
                }
            }
        }

        public static async Task<RomanizedSong> GetRomanizedSongFromTable(int id)
        {
            Log.WriteLine(LogPriority.Verbose, "DatabaseHandling", $"GetSongFromTable: Attempting to find romanized song with ID {id} on table...");

            DataRow[] _;
            try
            {
                _ = rdb.Select("id = " + id);

                if (_ != null && _.Length != 0)
                {
                    DataRow dr = _[0];
                    RomanizedSong song = DataRowToRomanizedSong(dr);

                    return song;
                }
                else
                {
                    Log.WriteLine(LogPriority.Info, "DatabaseHandling", "GetSongFromTable: Did not find song, returning null...");
                    return null;
                }
            }
            catch (EvaluateException)
            {
                //EX: Add error hadnling
                return null;
            }
        }
        #endregion

        #region Writing
        internal static async Task WriteImages(Song songInfo)
        {
            //Header and cover images are always saved on a separate folder with
            //the song's ID to identify it.
            string pathHeader = Path.Combine(pathImg, songInfo.Id + headerSuffix);
            string pathCover = Path.Combine(pathImg, songInfo.Id + coverSuffix);

            if (prefs.GetBoolean("save_header", true))
            {
                using (FileStream fs = File.Create(pathHeader))
                {
                    Stream header = await ImageService.Instance.LoadUrl(songInfo.Header).AsJPGStreamAsync();
                    header.Seek(0, SeekOrigin.Begin);
                    header.CopyTo(fs);

                    Log.WriteLine(LogPriority.Info, "MainActivity", "SaveSong: Saved header image");
                }
            }

            using (FileStream fs = File.Create(pathCover))
            {
                Stream cover = await ImageService.Instance.LoadUrl(songInfo.Cover).AsJPGStreamAsync();
                cover.Seek(0, SeekOrigin.Begin);
                cover.CopyTo(fs);

                Log.WriteLine(LogPriority.Info, "MainActivity", "SaveSong: Saved cover image");
            }
        }

        // Writes a song to the saved lyrics database.
        // Returns true if successful.
        //TODO: Add ability to return an error, song already saved or saved successfully messages
        public static async Task<bool> WriteInfoAndLyrics(SongBundle song)
        {
            await MiscTools.CheckAndCreateAppFolders();

            InitializeTables();
            db = await ReadDatabaseFile(DBPath);
            rdb = await ReadRomanizedDatabaseFile(romanizedDBPath);

            try
            {
                if (await GetSongFromTable(song.Normal.Id) == null)
                {
                    // Write lyrics to file
                    string _filepath = Path.Combine(lyricsPath, song.Normal.Id + lyricsExtension);
                    File.WriteAllText(_filepath, song.Normal.Lyrics);

                    if (song.Romanized != null)
                    {
                        string _romanizedFilepath = Path.Combine(lyricsPath, song.Normal.Id + romanizedExtension);
                        File.WriteAllText(_romanizedFilepath, song.Romanized.Lyrics);

                        song.Normal.Romanized = true;

                        rdb.Rows.Add(
                            song.Romanized.Id,
                            song.Romanized.Title,
                            song.Romanized.Artist,
                            song.Romanized.Album,
                            song.Romanized.FeaturedArtist);
                        rdb.WriteXml(romanizedDBPath);
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
                        song.Normal.APIPath,
                        song.Normal.Path);
                    db.WriteXml(DBPath);

                    Log.WriteLine(LogPriority.Info, "DatabaseHandling", $"WriteLyrics: Wrote song {song.Normal.Title} to disk");
                    return true;
                }
                else { return false; }
            }
            catch (IOException ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Error, "DatabaseHandling", "WriteSong: Exception while writing lyrics to disk!\n" + ex.ToString());

                return false;
            }
            catch (NullReferenceException ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Error, "DatabaseHandling", "WriteSong: NullReferenceException while writing lyrics to disk! Reload song and try again.\n" + ex.ToString());

                return false;
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Error, "DatabaseHandling", "WriteSong: Unkown error while writing song to disk!\n" + ex.ToString());

                return false;
            }
        }
        #endregion
    }
}
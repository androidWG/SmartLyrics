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

namespace SmartLyrics.Toolbox
{
    internal class DatabaseHandling
    {
        private static DataTable db = new DataTable("db");

        private static readonly string path = Path.Combine(applicationPath, savedLyricsLocation + databaseLocation);
        private static readonly string pathImg = Path.Combine(applicationPath, savedImagesLocation);
        private static readonly string lyricsPath = Path.Combine(applicationPath, savedLyricsLocation);

        //! See https://github.com/AndroidWG/SmartLyrics/wiki/Saved-Lyrics-Database for more information on how the database works.

        #region Toolbox
        //Clear table and add correct columns
        internal static void InitializeTable()
        {
            //TODO: Add error handling
            db.Clear();
            db.Columns.Clear();

            db.Columns.Add("id", typeof(int));
            db.Columns.Add("title", typeof(string));
            db.Columns.Add("artist", typeof(string));
            db.Columns.Add("album", typeof(string));
            db.Columns.Add("featuredArtist", typeof(string));
            db.Columns.Add("cover", typeof(string));
            db.Columns.Add("header", typeof(string));
            db.Columns.Add("romanized", typeof(Song));
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
                Romanized = (Song)dr["romanized"],
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
                _dt.Columns.Add("romanized", typeof(Song));
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
        #endregion

        // Writes a song to the saved lyrics database.
        // Returns true if successful.
        public static async Task<bool> WriteInfoAndLyrics(Song songInfo)
        {
            await MiscTools.CheckAndCreateAppFolders();

            InitializeTable();
            db = await ReadFromDatabaseFile(path);

            try
            {
                if (await GetSongFromTable(songInfo.Id) == null)
                {
                    // Write lyrics to file
                    string _filepath = Path.Combine(lyricsPath, songInfo.Id + lyricsExtension);
                    File.WriteAllText(_filepath, songInfo.Lyrics);

                    if (songInfo.Romanized != null)
                    {
                        string _romanizedFilepath = Path.Combine(lyricsPath, songInfo.Id + romanizedExtension);
                        File.WriteAllText(_romanizedFilepath, songInfo.Romanized.Lyrics);
                    }
                    
                    await WriteImages(songInfo);

                    //Purge romanized lyrics after being saved in a separate file
                    songInfo.Romanized.Lyrics = "";

                    db.Rows.Add(
                        songInfo.Id,
                        songInfo.Title,
                        songInfo.Artist,
                        songInfo.Album,
                        songInfo.FeaturedArtist,
                        songInfo.Cover,
                        songInfo.Header,
                        songInfo.Romanized,
                        songInfo.APIPath,
                        songInfo.Path);

                    db.WriteXml(path);

                    Log.WriteLine(LogPriority.Info, "DatabaseHandling", $"WriteLyrics: Wrote song {songInfo.Title} to disk");
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
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
                Log.WriteLine(LogPriority.Error, "DatabaseHandling", "WriteSong: Unkown error while writing song to disk!\n" + ex.ToString());

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
                    Song song = DataRowToSong(dr);

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
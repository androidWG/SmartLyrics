using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.AppCenter.Crashes;
using static SmartLyrics.Globals;

namespace SmartLyrics.Common
{
    class DatabaseHandling
    {
        private static DataTable db = new DataTable("savedSongs");

        //writes a song to the saved lyrics database
        //returns true if successful
        public static async Task<bool> WriteToTable(Song songInfo)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", "DatabaseHandling.cs: Started WriteToTable method");

            string path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation + databaseLocation);
            if (await GetSongFromTable(songInfo.id) == null)
            {
                db.Columns.Add("id", typeof(int));
                db.Columns.Add("title", typeof(string));
                db.Columns.Add("artist", typeof(string));
                db.Columns.Add("album", typeof(string));
                db.Columns.Add("featuredArtist", typeof(string));
                db.Columns.Add("cover", typeof(string));
                db.Columns.Add("header", typeof(string));
                db.Columns.Add("APIPath", typeof(string));
                db.Columns.Add("path", typeof(string));
                db.Columns.Add("lyrics", typeof(string));

                db.Clear();
                if (File.Exists(path))
                {
                    //TODO: better error handling
                    try
                    {
                        using Stream s = new FileStream(path, FileMode.Open, FileAccess.Read);
                        db.ReadXml(s);
                    }
                    catch (XmlException ex)
                    {
                        Crashes.TrackError(ex);
                        Log.WriteLine(LogPriority.Info, "SmartLyrics", "DatabaseHandling.cs: Exception cought! "+ ex.ToString());
                    }
                }

                db.Rows.Add(
                    songInfo.id,
                    songInfo.title,
                    songInfo.artist,
                    songInfo.album,
                    songInfo.featuredArtist,
                    songInfo.cover,
                    songInfo.header,
                    songInfo.APIPath,
                    songInfo.path,
                    songInfo.lyrics);

                db.WriteXml(path);

                return true;
            }
            else
            {
                return false;
            }
        }

        public static async Task<Song> GetSongFromTable(int id)
        {
            Log.WriteLine(LogPriority.Info, "SmartLyrics", $"DatabaseHandling.cs: Attempting to find song with ID {id} on table...");

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
                    id = (int)dr["id"],
                    title = (string)dr["title"],
                    artist = (string)dr["artist"],
                    album = (string)dr["album"],
                    featuredArtist = (string)dr["featuredArtist"],
                    cover = (string)dr["cover"],
                    header = (string)dr["header"],
                    APIPath = (string)dr["APIPath"],
                    path = (string)dr["path"],
                    lyrics = (string)dr["lyrics"],
                };

                return song;
            }
            else
            {
                return null;
            }
        }
    }
}
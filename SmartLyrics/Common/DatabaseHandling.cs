using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using static SmartLyrics.Globals;

namespace SmartLyrics.Common
{
    class DatabaseHandling
    {
        public static async Task WriteToTable(Song songInfo)
        {
            DataTable db = new DataTable();
            
            string path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, savedLyricsLocation + databaseLocation);
            if (File.Exists(path))
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
                
                using Stream s = new FileStream(path, FileMode.Open, FileAccess.Read);
                db.ReadXml(s);

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
            }
        }
    }
}
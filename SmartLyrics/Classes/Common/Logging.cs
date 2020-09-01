using Android.Util;
using Mono.Data.Sqlite;
using SmartLyrics.Toolbox;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static SmartLyrics.Globals;

namespace SmartLyrics.Common
{
    class Logging
    {
        private static SqliteConnection sql;
        private static string filepath = "";

        public enum Priority
        {
            Debug,
            Verbose,
            Warn,
            Error,
            Fatal
        }

        public enum Type
        {
            Action,
            Event,
            Error,
            Processing,
            Info,
            Fatal
        }

        public static async Task StartSession()
        {
            await MiscTools.CheckAndCreateAppFolders();

            string loggingFolder = Path.Combine(applicationPath, logsLocation);
            List<DateTime> previousSessions = new List<DateTime>();
            DateTime timestamp = DateTime.UtcNow;
            
            foreach (string s in Directory.EnumerateFiles(loggingFolder))
            {
                string timestampString = Path.GetFileNameWithoutExtension(s);
                if (DateTime.TryParseExact(timestampString, logDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                {
                    previousSessions.Add(parsed);
                }
            }

            previousSessions.OrderByDescending(x => x);
            if (previousSessions.Count != 0)
            {
                DateTime latest = previousSessions.First();
                string latestPath = Path.Combine(applicationPath, logsLocation, latest.ToString(logDateTimeFormat, CultureInfo.InvariantCulture) + logDatabaseExtension);

                //If the latest log file is newer than 6 hours AND smaller than 5MB, use that file
                if (latest > DateTime.UtcNow.AddHours(-6) || new FileInfo(latestPath).Length >= 5000000)
                {
                    timestamp = latest;
                }
            }
            
            filepath = Path.Combine(applicationPath, logsLocation, timestamp.ToString(logDateTimeFormat, CultureInfo.InvariantCulture) + logDatabaseExtension);
            InitializeDB();
        }

        private static void InitializeDB()
        {            
            string source = "URI=file:" + filepath;
            sql = new SqliteConnection(source);
            sql.Open();

            using var cmd = new SqliteCommand(sql);
            cmd.CommandText = "DROP TABLE IF EXISTS log";
            cmd.ExecuteNonQuery();
            cmd.CommandText = @"CREATE TABLE log(id INTEGER PRIMARY KEY,time DATETIME DEFAULT(STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW')),file TEXT,method TEXT,type TEXT,message TEXT,attach TEXT)";
            cmd.ExecuteNonQuery();
        }

        public static void Log(Type type,
                               string message,
                               string attachment = "",
                               [CallerMemberName] string memberName = "",
                               [CallerFilePath] string sourceFilePath = "",
                               [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (sql == null)
            {
                InitializeDB();
            }

            string file = Path.GetFileName(sourceFilePath.Replace('\\', '/'));
            string line = ", " + sourceLineNumber;
            string fileAndLine = file + line;

            LogPriority priority = LogPriority.Debug;
            switch (type)
            {
                case Type.Action:
                    priority = LogPriority.Info;
                    break;
                case Type.Event:
                    priority = LogPriority.Debug;
                    break;
                case Type.Error:
                    priority = LogPriority.Warn;
                    break;
                case Type.Processing:
                    priority = LogPriority.Verbose;
                    break;
                case Type.Info:
                    priority = LogPriority.Verbose;
                    break;
                case Type.Fatal:
                    priority = LogPriority.Error;
                    break;
            }
            Android.Util.Log.WriteLine(priority, fileAndLine, memberName + ": " + message);

            using var cmd = new SqliteCommand(sql);
            cmd.CommandText = $"INSERT INTO log(time, file, method, type, message, attach) VALUES(@Timestamp,@File,@Method,@Type,@Message,@Attach)";
            cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            cmd.Parameters.AddWithValue("@File", file + line);
            cmd.Parameters.AddWithValue("@Method", memberName);
            cmd.Parameters.AddWithValue("@Type", type.ToString("G"));
            cmd.Parameters.AddWithValue("@Message", message);
            cmd.Parameters.AddWithValue("@Attach", attachment);
            cmd.ExecuteNonQuery();
        }

        public static FileInfo GetLatestLog()
        {
            DirectoryInfo loggingFolder = new DirectoryInfo(Path.Combine(applicationPath, logsLocation));
            FileInfo latest = loggingFolder.GetFiles("*" + logDatabaseExtension)
             .OrderByDescending(f => f.LastWriteTime)
             .ElementAt(1); //Gets the second most recent log file; most recent would be the one being currently used

            return latest;
        } 
    }
}
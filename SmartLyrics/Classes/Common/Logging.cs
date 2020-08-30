using Android.Graphics;
using Android.Util;
using Mono.Data.Sqlite;

using System;
using System.Runtime.CompilerServices;
using static SmartLyrics.Globals;

namespace SmartLyrics.Common
{
    class Logging
    {
        private static SqliteConnection sql;

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

        private static void InitializeDB()
        {
            string filepath = System.IO.Path.Combine(applicationPath, "log.db");
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
                               [CallerMemberName] string memberName = "",
                               [CallerFilePath] string sourceFilePath = "",
                               [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (sql == null)
            {
                InitializeDB();
            }

            string file = System.IO.Path.GetFileName(sourceFilePath.Replace('\\', '/'));
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
            cmd.CommandText = $"INSERT INTO log(time, file, method, type, message) VALUES(@Timestamp,@File,@Method,@Type,@Message)";
            cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            cmd.Parameters.AddWithValue("@File", file + line);
            cmd.Parameters.AddWithValue("@Method", memberName);
            cmd.Parameters.AddWithValue("@Type", type.ToString("G"));
            cmd.Parameters.AddWithValue("@Message", message);
            cmd.ExecuteNonQuery();
        }
    }
}
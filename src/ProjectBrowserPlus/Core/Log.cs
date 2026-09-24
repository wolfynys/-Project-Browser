using System;
using System.IO;

namespace ProjectBrowserPlus.Core
{
    public static class Log
    {
        public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProjectBrowserPlus");
        private static readonly object Gate = new object();

        public static void Info(string msg) => Write("INFO", msg);
        public static void Warn(string msg) => Write("WARN", msg);
        public static void Error(string msg, Exception ex = null) => Write("ERROR", ex == null ? msg : msg + "\n" + ex);

        private static void Write(string level, string msg)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Dir);
                    File.AppendAllText(Path.Combine(Dir, "log.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}\n");
                }
            }
            catch { }
        }
    }
}

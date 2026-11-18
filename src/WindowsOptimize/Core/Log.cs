using System;
using System.IO;
using System.Text;

namespace WindowsOptimize.Core
{
    public static class Log
    {
        static readonly object Gate = new object();

        public static string FilePath { get; } =
            Path.Combine(Paths.Logs, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");

        /// <summary>Mỗi dòng log mới (UI đăng ký để hiển thị).</summary>
        public static event Action<string> Line;

        public static void Step(string m) => Write("▶", m);
        public static void Info(string m) => Write(" ", m);
        public static void Ok(string m) => Write("✓", m);
        public static void Warn(string m) => Write("!", m);
        public static void Err(string m) => Write("✗", m);

        static void Write(string prefix, string message)
        {
            if (message == null) return;
            var line = $"[{DateTime.Now:HH:mm:ss}] {prefix} {message}";
            lock (Gate)
            {
                try { File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8); } catch { }
            }
            try { Line?.Invoke(line); } catch { }
        }
    }
}

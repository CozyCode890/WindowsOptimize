using System;
using System.IO;
using System.Reflection;

namespace WindowsOptimize.Core
{
    public static class Paths
    {
        /// <summary>C:\ProgramData\WindowsOptimize: nơi lưu guard, blocklist, nhật ký.</summary>
        public static string Data { get; } = Ensure(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WindowsOptimize"));

        public static string Logs { get; } = Ensure(Path.Combine(Data, "logs"));

        public static string Temp { get; } = Ensure(Path.Combine(Path.GetTempPath(), "WindowsOptimize"));

        public static string Downloads { get; } = Ensure(Path.Combine(Temp, "downloads"));

        public static string ExeDir { get; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static string System32 { get; } = Environment.GetFolderPath(Environment.SpecialFolder.System);

        static string Ensure(string dir)
        {
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }
    }
}

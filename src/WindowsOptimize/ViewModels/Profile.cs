using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WindowsOptimize.Core;

namespace WindowsOptimize.ViewModels
{
    /// <summary>
    /// Bộ "Của tôi". Mặc định lấy từ cờ Mine trong danh mục (đóng gói sẵn trong exe).
    /// Nếu có file WindowsOptimize.profile.txt cạnh exe (hoặc trong ProgramData) thì dùng file đó,
    /// nên chỉ cần mang theo exe + file profile trên USB là có đúng bộ của mình ở mọi máy.
    /// </summary>
    public sealed class Profile
    {
        public const string FileName = "WindowsOptimize.profile.txt";
        public HashSet<string> Bloat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Tweaks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Apps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string LoadedFrom;

        static IEnumerable<string> Candidates => new[]
        {
            Path.Combine(Paths.ExeDir, FileName),
            Path.Combine(Paths.Data, FileName),
        };

        /// <summary>null = không có file → dùng cờ Mine trong danh mục.</summary>
        public static Profile LoadCustom()
        {
            var file = Candidates.FirstOrDefault(File.Exists);
            if (file == null) return null;
            var p = new Profile { LoadedFrom = file };
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var i = line.IndexOf(':');
                if (i <= 0) continue;
                var kind = line.Substring(0, i).Trim().ToLowerInvariant();
                var val = line.Substring(i + 1).Trim();
                if (kind == "bloat") p.Bloat.Add(val);
                else if (kind == "tweak") p.Tweaks.Add(val);
                else if (kind == "app") p.Apps.Add(val);
            }
            return p;
        }

        public string Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Bộ \"Của tôi\" của WindowsOptimize. Đặt file này cạnh WindowsOptimize.exe.");
            sb.AppendLine("# bloat: = app rác sẽ gỡ | tweak: = tinh chỉnh muốn bật | app: = app sẽ cài (winget/Store ID)");
            foreach (var b in Bloat.OrderBy(x => x)) sb.AppendLine("bloat:" + b);
            foreach (var t in Tweaks.OrderBy(x => x)) sb.AppendLine("tweak:" + t);
            foreach (var a in Apps.OrderBy(x => x)) sb.AppendLine("app:" + a);
            foreach (var target in Candidates)
            {
                try
                {
                    File.WriteAllText(target, sb.ToString(), new UTF8Encoding(false));
                    return target;
                }
                catch { }
            }
            throw new IOException("Không ghi được file profile.");
        }
    }
}

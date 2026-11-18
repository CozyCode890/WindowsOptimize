using System;
using Microsoft.Win32;

namespace WindowsOptimize.Core
{
    public enum RegKind { Dword, String }

    /// <summary>Một giá trị registry mà tinh chỉnh muốn đặt.</summary>
    public sealed class RegOp
    {
        public string Path;
        public string Name;
        public object Value;
        public RegKind Kind;
        /// <summary>Giá trị khi hoàn tác. null = xoá giá trị (Windows tự dùng mặc định).</summary>
        public object Default;
        /// <summary>Không bắt buộc: lỗi khi ghi được bỏ qua và không dùng để dò trạng thái.</summary>
        public bool Optional;

        public override string ToString() => $@"{Path}\{Name} = {Value}";
    }

    public static class Reg
    {
        public static RegOp Dword(string path, string name, int value, int? @default = null, bool optional = false)
            => new RegOp { Path = path, Name = name, Value = value, Kind = RegKind.Dword, Default = @default, Optional = optional };

        public static RegOp Str(string path, string name, string value, string @default = null, bool optional = false)
            => new RegOp { Path = path, Name = name, Value = value, Kind = RegKind.String, Default = @default, Optional = optional };

        static RegistryKey Base(string path, out string sub)
        {
            var i = path.IndexOf('\\');
            var hive = (i < 0 ? path : path.Substring(0, i)).ToUpperInvariant();
            sub = i < 0 ? "" : path.Substring(i + 1);
            RegistryHive h;
            switch (hive)
            {
                case "HKLM": case "HKEY_LOCAL_MACHINE": h = RegistryHive.LocalMachine; break;
                case "HKCU": case "HKEY_CURRENT_USER": h = RegistryHive.CurrentUser; break;
                case "HKU": case "HKEY_USERS": h = RegistryHive.Users; break;
                case "HKCR": case "HKEY_CLASSES_ROOT": h = RegistryHive.ClassesRoot; break;
                default: throw new ArgumentException("Hive không hợp lệ: " + path);
            }
            return RegistryKey.OpenBaseKey(h, RegistryView.Registry64);
        }

        public static object Get(string path, string name)
        {
            try
            {
                using (var b = Base(path, out var sub))
                using (var k = b.OpenSubKey(sub, false))
                    return k?.GetValue(name ?? "", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
            catch { return null; }
        }

        public static bool KeyExists(string path)
        {
            try
            {
                using (var b = Base(path, out var sub))
                using (var k = b.OpenSubKey(sub, false))
                    return k != null;
            }
            catch { return false; }
        }

        public static string[] SubKeys(string path)
        {
            try
            {
                using (var b = Base(path, out var sub))
                using (var k = b.OpenSubKey(sub, false))
                    return k?.GetSubKeyNames() ?? new string[0];
            }
            catch { return new string[0]; }
        }

        public static void Set(string path, string name, object value, RegKind kind)
        {
            using (var b = Base(path, out var sub))
            using (var k = b.CreateSubKey(sub, true))
            {
                if (k == null) throw new InvalidOperationException("Không mở được " + path);
                if (kind == RegKind.Dword)
                    k.SetValue(name ?? "", Convert.ToInt32(value), RegistryValueKind.DWord);
                else
                    k.SetValue(name ?? "", Convert.ToString(value), RegistryValueKind.String);
            }
        }

        public static void CreateKey(string path)
        {
            using (var b = Base(path, out var sub))
            using (b.CreateSubKey(sub, true)) { }
        }

        public static void DeleteValue(string path, string name)
        {
            try
            {
                using (var b = Base(path, out var sub))
                using (var k = b.OpenSubKey(sub, true))
                    k?.DeleteValue(name ?? "", false);
            }
            catch { }
        }

        public static void DeleteKey(string path)
        {
            try
            {
                using (var b = Base(path, out var sub))
                    b.DeleteSubKeyTree(sub, false);
            }
            catch { }
        }

        public static bool Matches(RegOp op)
        {
            var v = Get(op.Path, op.Name);
            if (v == null) return false;
            try
            {
                return op.Kind == RegKind.Dword
                    ? Convert.ToInt64(v) == Convert.ToInt64(op.Value)
                    : string.Equals(Convert.ToString(v), Convert.ToString(op.Value), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public static void Apply(RegOp op)
        {
            try { Set(op.Path, op.Name, op.Value, op.Kind); }
            catch (Exception ex)
            {
                if (!op.Optional) throw new InvalidOperationException($"{op.Path}\\{op.Name}: {ex.Message}", ex);
            }
        }

        public static void Revert(RegOp op)
        {
            try
            {
                if (op.Default == null) DeleteValue(op.Path, op.Name);
                else Set(op.Path, op.Name, op.Default, op.Kind);
            }
            catch (Exception ex)
            {
                if (!op.Optional) throw new InvalidOperationException($"{op.Path}\\{op.Name}: {ex.Message}", ex);
            }
        }
    }
}

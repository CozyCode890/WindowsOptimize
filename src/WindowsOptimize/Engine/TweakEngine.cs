using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using WindowsOptimize.Catalog;
using WindowsOptimize.Core;

namespace WindowsOptimize.Engine
{
    public static class TweakEngine
    {
        const string SvcRoot = @"HKLM\SYSTEM\CurrentControlSet\Services\";

        /// <summary>Tinh chỉnh đã đang được áp dụng trên máy chưa (đọc trực tiếp registry/dịch vụ).</summary>
        public static bool IsApplied(Tweak t)
        {
            try
            {
                if (t.CustomDetect != null) return t.CustomDetect();
                var regs = t.Regs.Where(r => !r.Optional).ToList();
                var svcs = t.Services.Where(s => Reg.KeyExists(SvcRoot + s.Name)).ToList();
                if (regs.Count == 0 && svcs.Count == 0) return false;
                return regs.All(Reg.Matches) && svcs.All(s => Reg.Get(SvcRoot + s.Name, "Start") is int v && v == s.Start);
            }
            catch { return false; }
        }

        public static void Apply(Tweak t)
        {
            foreach (var r in t.Regs) Reg.Apply(r);
            foreach (var s in t.Services) SetService(s.Name, s.Start);
            t.CustomApply?.Invoke();
        }

        public static void Revert(Tweak t)
        {
            foreach (var r in t.Regs) Reg.Revert(r);
            foreach (var s in t.Services) SetService(s.Name, s.DefaultStart);
            t.CustomRevert?.Invoke();
        }

        static void SetService(string name, int start)
        {
            if (!Reg.KeyExists(SvcRoot + name)) return;
            try { Reg.Set(SvcRoot + name, "Start", start, RegKind.Dword); }
            catch
            {
                // Một số dịch vụ có ACL chặn sửa registry trực tiếp → dùng sc.exe
                var mode = start == 4 ? "disabled" : start == 3 ? "demand" : "auto";
                Shell.Run(Path.Combine(Paths.System32, "sc.exe"), $"config \"{name}\" start= {mode}");
            }
            if (start == 4)
            {
                try
                {
                    using (var sc = new ServiceController(name))
                        if (sc.Status == ServiceControllerStatus.Running) { sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15)); }
                }
                catch { }
            }
        }

        /// <summary>
        /// Khởi động lại Explorer để thay đổi taskbar/Explorer có hiệu lực ngay.
        /// Explorer bị kill sẽ được Windows tự mở lại (AutoRestartShell) với quyền người dùng thường.
        /// </summary>
        public static void RestartExplorer()
        {
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("explorer"))
                    try { p.Kill(); } catch { }
                System.Threading.Thread.Sleep(3000);
                if (System.Diagnostics.Process.GetProcessesByName("explorer").Length == 0)
                    Shell.Open(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
            }
            catch (Exception ex) { Log.Warn("Không khởi động lại được Explorer: " + ex.Message); }
        }
    }
}

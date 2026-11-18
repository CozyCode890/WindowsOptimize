using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsOptimize.Core;
using WindowsOptimize.Engine;
using WindowsOptimize.ViewModels;

namespace WindowsOptimize
{
    /// <summary>
    /// Chạy không giao diện (cho script/triển khai hàng loạt/kiểm thử):
    ///   WindowsOptimize.exe --auto mine|recommended [--no-debloat] [--no-tweaks] [--no-apps] [--no-guard] [--no-restore] [--log file]
    ///   WindowsOptimize.exe --report file      (xuất danh sách app/tinh chỉnh/trạng thái hiện tại)
    /// </summary>
    public static class Headless
    {
        public static bool Wants(string[] args) => args.Any(a => a == "--auto" || a == "--report");

        static string Arg(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public static int Run(string[] args)
        {
            try { return RunCore(args); }
            catch (Exception ex)
            {
                Log.Err("Lỗi: " + ex);
                var log = Arg(args, "--log");
                if (log != null) try { File.Copy(Log.FilePath, log, true); } catch { }
                return 2;
            }
        }

        static int RunCore(string[] args)
        {
            // Chạy toàn bộ trên thread pool để await bên trong không cần Dispatcher
            return Task.Run(async () =>
            {
                var vm = new MainVM();
                await vm.LoadAsync();

                var report = Arg(args, "--report");
                if (report != null)
                {
                    File.WriteAllText(report, BuildReport(vm), new UTF8Encoding(false));
                    return 0;
                }

                var preset = (Arg(args, "--auto") ?? "recommended").ToLowerInvariant() == "mine" ? Preset.Mine : Preset.Recommended;
                vm.ApplyPreset(preset);
                if (args.Contains("--no-debloat")) vm.ApplyPreset(Preset.None, "bloat");
                if (args.Contains("--no-tweaks")) vm.ApplyPreset(Preset.Current, "tweak");
                if (args.Contains("--no-apps")) vm.ApplyPreset(Preset.None, "app");
                if (args.Contains("--no-guard")) vm.EnableGuard = false;
                if (args.Contains("--no-restore")) vm.CreateRestorePoint = false;

                var ok = await vm.RunAsync(vm.BuildPlan());
                var log = Arg(args, "--log");
                if (log != null) try { File.Copy(Log.FilePath, log, true); } catch { }
                return ok ? 0 : 1;
            }).GetAwaiter().GetResult();
        }

        static string BuildReport(MainVM vm)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# WindowsOptimize report");
            sb.AppendLine($"system: {SysInfo.Summary} | edition={SysInfo.EditionId} | ltsc={SysInfo.IsLtsc}");
            sb.AppendLine($"activation: {vm.ActivationText}");
            sb.AppendLine($"winget: {vm.WingetText}");
            sb.AppendLine($"store: {vm.StoreText}");
            sb.AppendLine($"guard: {vm.GuardText}");
            sb.AppendLine($"onedrive: {Appx.OneDrivePresent()}");
            sb.AppendLine();
            sb.AppendLine("## Keep (installed)");
            sb.AppendLine(vm.KeepText);
            sb.AppendLine();
            sb.AppendLine("## Bloat found");
            foreach (var g in vm.BloatGroups)
                foreach (var i in g.Items.Cast<BloatItemVM>())
                    sb.AppendLine($"[{g.Name}] {i.Title} | rec={i.Def?.Recommended} mine={i.Def?.Mine} | {string.Join(", ", i.Packages.Select(p => p.Name + (p.Installed ? "" : "(prov)")))}");
            sb.AppendLine();
            sb.AppendLine("## Tweaks");
            foreach (var t in vm.TweakItems)
                sb.AppendLine($"{(t.IsApplied ? "[x]" : "[ ]")} {t.Def.Id} {t.Title}");
            sb.AppendLine();
            sb.AppendLine("## Apps installed (from catalog)");
            foreach (var a in vm.AppItems.Where(a => a.IsInstalled))
                sb.AppendLine($"{a.Def.Id} {a.Title}");
            sb.AppendLine();
            sb.AppendLine("## All Appx packages");
            sb.AppendLine("(name | installed | provisioned | nonremovable | framework | signature)");
            foreach (var p in Appx.Scan())
                sb.AppendLine($"{p.Name} | {p.Installed} | {p.Provisioned} | {p.NonRemovable} | {p.IsFramework} | {p.SignatureKind}");
            return sb.ToString();
        }
    }
}

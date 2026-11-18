using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using WindowsOptimize.Catalog;
using WindowsOptimize.Core;

namespace WindowsOptimize.Engine
{
    public static class Winget
    {
        public static string Exe { get; private set; }
        public static Version Version { get; private set; }
        static readonly Version MinVersion = new Version(1, 8);

        public static bool IsReady => Exe != null && Version != null && Version >= MinVersion;

        /// <summary>Tìm winget.exe: alias của user, hoặc thư mục cài App Installer.</summary>
        public static bool Find()
        {
            Exe = null; Version = null;
            var candidates = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe"),
            };
            var loc = Shell.PowerShell(
                "Get-AppxPackage -Name Microsoft.DesktopAppInstaller | Sort-Object Version -Descending | Select-Object -First 1 -ExpandProperty InstallLocation",
                timeoutMs: 60000).Output.Trim();
            if (loc.Length > 0 && !loc.Contains("\n")) candidates.Add(Path.Combine(loc, "winget.exe"));

            foreach (var c in candidates.Where(File.Exists))
            {
                var r = Shell.Run(c, "--version", timeoutMs: 60000);
                var m = Regex.Match(r.Output, @"v?(\d+\.\d+(\.\d+)?)");
                if (r.Ok && m.Success)
                {
                    Exe = c;
                    Version = new Version(m.Groups[1].Value);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Đảm bảo có winget mới: (1) đăng ký lại App Installer, (2) nếu vẫn thiếu/cũ (LTSC, Server,
        /// máy mới chưa cập nhật Store) thì tải bản chính thức từ github.com/microsoft/winget-cli.
        /// </summary>
        public static bool Ensure(CancellationToken ct, Action<string> status = null)
        {
            if (Find() && IsReady) { Log.Ok($"winget v{Version} sẵn sàng."); return true; }

            if (Exe == null)
            {
                Log.Info("Đăng ký lại App Installer (winget)...");
                Shell.PowerShell("Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -ErrorAction SilentlyContinue", timeoutMs: 120000);
                if (Find() && IsReady) { Log.Ok($"winget v{Version} sẵn sàng."); return true; }
            }

            Log.Info(Exe == null ? "Máy chưa có winget → tải bản chính thức từ GitHub của Microsoft." : $"winget v{Version} quá cũ → cập nhật.");
            try
            {
                var api = Net.GetString("https://api.github.com/repos/microsoft/winget-cli/releases/latest");
                string Url(string pattern) => Regex.Matches(api, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"")
                    .Cast<Match>().Select(m => m.Groups[1].Value).FirstOrDefault(u => Regex.IsMatch(u, pattern, RegexOptions.IgnoreCase));

                var bundleUrl = Url(@"Microsoft\.DesktopAppInstaller_8wekyb3d8bbwe\.msixbundle$");
                var depsUrl = Url(@"DesktopAppInstaller_Dependencies\.zip$");
                var licUrl = Url(@"_License1\.xml$");
                if (bundleUrl == null) throw new Exception("Không tìm thấy file cài winget trên GitHub.");

                var dir = Path.Combine(Paths.Downloads, "winget");
                Directory.CreateDirectory(dir);
                var bundle = Path.Combine(dir, "AppInstaller.msixbundle");
                var deps = Path.Combine(dir, "deps.zip");
                var lic = Path.Combine(dir, "license.xml");

                Net.Download(bundleUrl, bundle, ct, p => status?.Invoke($"Tải winget {p}%"));
                if (depsUrl != null) Net.Download(depsUrl, deps, ct, p => status?.Invoke($"Tải thư viện phụ thuộc {p}%"));
                if (licUrl != null) Net.Download(licUrl, lic, ct);

                var depDir = Path.Combine(dir, "deps");
                if (Directory.Exists(depDir)) Directory.Delete(depDir, true);
                var depFiles = new List<string>();
                if (File.Exists(deps))
                {
                    ZipFile.ExtractToDirectory(deps, depDir);
                    var arch = Environment.Is64BitOperatingSystem
                        ? (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")?.Equals("ARM64", StringComparison.OrdinalIgnoreCase) == true ? "arm64" : "x64")
                        : "x86";
                    depFiles = Directory.GetFiles(depDir, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".appx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
                        .Where(f => f.IndexOf("\\" + arch + "\\", StringComparison.OrdinalIgnoreCase) >= 0 || Path.GetFileName(f).IndexOf("_" + arch, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                status?.Invoke("Đang cài winget...");
                var depsPs = Shell.QList(depFiles);
                var licPs = File.Exists(lic) ? $"-LicensePath {Shell.Q(lic)}" : "-SkipLicense";
                var r = Shell.PowerShell($@"
$deps = {depsPs}
foreach ($d in $deps) {{ try {{ Add-AppxPackage -Path $d -ErrorAction Stop }} catch {{ }} }}
try {{ Add-AppxProvisionedPackage -Online -PackagePath {Shell.Q(bundle)} -DependencyPackagePath $deps {licPs} -ErrorAction Stop | Out-Null; 'PROVISIONED' }}
catch {{ 'WARN ' + $_.Exception.Message }}
try {{ Add-AppxPackage -Path {Shell.Q(bundle)} -ForceApplicationShutdown -ErrorAction Stop; 'REGISTERED' }}
catch {{ 'WARN ' + $_.Exception.Message }}
", Log.Info, ct, 15 * 60 * 1000);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log.Err("Cài winget lỗi: " + ex.Message); }

            if (Find() && Exe != null)
            {
                Log.Ok($"winget v{Version} sẵn sàng.");
                return true;
            }
            Log.Err("Không cài được winget. Thử cập nhật \"App Installer\" trong Microsoft Store rồi chạy lại.");
            return false;
        }

        /// <summary>ID các gói winget đang cài (dùng winget export → JSON).</summary>
        public static HashSet<string> InstalledIds()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Exe == null) return set;
            var file = Path.Combine(Paths.Temp, "winget-export.json");
            try { File.Delete(file); } catch { }
            Shell.Run(Exe, $"export -o \"{file}\" --include-versions --accept-source-agreements --disable-interactivity", timeoutMs: 180000);
            if (!File.Exists(file)) return set;
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"PackageIdentifier\"\\s*:\\s*\"([^\"]+)\""))
                set.Add(m.Groups[1].Value);
            return set;
        }

        // Mã thoát winget coi như thành công
        const int AlreadyInstalled = unchecked((int)0x8A150061);
        const int NoUpgrade = unchecked((int)0x8A15002B);
        const int RebootRequired = unchecked((int)0x8A150109);
        const int RebootInitiate = unchecked((int)0x8A15010A);

        public static (bool Ok, string Message) Install(AppDef app, CancellationToken ct)
        {
            var args = $"install --id \"{app.Id}\" -e --source {app.Source} --accept-package-agreements --accept-source-agreements --disable-interactivity --silent";
            int code;
            string tail = "";
            if (app.AsUser)
            {
                code = Tasks.RunAsLimitedUser(Exe, args, ct);
            }
            else
            {
                var r = Shell.Run(Exe, args, line =>
                {
                    var l = line.Trim();
                    if (l.Length < 3 || l.IndexOfAny(new[] { '█', '▒' }) >= 0 || Regex.IsMatch(l, @"^[-\\|/]$")) return;
                    Log.Info("   " + l);
                }, 45 * 60 * 1000, ct);
                code = r.ExitCode;
                tail = r.Output.Split('\n').Select(s => s.Trim()).LastOrDefault(s => s.Length > 3 && s.IndexOfAny(new[] { '█', '▒' }) < 0) ?? "";
            }
            if (code == 0) return (true, "Đã cài");
            if (code == AlreadyInstalled || code == NoUpgrade) return (true, "Đã có sẵn");
            if (code == RebootRequired || code == RebootInitiate || code == 3010) return (true, "Cần khởi động lại");
            return (false, $"Lỗi 0x{code:X8} {tail}".Trim());
        }
    }
}

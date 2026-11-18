using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using WindowsOptimize.Core;

namespace WindowsOptimize.Engine
{
    /// <summary>
    /// Cài Microsoft Store cho Windows LTSC / IoT LTSC.
    /// Cách 1 (chính thức): wsreset -i, Windows tự tải Store từ Microsoft.
    /// Cách 2 (dự phòng): gói từ github.com/minihub/LTSC-Add-MicrosoftStore. Các gói đều có chữ ký
    /// của Microsoft, Windows sẽ kiểm tra chữ ký khi cài.
    /// </summary>
    public static class StoreHelper
    {
        public static bool IsInstalled() => Appx.IsInstalled("Microsoft.WindowsStore");

        public static bool Ensure(CancellationToken ct, Action<string> status = null)
        {
            if (IsInstalled()) { Log.Ok("Microsoft Store đã có."); return true; }

            Log.Info("Máy chưa có Microsoft Store → chạy \"wsreset -i\" (Windows tự tải Store từ Microsoft)...");
            Shell.Run(Path.Combine(Paths.System32, "WSReset.exe"), "-i", timeoutMs: 120000, ct: ct);
            for (var i = 0; i < 48 && !ct.IsCancellationRequested; i++) // tối đa ~8 phút
            {
                status?.Invoke($"Chờ Windows cài Store... {i * 10}s");
                Thread.Sleep(10000);
                if (IsInstalled())
                {
                    Log.Ok("Đã cài Microsoft Store.");
                    return true;
                }
            }
            ct.ThrowIfCancellationRequested();

            Log.Warn("wsreset -i không thành công → dùng gói LTSC-Add-MicrosoftStore (≈400 MB).");
            try
            {
                const string url = "https://github.com/minihub/LTSC-Add-MicrosoftStore/releases/download/LTSC-Add-MicrosoftStore-24H2/LTSC-Add-MicrosoftStore-24H2.zip";
                var dir = Path.Combine(Paths.Downloads, "store");
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                var zip = Path.Combine(Paths.Downloads, "store.zip");
                Net.Download(url, zip, ct, p => status?.Invoke($"Tải Microsoft Store {p}%"));
                ZipFile.ExtractToDirectory(zip, dir);

                var arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")?.Equals("ARM64", StringComparison.OrdinalIgnoreCase) == true ? "arm64" : "x64";
                var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                var deps = files.Where(f => (f.EndsWith(".appx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
                                            && Path.GetFileName(f).IndexOf(arch, StringComparison.OrdinalIgnoreCase) >= 0
                                            && new[] { "VCLibs", "NET.Native", "UI.Xaml" }.Any(k => Path.GetFileName(f).IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                                .ToList();
                var pkgs = new[] { ("WindowsStore", ".msixbundle"), ("StorePurchaseApp", ".appxbundle"), ("DesktopAppInstaller", ".msixbundle") };
                var script = "$deps = " + Shell.QList(deps) + "\r\nforeach ($d in $deps) { try { Add-AppxPackage -Path $d -ErrorAction Stop } catch { } }\r\n";
                foreach (var (name, ext) in pkgs)
                {
                    var pkg = files.FirstOrDefault(f => Path.GetFileName(f).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 && f.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
                    var lic = files.FirstOrDefault(f => Path.GetFileName(f).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 && f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
                    if (pkg == null) continue;
                    var licArg = lic != null ? "-LicensePath " + Shell.Q(lic) : "-SkipLicense";
                    script += $"try {{ Add-AppxProvisionedPackage -Online -PackagePath {Shell.Q(pkg)} -DependencyPackagePath $deps {licArg} -ErrorAction Stop | Out-Null }} catch {{ 'WARN ' + $_.Exception.Message }}\r\n";
                    script += $"try {{ Add-AppxPackage -Path {Shell.Q(pkg)} -ErrorAction Stop; 'OK {name}' }} catch {{ 'WARN ' + $_.Exception.Message }}\r\n";
                }
                Shell.PowerShell(script, Log.Info, ct, 20 * 60 * 1000);
                try { File.Delete(zip); Directory.Delete(dir, true); } catch { }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log.Err("Cài Store lỗi: " + ex.Message); }

            if (IsInstalled()) { Log.Ok("Đã cài Microsoft Store."); return true; }
            Log.Err("Không cài được Microsoft Store.");
            return false;
        }
    }
}

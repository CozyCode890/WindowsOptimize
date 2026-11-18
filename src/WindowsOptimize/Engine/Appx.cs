using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WindowsOptimize.Core;

namespace WindowsOptimize.Engine
{
    public sealed class AppxPkg
    {
        public string Name;
        public string FullName;
        public string Family;
        public string SignatureKind;
        public bool NonRemovable;
        public bool IsFramework;
        public bool Installed;     // có trong Get-AppxPackage -AllUsers
        public bool Provisioned;   // có trong Get-AppxProvisionedPackage (sẽ cài cho user mới)
        public string ProvisionedName;
        public string DisplayName;  // tên trong Start menu (nếu có)
    }

    public static class Appx
    {
        /// <summary>Quét toàn bộ gói Appx đã cài (mọi user) và gói provisioned.</summary>
        public static List<AppxPkg> Scan()
        {
            var r = Shell.PowerShell(@"
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$pk = if ($admin) { Get-AppxPackage -AllUsers } else { Get-AppxPackage }
$pk | ForEach-Object {
  'P|' + $_.Name + '|' + $_.PackageFullName + '|' + $_.PackageFamilyName + '|' + $_.NonRemovable + '|' + $_.IsFramework + '|' + $_.SignatureKind
}
if ($admin) { Get-AppxProvisionedPackage -Online | ForEach-Object { 'V|' + $_.DisplayName + '|' + $_.PackageName } }
try { Get-StartApps | Where-Object { $_.AppID -like '*!*' } | ForEach-Object { 'S|' + $_.AppID.Split('!')[0] + '|' + $_.Name } } catch { }
", timeoutMs: 180000);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var map = new Dictionary<string, AppxPkg>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in r.Output.Split('\n'))
            {
                var line = raw.Trim();
                var f = line.Split('|');
                if (f.Length >= 7 && f[0] == "P")
                {
                    if (!map.TryGetValue(f[1], out var p)) map[f[1]] = p = new AppxPkg { Name = f[1] };
                    p.FullName = f[2];
                    p.Family = f[3];
                    p.NonRemovable |= f[4].Equals("True", StringComparison.OrdinalIgnoreCase);
                    p.IsFramework = f[5].Equals("True", StringComparison.OrdinalIgnoreCase);
                    p.SignatureKind = f[6];
                    p.Installed = true;
                }
                else if (f.Length >= 3 && f[0] == "S")
                {
                    if (!names.ContainsKey(f[1])) names[f[1]] = f[2];
                }
                else if (f.Length >= 3 && f[0] == "V")
                {
                    if (!map.TryGetValue(f[1], out var p)) map[f[1]] = p = new AppxPkg { Name = f[1], SignatureKind = "Store" };
                    p.Provisioned = true;
                    p.ProvisionedName = f[2];
                    if (string.IsNullOrEmpty(p.Family)) p.Family = FamilyFromPackageName(f[2]);
                }
            }
            foreach (var p in map.Values)
                if (p.Family != null && names.TryGetValue(p.Family, out var dn)) p.DisplayName = dn;
            return map.Values.OrderBy(p => p.Name).ToList();
        }

        /// <summary>Name_Version_Arch_Resource_PublisherId → Name_PublisherId</summary>
        public static string FamilyFromPackageName(string packageName)
        {
            var parts = packageName.Split('_');
            return parts.Length >= 5 ? parts[0] + "_" + parts[parts.Length - 1] : packageName;
        }

        public static bool Like(string name, string pattern)
        {
            if (pattern.IndexOf('*') < 0) return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
            var rx = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(name, rx, RegexOptions.IgnoreCase);
        }

        public static bool IsInstalled(string name)
        {
            var r = Shell.PowerShell($"if (Get-AppxPackage -AllUsers -Name {Shell.Q(name)}) {{ 'YES' }}", timeoutMs: 60000);
            return r.Output.Contains("YES");
        }

        /// <summary>
        /// Gỡ gói Appx cho mọi user + bỏ provisioned (để user mới không bị cài lại).
        /// Trả về kết quả cho từng nhóm theo key.
        /// </summary>
        public static Dictionary<string, string> Remove(IList<(string Key, string[] Patterns)> items, Action<string, string> onResult,
            System.Threading.CancellationToken ct)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(@"
$prov = @(Get-AppxProvisionedPackage -Online)
function Remove-Group([string]$key, [string[]]$patterns) {
  $found = $false; $failed = $false
  foreach ($n in $patterns) {
    foreach ($p in @(Get-AppxPackage -AllUsers -Name $n)) {
      if ($p.NonRemovable) { continue }
      $found = $true
      try { Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction Stop }
      catch {
        try { Remove-AppxPackage -Package $p.PackageFullName -ErrorAction Stop }
        catch { $failed = $true; Write-Output ('WARN|' + $key + '|' + $p.Name + ': ' + $_.Exception.Message) }
      }
    }
    foreach ($v in @($prov | Where-Object { $_.DisplayName -like $n })) {
      $found = $true
      try { Remove-AppxProvisionedPackage -Online -PackageName $v.PackageName -AllUsers -ErrorAction Stop | Out-Null }
      catch {
        try { Remove-AppxProvisionedPackage -Online -PackageName $v.PackageName -ErrorAction Stop | Out-Null }
        catch { $failed = $true; Write-Output ('WARN|' + $key + '|' + $v.DisplayName + ': ' + $_.Exception.Message) }
      }
    }
  }
  if (-not $found) { 'SKIP|' + $key } elseif ($failed) { 'FAIL|' + $key } else { 'OK|' + $key }
}");
            foreach (var it in items)
                sb.AppendLine($"Remove-Group {Shell.Q(it.Key)} {Shell.QList(it.Patterns)}");

            var results = new Dictionary<string, string>();
            Shell.PowerShell(sb.ToString(), line =>
            {
                var f = line.Trim().Split(new[] { '|' }, 3);
                if (f.Length < 2) return;
                if (f[0] == "WARN") { Log.Warn(f.Length > 2 ? f[2] : f[1]); return; }
                if (f[0] == "OK" || f[0] == "FAIL" || f[0] == "SKIP")
                {
                    results[f[1]] = f[0];
                    onResult?.Invoke(f[1], f[0]);
                }
            }, ct, 30 * 60 * 1000);
            return results;
        }

        /// <summary>Gỡ OneDrive bản Win32 và chặn tự cài cho user mới.</summary>
        public static bool RemoveOneDrive()
        {
            var r = Shell.PowerShell(@"
Get-Process OneDrive -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
$setups = @()
$setups += Get-ChildItem ""$env:LOCALAPPDATA\Microsoft\OneDrive\*\OneDriveSetup.exe"" -ErrorAction SilentlyContinue | ForEach-Object FullName
$setups += Get-ChildItem ""$env:ProgramFiles\Microsoft OneDrive\*\OneDriveSetup.exe"" -ErrorAction SilentlyContinue | ForEach-Object FullName
$setups += Get-ChildItem ""${env:ProgramFiles(x86)}\Microsoft OneDrive\*\OneDriveSetup.exe"" -ErrorAction SilentlyContinue | ForEach-Object FullName
foreach ($s in $setups) {
  Write-Output ('Gỡ ' + $s)
  if ($s -like ""$env:LOCALAPPDATA*"") { Start-Process $s '/uninstall' -Wait } else { Start-Process $s '/uninstall /allusers' -Wait }
}
foreach ($s in @(""$env:SystemRoot\System32\OneDriveSetup.exe"", ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"")) {
  if (Test-Path $s) { Write-Output ('Gỡ ' + $s); Start-Process $s '/uninstall' -Wait }
}
# User mới không bị cài OneDrive
reg.exe load 'HKU\WO_Default' ""$env:SystemDrive\Users\Default\NTUSER.DAT"" | Out-Null
reg.exe delete 'HKU\WO_Default\Software\Microsoft\Windows\CurrentVersion\Run' /v OneDriveSetup /f 2>$null | Out-Null
[gc]::Collect(); Start-Sleep -Milliseconds 500
reg.exe unload 'HKU\WO_Default' | Out-Null
reg.exe delete 'HKCU\Software\Microsoft\Windows\CurrentVersion\Run' /v OneDrive /f 2>$null | Out-Null
# Bỏ OneDrive khỏi thanh bên trái Explorer
reg.exe add 'HKCU\Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}' /v System.IsPinnedToNameSpaceTree /t REG_DWORD /d 0 /f | Out-Null
foreach ($p in @(Get-AppxPackage -AllUsers -Name 'Microsoft.OneDriveSync')) { Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction SilentlyContinue }
'DONE'
", Log.Info, default, 10 * 60 * 1000);
            return r.Output.Contains("DONE");
        }

        public static bool OneDrivePresent()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return System.IO.File.Exists(System.IO.Path.Combine(local, @"Microsoft\OneDrive\OneDrive.exe"))
                   || System.IO.File.Exists(System.IO.Path.Combine(pf, @"Microsoft OneDrive\OneDrive.exe"))
                   || System.IO.File.Exists(System.IO.Path.Combine(Paths.System32, "OneDriveSetup.exe"));
        }
    }
}

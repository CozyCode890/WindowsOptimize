using System;
using System.IO;
using WindowsOptimize.Core;

namespace WindowsOptimize.Engine
{
    public static class Activation
    {
        /// <summary>
        /// Mở Microsoft Activation Scripts (massgrave.dev) trong cửa sổ PowerShell riêng.
        /// MAS là script bên thứ ba có menu riêng: người dùng tự chọn HWID/Ohook/... trong đó.
        /// Nếu nhà mạng chặn DNS get.activated.win, dùng DNS-over-HTTPS (theo hướng dẫn của MAS).
        /// </summary>
        public static void OpenMas()
        {
            const string cmd =
                "try { irm https://get.activated.win | iex } " +
                "catch { iex (curl.exe -s --doh-url https://1.1.1.1/dns-query https://get.activated.win | Out-String) }";
            Log.Info("Mở Microsoft Activation Scripts (massgrave.dev) trong cửa sổ mới...");
            Shell.Open(Shell.PowerShellExe, $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"");
        }
    }

    public static class RestorePoint
    {
        public static bool Create(string description)
        {
            var r = Shell.PowerShell($@"
try {{ Enable-ComputerRestore -Drive ""$env:SystemDrive\"" -ErrorAction Stop }} catch {{ }}
New-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name SystemRestorePointCreationFrequency -PropertyType DWord -Value 0 -Force | Out-Null
try {{ Checkpoint-Computer -Description {Shell.Q(description)} -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; 'RP_OK' }}
catch {{ 'RP_FAIL ' + $_.Exception.Message }}
", timeoutMs: 10 * 60 * 1000);
            if (r.Output.Contains("RP_OK")) { Log.Ok("Đã tạo điểm khôi phục hệ thống."); return true; }
            Log.Warn("Không tạo được điểm khôi phục: " + r.Output.Trim());
            return false;
        }
    }
}

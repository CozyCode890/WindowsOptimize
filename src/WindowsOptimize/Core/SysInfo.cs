using System;
using System.Management;
using System.Security.Principal;

namespace WindowsOptimize.Core
{
    public static class SysInfo
    {
        const string NtKey = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public static string EditionId => Reg.Get(NtKey, "EditionID") as string ?? "";
        public static string DisplayVersion => Reg.Get(NtKey, "DisplayVersion") as string ?? "";
        public static int Build => int.TryParse(Reg.Get(NtKey, "CurrentBuildNumber") as string, out var b) ? b : 0;
        public static int Ubr => Reg.Get(NtKey, "UBR") is int u ? u : 0;
        public static bool IsServer => string.Equals(Reg.Get(NtKey, "InstallationType") as string, "Server", StringComparison.OrdinalIgnoreCase);

        /// <summary>LTSC/LTSB: EditionID kết thúc bằng "S" (EnterpriseS, IoTEnterpriseS, EnterpriseSN...).</summary>
        public static bool IsLtsc
        {
            get
            {
                var e = EditionId;
                return e.IndexOf("EnterpriseS", StringComparison.OrdinalIgnoreCase) >= 0
                       || ProductName.IndexOf("LTSC", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public static bool IsWindows11 => Build >= 22000;

        /// <summary>Registry vẫn ghi "Windows 10 ..." trên Win 11, nên sửa lại cho đúng.</summary>
        public static string ProductName
        {
            get
            {
                var n = Reg.Get(NtKey, "ProductName") as string ?? "Windows";
                if (Build >= 22000) n = n.Replace("Windows 10", "Windows 11");
                return n;
            }
        }

        public static string Summary => $"{ProductName} {DisplayVersion} (build {Build}.{Ubr})";

        public static string CurrentUserSid => WindowsIdentity.GetCurrent().User?.Value;

        public static bool IsAdmin =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        /// <summary>Trạng thái bản quyền Windows (WMI). null = không xác định.</summary>
        public static bool? IsActivated()
        {
            try
            {
                using (var s = new ManagementObjectSearcher(
                    "SELECT LicenseStatus FROM SoftwareLicensingProduct " +
                    "WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL"))
                {
                    foreach (ManagementObject o in s.Get())
                        if (Convert.ToInt32(o["LicenseStatus"]) == 1) return true;
                    return false;
                }
            }
            catch { return null; }
        }
    }
}

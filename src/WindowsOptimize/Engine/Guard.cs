using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using WindowsOptimize.Core;

namespace WindowsOptimize.Engine
{
    /// <summary>
    /// Chặn app đã gỡ bị cài lại qua Windows Update / Microsoft Store / Content Delivery, theo 4 lớp:
    ///  1. Khoá Deprovisioned: cách chính thức để feature update không cài lại gói đã gỡ.
    ///  2. Policy RemoveDefaultMicrosoftStorePackages (Windows 11 25H2+, Enterprise/Education).
    ///  3. Tắt Consumer Features / silent install (cho cả user hiện tại và user mới).
    ///  4. Task "AppGuard" chạy bằng SYSTEM, tự gỡ lại nếu app vẫn lọt vào.
    /// </summary>
    public static class Guard
    {
        public const string TaskFolder = "WindowsOptimize";
        public const string TaskName = "AppGuard";
        static string ListFile => Path.Combine(Paths.Data, "blocklist.txt");
        static string FamiliesFile => Path.Combine(Paths.Data, "families.txt");
        static string ScriptFile => Path.Combine(Paths.Data, "guard.ps1");

        const string Deprov = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned";
        const string RemovePolicy = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx\RemoveDefaultMicrosoftStorePackages";

        public static bool IsActive => Tasks.Exists(TaskFolder, TaskName);

        public static List<string> BlockList =>
            File.Exists(ListFile)
                ? File.ReadAllLines(ListFile).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList()
                : new List<string>();

        static List<string> Families =>
            File.Exists(FamiliesFile) ? File.ReadAllLines(FamiliesFile).Where(l => l.Trim().Length > 0).ToList() : new List<string>();

        /// <summary>Bật Guard cho danh sách gói (gộp với danh sách cũ).</summary>
        public static void Enable(IEnumerable<string> patterns, IEnumerable<string> families)
        {
            var names = new SortedSet<string>(BlockList, StringComparer.OrdinalIgnoreCase);
            foreach (var p in patterns) names.Add(p);
            var fams = new SortedSet<string>(Families, StringComparer.OrdinalIgnoreCase);
            foreach (var f in families.Where(f => !string.IsNullOrEmpty(f))) fams.Add(f);

            File.WriteAllText(ListFile,
                "# Danh sách app bị chặn cài lại (WindowsOptimize Guard). Mỗi dòng 1 tên gói, hỗ trợ *.\r\n" +
                string.Join("\r\n", names) + "\r\n", new UTF8Encoding(false));
            File.WriteAllLines(FamiliesFile, fams);
            WriteScript();

            // 1 + 2: Deprovisioned + policy 25H2
            foreach (var f in fams)
            {
                try { Reg.CreateKey($@"{Deprov}\{f}"); } catch (Exception ex) { Log.Warn($"Deprovisioned {f}: {ex.Message}"); }
                try { Reg.Set($@"{RemovePolicy}\{f}", "RemovedPackage", 1, RegKind.Dword); } catch { }
            }
            if (fams.Count > 0) { try { Reg.Set(RemovePolicy, "Enabled", 1, RegKind.Dword); } catch { } }

            // 3: Consumer features (máy) + silent install (user hiện tại + user mới)
            const string cc = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent";
            Reg.Set(cc, "DisableWindowsConsumerFeatures", 1, RegKind.Dword);
            Reg.Set(cc, "DisableConsumerAccountStateContent", 1, RegKind.Dword);
            Reg.Set(cc, "DisableCloudOptimizedContent", 1, RegKind.Dword);
            ApplyCdm(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager");
            WithDefaultUserHive(root => ApplyCdm(root + @"\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"));

            // Trigger Outlook/DevHome tự cài sau OOBE
            const string orch = @"HKLM\SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe";
            if (names.Contains("Microsoft.OutlookForWindows")) Reg.DeleteKey(orch + @"\OutlookUpdate");
            if (names.Contains("Microsoft.Windows.DevHome")) Reg.DeleteKey(orch + @"\DevHomeUpdate");

            // 4: Task chạy bằng SYSTEM
            Tasks.RegisterSystemTask(TaskFolder, TaskName, TaskXml());
            Log.Ok($"Guard đang bảo vệ {names.Count} mục (task \\{TaskFolder}\\{TaskName}).");
        }

        /// <summary>Bỏ chặn 1 gói (khi người dùng chủ động cài lại app đó).</summary>
        public static void Unblock(string appxName)
        {
            if (string.IsNullOrEmpty(appxName)) return;
            var list = BlockList;
            var keep = list.Where(n => !Appx.Like(appxName, n)).ToList();
            if (keep.Count != list.Count)
            {
                File.WriteAllText(ListFile, "# WindowsOptimize Guard\r\n" + string.Join("\r\n", keep) + "\r\n", new UTF8Encoding(false));
                Log.Info($"Bỏ chặn {appxName} trong Guard.");
            }
            var fams = Families;
            var hit = fams.Where(f => f.StartsWith(appxName + "_", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var f in hit)
            {
                Reg.DeleteKey($@"{Deprov}\{f}");
                Reg.DeleteKey($@"{RemovePolicy}\{f}");
            }
            if (hit.Count > 0) File.WriteAllLines(FamiliesFile, fams.Except(hit));
        }

        /// <summary>Tắt hẳn Guard và gỡ mọi khoá chặn.</summary>
        public static void Disable()
        {
            Tasks.Delete(TaskFolder, TaskName);
            foreach (var f in Families)
            {
                Reg.DeleteKey($@"{Deprov}\{f}");
                Reg.DeleteKey($@"{RemovePolicy}\{f}");
            }
            if (Reg.SubKeys(RemovePolicy).Length == 0) Reg.DeleteKey(RemovePolicy);
            foreach (var f in new[] { ListFile, FamiliesFile, ScriptFile })
                try { File.Delete(f); } catch { }
            Log.Ok("Đã tắt Guard và bỏ chặn mọi app.");
        }

        static void ApplyCdm(string key)
        {
            foreach (var v in new[] { "SilentInstalledAppsEnabled", "PreInstalledAppsEnabled", "PreInstalledAppsEverEnabled", "OemPreInstalledAppsEnabled", "FeatureManagementEnabled" })
                try { Reg.Set(key, v, 0, RegKind.Dword); } catch { }
        }

        /// <summary>Nạp hive của user mặc định (C:\Users\Default) để mọi user tạo sau cũng được áp dụng.</summary>
        public static void WithDefaultUserHive(Action<string> action)
        {
            var hive = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", @"Users\Default\NTUSER.DAT");
            if (!File.Exists(hive)) return;
            var reg = Path.Combine(Paths.System32, "reg.exe");
            var load = Shell.Run(reg, $"load HKU\\WO_Default \"{hive}\"");
            if (!load.Ok) { Log.Warn("Không nạp được hive Default: " + load.Output.Trim()); return; }
            try { action(@"HKU\WO_Default"); }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                for (var i = 0; i < 5 && !Shell.Run(reg, "unload HKU\\WO_Default").Ok; i++)
                    System.Threading.Thread.Sleep(500);
            }
        }

        static void WriteScript()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("guard.ps1"))
            using (var r = new StreamReader(s, Encoding.UTF8))
                File.WriteAllText(ScriptFile, r.ReadToEnd(), new UTF8Encoding(true));
        }

        static string TaskXml()
        {
            var ps = Shell.PowerShellExe;
            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.3"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>WindowsOptimize</Author>
    <Description>Gỡ lại app rác bị Windows Update/Store cài lại. Danh sách: {Paths.Data}\blocklist.txt</Description>
  </RegistrationInfo>
  <Triggers>
    <BootTrigger><Enabled>true</Enabled><Delay>PT3M</Delay></BootTrigger>
    <LogonTrigger><Enabled>true</Enabled><Delay>PT2M</Delay></LogonTrigger>
    <CalendarTrigger>
      <StartBoundary>2024-01-01T12:00:00</StartBoundary>
      <Enabled>true</Enabled>
      <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>
    </CalendarTrigger>
    <EventTrigger>
      <Enabled>true</Enabled>
      <Delay>PT5M</Delay>
      <Subscription>&lt;QueryList&gt;&lt;Query Id=""0"" Path=""System""&gt;&lt;Select Path=""System""&gt;*[System[Provider[@Name='Microsoft-Windows-WindowsUpdateClient'] and (EventID=19)]]&lt;/Select&gt;&lt;/Query&gt;&lt;/QueryList&gt;</Subscription>
    </EventTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <ExecutionTimeLimit>PT30M</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{ps}</Command>
      <Arguments>-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{ScriptFile}""</Arguments>
    </Exec>
  </Actions>
</Task>";
        }
    }
}

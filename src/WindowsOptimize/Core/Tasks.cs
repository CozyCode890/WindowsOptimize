using System;
using System.Threading;

namespace WindowsOptimize.Core
{
    /// <summary>
    /// Task Scheduler qua COM (Schedule.Service): dùng cho Guard (chạy bằng SYSTEM)
    /// và để chạy lệnh dưới quyền người dùng thường (một số installer từ chối chạy khi đang là admin).
    /// </summary>
    public static class Tasks
    {
        const int CreateOrUpdate = 6;
        const int LogonInteractiveToken = 3;
        const int LogonServiceAccount = 5;

        static dynamic Connect()
        {
            var t = Type.GetTypeFromProgID("Schedule.Service");
            dynamic svc = Activator.CreateInstance(t);
            svc.Connect();
            return svc;
        }

        static dynamic Folder(dynamic svc, string folder)
        {
            dynamic root = svc.GetFolder("\\");
            try { return svc.GetFolder("\\" + folder); }
            catch { return root.CreateFolder(folder); }
        }

        public static void RegisterSystemTask(string folder, string name, string xml)
        {
            var svc = Connect();
            var f = Folder(svc, folder);
            f.RegisterTask(name, xml, CreateOrUpdate, "SYSTEM", null, LogonServiceAccount);
        }

        public static bool Exists(string folder, string name)
        {
            try
            {
                var svc = Connect();
                dynamic f = svc.GetFolder("\\" + folder);
                f.GetTask(name);
                return true;
            }
            catch { return false; }
        }

        public static void Delete(string folder, string name)
        {
            try
            {
                var svc = Connect();
                dynamic f = svc.GetFolder("\\" + folder);
                f.DeleteTask(name, 0);
            }
            catch { }
        }

        public static void RunNow(string folder, string name)
        {
            try
            {
                var svc = Connect();
                dynamic f = svc.GetFolder("\\" + folder);
                dynamic task = f.GetTask(name);
                task.Run(null);
            }
            catch (Exception ex) { Log.Warn("Không chạy được task " + name + ": " + ex.Message); }
        }

        /// <summary>
        /// Chạy 1 lệnh dưới quyền người dùng đang đăng nhập (không phải admin) và chờ xong.
        /// Trả về mã thoát của lệnh.
        /// </summary>
        public static int RunAsLimitedUser(string exe, string args, CancellationToken ct, int timeoutMs = 30 * 60 * 1000)
        {
            const string folder = "WindowsOptimize";
            var name = "UserRun-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var sid = SysInfo.CurrentUserSid;
            var xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Principals>
    <Principal id=""Author"">
      <UserId>{sid}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT2H</ExecutionTimeLimit>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{System.Security.SecurityElement.Escape(exe)}</Command>
      <Arguments>{System.Security.SecurityElement.Escape(args)}</Arguments>
    </Exec>
  </Actions>
</Task>";
            var svc = Connect();
            var f = Folder(svc, folder);
            dynamic task = f.RegisterTask(name, xml, CreateOrUpdate, null, null, LogonInteractiveToken);
            try
            {
                task.Run(null);
                var start = DateTime.UtcNow;
                Thread.Sleep(1500);
                // State: 3 = Ready, 4 = Running
                while (true)
                {
                    dynamic t = f.GetTask(name);
                    int state = t.State;
                    if (state != 4 && (DateTime.UtcNow - start).TotalSeconds > 3) return (int)t.LastTaskResult;
                    if (ct.IsCancellationRequested || (DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
                    {
                        try { t.Stop(0); } catch { }
                        return -1;
                    }
                    Thread.Sleep(1000);
                }
            }
            finally
            {
                try { f.DeleteTask(name, 0); } catch { }
            }
        }
    }
}

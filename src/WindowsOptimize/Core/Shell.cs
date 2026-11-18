using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace WindowsOptimize.Core
{
    public sealed class ProcResult
    {
        public int ExitCode;
        public string Output = "";
        public bool TimedOut;
        public bool Ok => ExitCode == 0 && !TimedOut;
    }

    public static class Shell
    {
        public static string PowerShellExe =>
            Path.Combine(Paths.System32, @"WindowsPowerShell\v1.0\powershell.exe");

        /// <summary>Chạy 1 tiến trình ẩn, gom output, hỗ trợ huỷ và timeout.</summary>
        public static ProcResult Run(string file, string args, Action<string> onLine = null,
            int timeoutMs = -1, CancellationToken ct = default, Encoding encoding = null)
        {
            var psi = new ProcessStartInfo(file, args ?? "")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = encoding ?? Encoding.UTF8,
                StandardErrorEncoding = encoding ?? Encoding.UTF8,
                WorkingDirectory = Paths.Temp,
            };
            var sb = new StringBuilder();
            var result = new ProcResult();
            try
            {
                using (var p = new Process { StartInfo = psi })
                {
                    DataReceivedEventHandler handler = (s, e) =>
                    {
                        if (e.Data == null) return;
                        lock (sb) sb.AppendLine(e.Data);
                        try { onLine?.Invoke(e.Data); } catch { }
                    };
                    p.OutputDataReceived += handler;
                    p.ErrorDataReceived += handler;
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    var sw = Stopwatch.StartNew();
                    while (!p.WaitForExit(200))
                    {
                        if (ct.IsCancellationRequested || (timeoutMs > 0 && sw.ElapsedMilliseconds > timeoutMs))
                        {
                            result.TimedOut = !ct.IsCancellationRequested;
                            try { p.Kill(); } catch { }
                            break;
                        }
                    }
                    p.WaitForExit(5000);
                    result.ExitCode = p.HasExited ? p.ExitCode : -1;
                }
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                sb.AppendLine(ex.Message);
            }
            lock (sb) result.Output = sb.ToString();
            return result;
        }

        /// <summary>
        /// Chạy script bằng Windows PowerShell 5.1 (có sẵn trên mọi máy) với ExecutionPolicy Bypass,
        /// nên người dùng không bao giờ phải đụng tới Set-ExecutionPolicy.
        /// </summary>
        public static ProcResult PowerShell(string script, Action<string> onLine = null,
            CancellationToken ct = default, int timeoutMs = -1)
        {
            var file = Path.Combine(Paths.Temp, "wo_" + Guid.NewGuid().ToString("N") + ".ps1");
            var header =
                "$ErrorActionPreference = 'Continue'\r\n" +
                "$ProgressPreference = 'SilentlyContinue'\r\n" +
                "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n";
            File.WriteAllText(file, header + script, new UTF8Encoding(true));
            try
            {
                return Run(PowerShellExe,
                    $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{file}\"",
                    onLine, timeoutMs, ct);
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
        }

        /// <summary>Đặt chuỗi vào dấu nháy đơn của PowerShell.</summary>
        public static string Q(string s) => "'" + (s ?? "").Replace("'", "''") + "'";

        public static string QList(System.Collections.Generic.IEnumerable<string> items)
        {
            var sb = new StringBuilder("@(");
            var first = true;
            foreach (var i in items)
            {
                if (!first) sb.Append(',');
                sb.Append(Q(i));
                first = false;
            }
            return sb.Append(')').ToString();
        }

        /// <summary>Mở 1 cửa sổ/URL bằng shell (không chờ).</summary>
        public static void Open(string target, string args = null)
        {
            try { Process.Start(new ProcessStartInfo(target, args ?? "") { UseShellExecute = true }); }
            catch (Exception ex) { Log.Err(ex.Message); }
        }
    }
}

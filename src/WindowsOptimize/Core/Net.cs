using System;
using System.IO;
using System.Net;
using System.Threading;

namespace WindowsOptimize.Core
{
    public static class Net
    {
        static Net()
        {
            // .NET Framework mặc định có thể chưa bật TLS 1.2/1.3 → GitHub từ chối.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
        }

        const string UserAgent = "WindowsOptimize/1.0 (+https://github.com)";

        public static string GetString(string url)
        {
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                wc.Encoding = System.Text.Encoding.UTF8;
                return wc.DownloadString(url);
            }
        }

        /// <summary>Tải file, báo tiến độ theo %, hỗ trợ huỷ.</summary>
        public static void Download(string url, string dest, CancellationToken ct, Action<int> percent = null)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent;
            req.AllowAutoRedirect = true;
            req.Timeout = 60000;
            req.ReadWriteTimeout = 60000;
            var tmp = dest + ".part";
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = File.Create(tmp))
            {
                var total = resp.ContentLength;
                var buf = new byte[1 << 16];
                long done = 0;
                int lastPct = -1, n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    dst.Write(buf, 0, n);
                    done += n;
                    if (total > 0)
                    {
                        var pct = (int)(done * 100 / total);
                        if (pct != lastPct) { lastPct = pct; percent?.Invoke(pct); }
                    }
                }
            }
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
        }

        public static bool IsOnline()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://www.msftconnecttest.com/connecttest.txt");
                req.Timeout = 5000;
                using (var r = (HttpWebResponse)req.GetResponse()) return r.StatusCode == HttpStatusCode.OK;
            }
            catch { return false; }
        }
    }
}

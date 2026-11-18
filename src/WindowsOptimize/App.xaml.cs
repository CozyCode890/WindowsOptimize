using System;
using System.Windows;
using WindowsOptimize.Core;

namespace WindowsOptimize
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (s, ex) =>
            {
                Log.Err("Lỗi không mong đợi: " + ex.Exception);
                MessageBox.Show(ex.Exception.Message + "\n\nChi tiết trong nhật ký: " + Log.FilePath, "WindowsOptimize",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            if (Headless.Wants(e.Args))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var code = Headless.Run(e.Args);
                Shutdown(code);
                return;
            }

            var win = new MainWindow();
            MainWindow = win;
#if NOELEVATE
            var i = Array.IndexOf(e.Args, "--snap");
            if (i >= 0 && i + 1 < e.Args.Length)
            {
                win.WindowStartupLocation = WindowStartupLocation.Manual;
                win.Left = -30000; win.Top = 0; win.ShowActivated = false; win.ShowInTaskbar = false;
                win.Show();
                _ = win.SnapAsync(e.Args[i + 1]);
                return;
            }
#endif
            win.Show();
        }
    }
}

using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WindowsOptimize.Core;
using WindowsOptimize.Engine;
using WindowsOptimize.ViewModels;

namespace WindowsOptimize
{
    public partial class MainWindow : Window
    {
        readonly MainVM _vm = new MainVM();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            Log.Line += line => Dispatcher.BeginInvoke(new Action(() =>
            {
                LogBox.AppendText(line + Environment.NewLine);
                LogBox.ScrollToEnd();
            }));
            _vm.PropertyChanged += Vm_PropertyChanged;
            SourceInitialized += (s, e) => DarkTitleBar();
            Loaded += async (s, e) =>
            {
                Log.Info($"WindowsOptimize {_vm.Version} · {SysInfo.Summary}");
                await _vm.LoadAsync();
            };
            Closing += Window_Closing;
        }

        // ─── Giao diện ─────────────────────────────────────────────────────────
        void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var good = (Brush)FindResource("Good");
                var bad = (Brush)FindResource("Bad");
                var gray = (Brush)FindResource("Text3");
                DotAct.Fill = _vm.IsActivated ? good : bad;
                DotWinget.Fill = _vm.WingetOk ? good : (Brush)FindResource("Warn");
                DotStore.Fill = _vm.StoreOk ? good : (Brush)FindResource("Warn");
                DotGuard.Fill = _vm.GuardOn ? good : gray;
            }));
        }

        void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            var target = (string)((FrameworkElement)sender).Tag;
            foreach (var name in new[] { "PageHome", "PageBloat", "PageTweaks", "PageApps", "PageTools", "PageRun" })
                if (FindName(name) is FrameworkElement page)
                    page.Visibility = name == target ? Visibility.Visible : Visibility.Collapsed;
            BottomBar.Visibility = target == "PageRun" || target == "PageTools" ? Visibility.Collapsed : Visibility.Visible;
        }

        void ShowRunPage()
        {
            NavRun.Visibility = Visibility.Visible;
            NavRun.IsChecked = true;
        }

        /// <summary>Chia cột cho thẻ: giãn đều theo chiều rộng cửa sổ.</summary>
        void List_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var ic = (ItemsControl)sender;
            var min = ic == TweakList ? 330 : ic == BloatList ? 270 : 235;
            var avail = e.NewSize.Width;
            if (avail <= 0) return;
            var cols = Math.Max(1, (int)(avail / (min + 10)));
            ic.Tag = Math.Floor(avail / cols) - 10;
        }

        void GroupToggle_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is GroupVM g) g.ToggleAll();
        }

        void Search_TextChanged(object sender, TextChangedEventArgs e) => _vm.Filter(SearchBox.Text);

        static Preset ParsePreset(string s)
        {
            switch (s)
            {
                case "mine": return Preset.Mine;
                case "recommended": return Preset.Recommended;
                case "current": return Preset.Current;
                default: return Preset.None;
            }
        }

        void Preset_Click(object sender, RoutedEventArgs e)
        {
            var tag = ((string)((FrameworkElement)sender).Tag).Split(':');
            _vm.ApplyPreset(ParsePreset(tag[1]), tag[0]);
            if (tag[0] == "all") NavBloat.IsChecked = true;
        }

        // ─── Chạy ──────────────────────────────────────────────────────────────
        async void RunPreset_Click(object sender, RoutedEventArgs e)
        {
            _vm.ApplyPreset(ParsePreset((string)((FrameworkElement)sender).Tag));
            await StartPlan();
        }

        async void Start_Click(object sender, RoutedEventArgs e) => await StartPlan();

        async System.Threading.Tasks.Task StartPlan()
        {
            if (_vm.IsRunning) return;
            var plan = _vm.BuildPlan();
            if (plan.Count == 0)
            {
                MessageBox.Show(this, "Chưa chọn gì để làm.", "WindowsOptimize", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var msg = $"Sẽ thực hiện:\n\n" +
                      $"  • Gỡ {_vm.RemoveCount} app rác{(_vm.EnableGuard && _vm.RemoveCount > 0 ? " (và chặn cài lại)" : "")}\n" +
                      $"  • {_vm.TweakCount} thay đổi tinh chỉnh\n" +
                      $"  • Cài {_vm.InstallCount} ứng dụng\n\n" +
                      (_vm.CreateRestorePoint ? "Trước đó sẽ tạo điểm khôi phục hệ thống.\n" : "") +
                      "Có thể tiếp tục dùng máy trong lúc chạy. Bắt đầu?";
            if (MessageBox.Show(this, msg, "WindowsOptimize", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            await RunSteps(plan);
        }

        async System.Threading.Tasks.Task RunSteps(System.Collections.Generic.List<Step> steps)
        {
            if (_vm.IsRunning) { NavRun.IsChecked = true; return; }
            ShowRunPage();
            await _vm.RunAsync(steps);
        }

        void Cancel_Click(object sender, RoutedEventArgs e) => _vm.Cancel();
        void Back_Click(object sender, RoutedEventArgs e) => NavHome.IsChecked = true;

        void Reboot_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "Khởi động lại máy sau 5 giây?", "WindowsOptimize", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                Shell.Run(System.IO.Path.Combine(Paths.System32, "shutdown.exe"), "/r /t 5 /c \"WindowsOptimize: khởi động lại để hoàn tất\"");
        }

        void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_vm.IsRunning) return;
            if (MessageBox.Show(this, "Đang chạy dở. Thoát bây giờ có thể để lại việc làm dở. Vẫn thoát?", "WindowsOptimize",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                e.Cancel = true;
            else _vm.Cancel();
        }

        // ─── Công cụ ───────────────────────────────────────────────────────────
        void Mas_Click(object sender, RoutedEventArgs e) => Activation.OpenMas();

        async void Winget_Click(object sender, RoutedEventArgs e) => await RunSteps(new System.Collections.Generic.List<Step>
        {
            new Step("Cài / cập nhật winget", ct =>
            {
                var ok = Winget.Ensure(ct, s => _vm.StepText = s);
                _vm.WingetOk = ok; _vm.WingetText = ok ? $"v{Winget.Version}" : "Lỗi";
                return ok;
            })
        });

        async void Store_Click(object sender, RoutedEventArgs e) => await RunSteps(new System.Collections.Generic.List<Step>
        {
            new Step("Cài Microsoft Store", ct =>
            {
                var ok = StoreHelper.Ensure(ct, s => _vm.StepText = s);
                _vm.StoreOk = ok; _vm.StoreText = ok ? "Đã có" : "Chưa có";
                return ok;
            })
        });

        async void RestorePoint_Click(object sender, RoutedEventArgs e) => await RunSteps(new System.Collections.Generic.List<Step>
        {
            new Step("Tạo điểm khôi phục", ct => RestorePoint.Create("WindowsOptimize (thủ công)"))
        });

        void Rstrui_Click(object sender, RoutedEventArgs e) => Shell.Open(System.IO.Path.Combine(Paths.System32, "rstrui.exe"));
        void Logs_Click(object sender, RoutedEventArgs e) => Shell.Open(Paths.Logs);

        async void Explorer_Click(object sender, RoutedEventArgs e) => await RunSteps(new System.Collections.Generic.List<Step>
        {
            new Step("Làm mới Explorer", ct => { TweakEngine.RestartExplorer(); return true; })
        });

        void GuardRun_Click(object sender, RoutedEventArgs e)
        {
            if (!Guard.IsActive) { MessageBox.Show(this, "Guard chưa bật. Hãy gỡ app rác với ô \"Chặn cài lại\" được tick.", "WindowsOptimize"); return; }
            Tasks.RunNow(Guard.TaskFolder, Guard.TaskName);
            MessageBox.Show(this, $"Đã chạy kiểm tra. Kết quả ghi ở {System.IO.Path.Combine(Paths.Data, "guard.log")}", "WindowsOptimize");
        }

        async void GuardOff_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "Tắt bảo vệ và cho phép Windows/Store cài lại mọi app đã gỡ?", "WindowsOptimize",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            await RunSteps(new System.Collections.Generic.List<Step>
            {
                new Step("Tắt bảo vệ chống cài lại", ct => { Guard.Disable(); _vm.RefreshGuard(); return true; })
            });
        }

        void SaveMine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = _vm.SaveMine();
                MessageBox.Show(this, "Đã lưu bộ \"Của tôi\" vào:\n" + path + "\n\nCopy file này cùng WindowsOptimize.exe sang máy khác để dùng lại.", "WindowsOptimize");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "WindowsOptimize", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

#if NOELEVATE
        /// <summary>Bản dev: chụp ảnh từng trang ra PNG để kiểm tra giao diện.</summary>
        public async System.Threading.Tasks.Task SnapAsync(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            await _vm.LoadAsync();
            await System.Threading.Tasks.Task.Delay(800);
            _vm.ApplyPreset(Preset.Mine);
            var pages = new (RadioButton Nav, string Name)[] { (NavHome, "1-home"), (NavBloat, "2-bloat"), (NavTweaks, "3-tweaks"), (NavApps, "4-apps"), (NavTools, "5-tools") };
            foreach (var (nav, name) in pages)
            {
                nav.IsChecked = true;
                await System.Threading.Tasks.Task.Delay(500);
                Save(System.IO.Path.Combine(dir, name + ".png"));
            }
            Log.Info("Đây là dòng nhật ký mẫu cho ảnh chụp trang Tiến trình.");
            ShowRunPage();
            _vm.StepText = "Cài Zalo (3/12)";
            _vm.Progress = 45;
            await System.Threading.Tasks.Task.Delay(500);
            Save(System.IO.Path.Combine(dir, "6-run.png"));
            Application.Current.Shutdown();
        }

        void Save(string file)
        {
            UpdateLayout();
            var root = (FrameworkElement)Content;
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(root);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using (var fs = System.IO.File.Create(file)) enc.Save(fs);
        }
#endif

        // ─── Thanh tiêu đề tối ─────────────────────────────────────────────────
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        void DarkTitleBar()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var on = 1;
                DwmSetWindowAttribute(hwnd, 20, ref on, 4);            // DWMWA_USE_IMMERSIVE_DARK_MODE
                var caption = 0x001C1613;                               // #13161C (COLORREF = 0x00BBGGRR)
                DwmSetWindowAttribute(hwnd, 35, ref caption, 4);       // DWMWA_CAPTION_COLOR (Win 11)
            }
            catch { }
        }
    }
}

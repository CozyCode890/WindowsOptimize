using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsOptimize.Catalog;
using WindowsOptimize.Core;
using WindowsOptimize.Engine;

namespace WindowsOptimize.ViewModels
{
    public enum Preset { Mine, Recommended, None, Current }

    public sealed class Step
    {
        public string Title;
        public Func<CancellationToken, bool> Run;
        public Step(string title, Func<CancellationToken, bool> run) { Title = title; Run = run; }
    }

    public sealed class MainVM : Observable
    {
        // ─── Dữ liệu ───────────────────────────────────────────────────────────
        ObservableCollection<GroupVM> _bloat = new ObservableCollection<GroupVM>();
        public ObservableCollection<GroupVM> BloatGroups { get => _bloat; private set => Set(ref _bloat, value); }
        public ObservableCollection<GroupVM> TweakGroups { get; } = new ObservableCollection<GroupVM>();
        public ObservableCollection<GroupVM> AppGroups { get; } = new ObservableCollection<GroupVM>();

        string _keep = "";
        public string KeepText { get => _keep; set => Set(ref _keep, value); }

        public IEnumerable<BloatItemVM> BloatItems => BloatGroups.SelectMany(g => g.Items).Cast<BloatItemVM>();
        public IEnumerable<TweakItemVM> TweakItems => TweakGroups.SelectMany(g => g.Items).Cast<TweakItemVM>();
        public IEnumerable<AppItemVM> AppItems => AppGroups.SelectMany(g => g.Items).Cast<AppItemVM>();

        List<AppxPkg> _packages = new List<AppxPkg>();
        Profile _custom;

        // ─── Trạng thái hệ thống ───────────────────────────────────────────────
        public string SystemText => SysInfo.Summary;
        public bool IsLtsc => SysInfo.IsLtsc;
        public string Version => "v" + typeof(MainVM).Assembly.GetName().Version.ToString(3);

        string _activation = "Đang kiểm tra...";
        public string ActivationText { get => _activation; set => Set(ref _activation, value); }
        bool _activated = true;
        public bool IsActivated { get => _activated; set => Set(ref _activated, value); }

        string _winget = "Đang kiểm tra...";
        public string WingetText { get => _winget; set => Set(ref _winget, value); }
        bool _wingetOk = true;
        public bool WingetOk { get => _wingetOk; set => Set(ref _wingetOk, value); }

        string _store = "Đang kiểm tra...";
        public string StoreText { get => _store; set => Set(ref _store, value); }
        bool _storeOk = true;
        public bool StoreOk { get => _storeOk; set => Set(ref _storeOk, value); }

        string _guard = "";
        public string GuardText { get => _guard; set => Set(ref _guard, value); }
        bool _guardOn;
        public bool GuardOn { get => _guardOn; set => Set(ref _guardOn, value); }

        string _scan = "Đang quét app trên máy...";
        public string BloatStatus { get => _scan; set => Set(ref _scan, value); }
        string _appStatus = "Đang kiểm tra app đã cài...";
        public string AppStatus { get => _appStatus; set => Set(ref _appStatus, value); }
        public string MineSource => _custom != null ? "Đang dùng " + _custom.LoadedFrom : "Đóng gói sẵn trong exe";

        bool _loaded;
        public bool IsLoaded { get => _loaded; set => Set(ref _loaded, value); }

        // ─── Tuỳ chọn khi chạy ─────────────────────────────────────────────────
        bool _restore = true;
        public bool CreateRestorePoint { get => _restore; set => Set(ref _restore, value); }
        bool _enableGuard = true;
        public bool EnableGuard { get => _enableGuard; set => Set(ref _enableGuard, value); }

        // ─── Bộ đếm ────────────────────────────────────────────────────────────
        public int RemoveCount => BloatItems.Count(i => i.IsChecked);
        public int TweakCount => TweakItems.Count(i => i.IsChange);
        public int InstallCount => AppItems.Count(i => i.IsChecked && !i.IsInstalled);
        public bool CanStart => IsLoaded && !IsRunning && (RemoveCount + TweakCount + InstallCount) > 0;

        public void RefreshCounts()
        {
            Raise(nameof(RemoveCount)); Raise(nameof(TweakCount)); Raise(nameof(InstallCount)); Raise(nameof(CanStart));
        }

        // ─── Tiến trình ────────────────────────────────────────────────────────
        bool _running;
        public bool IsRunning { get => _running; set { Set(ref _running, value); Raise(nameof(CanStart)); } }
        bool _finished;
        public bool IsFinished { get => _finished; set => Set(ref _finished, value); }
        double _progress;
        public double Progress { get => _progress; set => Set(ref _progress, value); }
        string _stepText = "";
        public string StepText { get => _stepText; set => Set(ref _stepText, value); }
        string _summary = "";
        public string RunSummary { get => _summary; set => Set(ref _summary, value); }
        bool _needReboot;
        public bool NeedReboot { get => _needReboot; set => Set(ref _needReboot, value); }

        CancellationTokenSource _cts;

        public MainVM()
        {
            ItemVM.AnyCheckedChanged += RefreshCounts;
            _custom = Profile.LoadCustom();
            BuildTweaks();
            BuildApps();
        }

        // ═══ Tải dữ liệu ══════════════════════════════════════════════════════
        void BuildTweaks()
        {
            foreach (var g in TweakCatalog.All.GroupBy(t => t.Group))
            {
                var gv = new GroupVM(g.Key);
                foreach (var t in g)
                {
                    var vm = new TweakItemVM(t);
                    vm.IsApplied = TweakEngine.IsApplied(t);
                    vm.IsChecked = vm.IsApplied;
                    gv.Items.Add(vm);
                }
                TweakGroups.Add(gv);
            }
        }

        void BuildApps()
        {
            foreach (var g in AppCatalog.All.GroupBy(a => a.Category))
            {
                var gv = new GroupVM(g.Key);
                foreach (var a in g) gv.Items.Add(new AppItemVM(a));
                AppGroups.Add(gv);
            }
        }

        Task _loadTask;
        public Task LoadAsync() => _loadTask ?? (_loadTask = LoadCore());

        Task LoadCore()
        {
            var t1 = Task.Run(() =>
            {
                try
                {
                    _packages = Appx.Scan();
                    BuildBloat();
                    var store = _packages.Any(p => p.Name == "Microsoft.WindowsStore" && p.Installed);
                    StoreOk = store;
                    StoreText = store ? "Đã có" : (IsLtsc ? "Chưa có (bản LTSC)" : "Chưa có");
                    foreach (var a in AppItems.Where(a => a.Def.AppxName != null))
                        if (_packages.Any(p => p.Installed && p.Name.Equals(a.Def.AppxName, StringComparison.OrdinalIgnoreCase)))
                            a.IsInstalled = true;
                }
                catch (Exception ex) { BloatStatus = "Lỗi khi quét: " + ex.Message; }
            });
            var t2 = Task.Run(() =>
            {
                try
                {
                    if (Winget.Find())
                    {
                        WingetOk = Winget.IsReady;
                        WingetText = Winget.IsReady ? $"v{Winget.Version}" : $"v{Winget.Version} (cũ, sẽ tự cập nhật)";
                        var ids = Winget.InstalledIds();
                        foreach (var a in AppItems.Where(a => ids.Contains(a.Def.Id))) a.IsInstalled = true;
                        AppStatus = $"{AppItems.Count(a => a.IsInstalled)} app trong danh sách đã có trên máy.";
                    }
                    else
                    {
                        WingetOk = false;
                        WingetText = "Chưa có (sẽ tự cài khi cần)";
                        AppStatus = "Máy chưa có winget. Khi bắt đầu, công cụ sẽ tự cài winget rồi mới cài app.";
                    }
                }
                catch (Exception ex) { AppStatus = "Lỗi kiểm tra winget: " + ex.Message; }
            });
            var t3 = Task.Run(() =>
            {
                var a = SysInfo.IsActivated();
                IsActivated = a != false;
                ActivationText = a == true ? "Đã kích hoạt" : a == false ? "Chưa kích hoạt" : "Không rõ";
                RefreshGuard();
            });
            return Task.WhenAll(t1, t2, t3).ContinueWith(_ =>
            {
                IsLoaded = true;
                RefreshCounts();
            });
        }

        void BuildBloat()
        {
            var groups = new List<GroupVM>();
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var removable = _packages.Where(p => !p.NonRemovable).ToList();

            foreach (var g in BloatCatalog.All.GroupBy(b => b.Group))
            {
                var gv = new GroupVM(g.Key);
                foreach (var def in g)
                {
                    var vm = new BloatItemVM(def);
                    foreach (var p in removable.Where(p => def.Patterns.Any(pt => Appx.Like(p.Name, pt))))
                    {
                        vm.Packages.Add(p);
                        matched.Add(p.Name);
                    }
                    var present = def.Kind == BloatKind.OneDrive ? Appx.OneDrivePresent() || vm.Packages.Count > 0 : vm.Packages.Count > 0;
                    if (!present) continue;
                    vm.Refresh();
                    gv.Items.Add(vm);
                }
                if (gv.Items.Count > 0) groups.Add(gv);
            }

            // App không có trong danh mục: liệt kê để người dùng tự chọn, mặc định không tick
            var keepPatterns = BloatCatalog.Keep.Select(k => k.Pattern).ToList();
            var others = new GroupVM("Ứng dụng khác trên máy (không tự chọn)");
            foreach (var p in removable.Where(p => !matched.Contains(p.Name) && !p.IsFramework
                                                   && (p.SignatureKind == "Store" || p.SignatureKind == "Developer")
                                                   && !keepPatterns.Any(k => Appx.Like(p.Name, k))
                                                   && !BloatCatalog.SystemPrefixes.Any(s => p.Name.StartsWith(s, StringComparison.OrdinalIgnoreCase))))
            {
                var vm = new BloatItemVM(p);
                vm.Refresh();
                others.Items.Add(vm);
            }
            if (others.Items.Count > 0) groups.Add(others);

            var keep = BloatCatalog.Keep.Where(k => _packages.Any(p => p.Installed && Appx.Like(p.Name, k.Pattern))).Select(k => k.Name).ToList();
            KeepText = keep.Count > 0 ? string.Join("  •  ", keep) : "(không tìm thấy app Microsoft nào cần giữ)";

            var total = groups.Sum(g => g.Items.Count);
            BloatStatus = total == 0 ? "Máy đã sạch, không còn app rác nào trong danh sách 🎉" : $"Tìm thấy {total} app có thể gỡ.";
            BloatGroups = new ObservableCollection<GroupVM>(groups);
            Raise(nameof(BloatItems));
        }

        public void RefreshGuard()
        {
            GuardOn = Guard.IsActive;
            var n = Guard.BlockList.Count;
            GuardText = GuardOn ? $"Đang bật, chặn {n} mục" : "Chưa bật";
        }

        // ═══ Preset ═══════════════════════════════════════════════════════════
        public void ApplyPreset(Preset p, string scope = "all")
        {
            if (scope == "all" || scope == "bloat")
                foreach (var b in BloatItems)
                    b.IsChecked = !b.IsCustom && (p == Preset.Mine ? (_custom?.Bloat.Contains(b.Key) ?? b.Def.Mine)
                                                : p == Preset.Recommended && b.Def.Recommended);

            if (scope == "all" || scope == "tweak")
                foreach (var t in TweakItems)
                    t.IsChecked = t.IsApplied || (p == Preset.Mine ? (_custom?.Tweaks.Contains(t.Def.Id) ?? t.Def.Mine)
                                                : p == Preset.Recommended && t.Def.Recommended);

            if (scope == "all" || scope == "app")
                foreach (var a in AppItems.Where(a => !a.IsInstalled))
                    a.IsChecked = p == Preset.Mine ? (_custom?.Apps.Contains(a.Def.Id) ?? a.Def.Mine)
                                : p == Preset.Recommended && (a.Def.Popular || (a.Def.PopularLtsc && IsLtsc));

            RefreshCounts();
        }

        public string SaveMine()
        {
            var p = new Profile();
            foreach (var b in BloatItems.Where(b => b.IsChecked && !b.IsCustom)) p.Bloat.Add(b.Key);
            // giữ lại mục "của tôi" cũ không có trên máy này (máy này đã sạch sẵn)
            foreach (var d in BloatCatalog.All.Where(d => !BloatItems.Any(b => b.Key == d.Key) && (_custom?.Bloat.Contains(d.Key) ?? d.Mine))) p.Bloat.Add(d.Key);
            foreach (var t in TweakItems.Where(t => t.IsChecked)) p.Tweaks.Add(t.Def.Id);
            foreach (var a in AppItems.Where(a => a.IsChecked || a.IsInstalled)) p.Apps.Add(a.Def.Id);
            var path = p.Save();
            p.LoadedFrom = path;
            _custom = p;
            Raise(nameof(MineSource));
            return path;
        }

        public void Filter(string text)
        {
            text = (text ?? "").Trim();
            foreach (var g in AppGroups)
            {
                foreach (var i in g.Items)
                    i.Visible = text.Length == 0
                                || i.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                                || (i.Subtitle ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                                || ((AppItemVM)i).Def.Id.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                g.RefreshVisible();
            }
        }

        // ═══ Lập kế hoạch ═════════════════════════════════════════════════════
        public List<Step> BuildPlan()
        {
            var steps = new List<Step>();
            var installs = AppItems.Where(a => a.IsChecked && !a.IsInstalled).ToList();
            var installAppx = installs.Where(a => a.Def.AppxName != null).Select(a => a.Def.AppxName).ToList();

            // App người dùng muốn cài thì không gỡ (vd: tick gỡ Teams nhưng lại tick cài Teams)
            var removes = BloatItems.Where(b => b.IsChecked && !b.Patterns.Any(pt => installAppx.Any(n => Appx.Like(n, pt)))).ToList();
            var tweaks = TweakItems.Where(t => t.IsChange).ToList();

            if (CreateRestorePoint && (removes.Count > 0 || tweaks.Count > 0))
                steps.Add(new Step("Tạo điểm khôi phục hệ thống", ct => RestorePoint.Create("WindowsOptimize " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"))));

            if (removes.Count > 0)
                steps.Add(new Step($"Gỡ {removes.Count} app rác", ct => DoRemove(removes, ct)));

            if (EnableGuard && removes.Count > 0)
                steps.Add(new Step("Bật bảo vệ chống cài lại (Guard)", ct =>
                {
                    Guard.Enable(removes.SelectMany(r => r.Patterns), removes.SelectMany(r => r.Packages).Select(p => p.Family));
                    RefreshGuard();
                    return true;
                }));

            if (tweaks.Count > 0)
                steps.Add(new Step($"Áp dụng {tweaks.Count} tinh chỉnh", ct => DoTweaks(tweaks, ct)));

            if (installs.Count > 0)
            {
                foreach (var name in installAppx) steps.Add(new Step($"Bỏ chặn {name}", ct => { Guard.Unblock(name); return true; }));
                if (installs.Any(a => a.Def.Source == "msstore"))
                    steps.Add(new Step("Kiểm tra Microsoft Store", ct =>
                    {
                        var ok = StoreHelper.Ensure(ct, s => StepText = s);
                        StoreOk = ok; StoreText = ok ? "Đã có" : "Chưa có";
                        return ok;
                    }));
                steps.Add(new Step("Chuẩn bị winget", ct =>
                {
                    var ok = Winget.Ensure(ct, s => StepText = s);
                    WingetOk = ok; WingetText = ok ? $"v{Winget.Version}" : "Lỗi";
                    return ok;
                }));
                var i = 0;
                foreach (var a in installs)
                {
                    var idx = ++i;
                    steps.Add(new Step($"Cài {a.Title} ({idx}/{installs.Count})", ct => DoInstall(a, ct)));
                }
            }

            if (tweaks.Any(t => t.Def.ExplorerRestart))
                steps.Add(new Step("Làm mới Explorer", ct => { TweakEngine.RestartExplorer(); return true; }));

            return steps;
        }

        bool DoRemove(List<BloatItemVM> items, CancellationToken ct)
        {
            foreach (var b in items) b.SetBadge("Đang gỡ...", BadgeKind.Busy);
            var ok = true;
            var appx = items.Where(b => b.Def?.Kind != BloatKind.OneDrive).Select(b => (b.Key, b.Patterns)).ToList();
            if (appx.Count > 0)
            {
                var res = Appx.Remove(appx, (key, status) =>
                {
                    var vm = items.FirstOrDefault(x => x.Key == key);
                    if (vm == null) return;
                    if (status == "FAIL") { vm.SetBadge("Lỗi", BadgeKind.Danger); Log.Err("Gỡ chưa hết: " + vm.Title); }
                    else { vm.SetBadge("Đã gỡ", BadgeKind.Success); Log.Ok("Đã gỡ " + vm.Title); }
                }, ct);
                ok = res.Values.All(v => v != "FAIL");
            }
            foreach (var od in items.Where(b => b.Def?.Kind == BloatKind.OneDrive))
            {
                var r = Appx.RemoveOneDrive();
                od.SetBadge(r ? "Đã gỡ" : "Lỗi", r ? BadgeKind.Success : BadgeKind.Danger);
                if (r) Log.Ok("Đã gỡ OneDrive"); else ok = false;
            }
            foreach (var b in items) { b.IsChecked = false; b.IsEnabled = b.BadgeKind == BadgeKind.Danger; }
            NeedReboot = true;
            return ok;
        }

        bool DoTweaks(List<TweakItemVM> tweaks, CancellationToken ct)
        {
            var ok = true;
            foreach (var t in tweaks)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (t.IsChecked) { TweakEngine.Apply(t.Def); Log.Ok("Bật: " + t.Title); }
                    else { TweakEngine.Revert(t.Def); Log.Ok("Hoàn tác: " + t.Title); }
                }
                catch (Exception ex) { ok = false; Log.Err($"{t.Title}: {ex.Message}"); }
                t.IsApplied = TweakEngine.IsApplied(t.Def);
                if (t.IsChecked != t.IsApplied && t.Def.CustomDetect == null) Log.Warn($"{t.Title}: chưa xác nhận được trạng thái mới.");
                t.IsChecked = t.IsApplied;
            }
            NeedReboot = true;
            return ok;
        }

        bool DoInstall(AppItemVM a, CancellationToken ct)
        {
            if (!Winget.IsReady && Winget.Exe == null) { a.SetBadge("Thiếu winget", BadgeKind.Danger); return false; }
            a.SetBadge("Đang cài...", BadgeKind.Busy);
            var (ok, msg) = Winget.Install(a.Def, ct);
            if (ok)
            {
                a.IsInstalled = true;
                a.SetBadge(msg == "Đã cài" ? "Đã cài" : msg, BadgeKind.Success);
                Log.Ok($"{a.Title}: {msg}");
            }
            else
            {
                a.SetBadge("Lỗi", BadgeKind.Danger);
                Log.Err($"{a.Title}: {msg}");
            }
            return ok;
        }

        // ═══ Thực thi ═════════════════════════════════════════════════════════
        public async Task<bool> RunAsync(List<Step> steps)
        {
            if (IsRunning || steps.Count == 0) return false;
            IsRunning = true; IsFinished = false; NeedReboot = false; Progress = 0; RunSummary = "";
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var failed = new List<string>();
            var started = DateTime.Now;
            Log.Step($"Bắt đầu: {steps.Count} bước · {SysInfo.Summary}");

            await Task.Run(() =>
            {
                for (var i = 0; i < steps.Count; i++)
                {
                    if (ct.IsCancellationRequested) { Log.Warn("Đã huỷ bởi người dùng."); break; }
                    var s = steps[i];
                    StepText = s.Title;
                    Progress = i * 100.0 / steps.Count;
                    Log.Step(s.Title);
                    try
                    {
                        if (!s.Run(ct)) failed.Add(s.Title);
                    }
                    catch (OperationCanceledException) { Log.Warn("Đã huỷ."); break; }
                    catch (Exception ex) { failed.Add(s.Title); Log.Err(ex.Message); }
                }
            });

            Progress = 100;
            var mins = (DateTime.Now - started).TotalMinutes;
            RunSummary = failed.Count == 0
                ? $"Hoàn tất {steps.Count} bước trong {mins:0.#} phút."
                : $"Xong, {failed.Count} bước có lỗi: {string.Join(", ", failed)}. Xem nhật ký bên dưới.";
            StepText = failed.Count == 0 ? "Hoàn tất ✓" : "Hoàn tất (có lỗi)";
            Log.Step(RunSummary);
            Log.Info("Nhật ký đầy đủ: " + Log.FilePath);
            IsRunning = false; IsFinished = true;
            RefreshCounts();
            return failed.Count == 0;
        }

        public void Cancel() => _cts?.Cancel();
    }
}

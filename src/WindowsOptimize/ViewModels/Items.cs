using System;
using System.Collections.ObjectModel;
using System.Linq;
using WindowsOptimize.Catalog;
using WindowsOptimize.Core;
using WindowsOptimize.Engine;

namespace WindowsOptimize.ViewModels
{
    public enum BadgeKind { None, Info, Success, Warn, Danger, Busy }

    /// <summary>Một ô có checkbox (dùng chung cho app rác / tinh chỉnh / app cài).</summary>
    public abstract class ItemVM : Observable
    {
        public static event Action AnyCheckedChanged;

        public string Title { get; protected set; }
        public string Subtitle { get; protected set; }
        public string Tooltip { get; protected set; }

        bool _checked;
        public bool IsChecked
        {
            get => _checked;
            set { if (Set(ref _checked, value)) { OnCheckedChanged(); AnyCheckedChanged?.Invoke(); } }
        }

        protected virtual void OnCheckedChanged() { }

        bool _enabled = true;
        public bool IsEnabled { get => _enabled; set => Set(ref _enabled, value); }

        bool _visible = true;
        public bool Visible { get => _visible; set => Set(ref _visible, value); }

        string _badge;
        public string Badge { get => _badge; set { Set(ref _badge, value); Raise(nameof(HasBadge)); } }
        public bool HasBadge => !string.IsNullOrEmpty(_badge);

        BadgeKind _badgeKind;
        public BadgeKind BadgeKind { get => _badgeKind; set => Set(ref _badgeKind, value); }

        public void SetBadge(string text, BadgeKind kind) { Badge = text; BadgeKind = kind; }
    }

    public sealed class BloatItemVM : ItemVM
    {
        public BloatDef Def { get; }
        public System.Collections.Generic.List<AppxPkg> Packages { get; } = new System.Collections.Generic.List<AppxPkg>();
        public string Key => Def?.Key ?? Patterns[0];
        public bool IsCustom { get; }   // gói không có trong danh mục ("Ứng dụng khác")
        public string[] Patterns { get; }

        public BloatItemVM(BloatDef def)
        {
            Def = def;
            Title = def.Name;
            Subtitle = def.Desc;
            Patterns = def.Patterns;
        }

        public BloatItemVM(AppxPkg pkg)
        {
            IsCustom = true;
            Title = !string.IsNullOrEmpty(pkg.DisplayName) ? pkg.DisplayName : Prettify(pkg.Name);
            Subtitle = pkg.Name;
            Patterns = new[] { pkg.Name };
            Packages.Add(pkg);
        }

        public void Refresh()
        {
            Tooltip = string.Join("\n", Packages.Select(p => p.FullName ?? p.ProvisionedName ?? p.Name));
            if (Def?.Kind == BloatKind.OneDrive) Tooltip = "OneDrive (Win32) + Microsoft.OneDriveSync";
            Raise(nameof(Tooltip));
        }

        /// <summary>"PythonSoftwareFoundation.Python.3.12" → "Python 3.12", "40174MouriNaruto.NanaZip" → "NanaZip"</summary>
        static string Prettify(string name)
        {
            var parts = name.Split('.');
            var rest = parts.Length > 1 ? parts.Skip(1).ToArray() : parts;
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < rest.Length; i++)
            {
                var numeric = rest[i].All(char.IsDigit);
                var prevNumeric = i > 0 && rest[i - 1].All(char.IsDigit);
                if (i > 0) sb.Append(numeric && prevNumeric ? "." : " ");
                sb.Append(rest[i]);
            }
            return sb.ToString();
        }
    }

    public sealed class TweakItemVM : ItemVM
    {
        public Tweak Def { get; }
        bool _applied;
        public bool IsApplied { get => _applied; set { Set(ref _applied, value); UpdateBadge(); } }

        public TweakItemVM(Tweak t)
        {
            Def = t;
            Title = t.Name;
            Subtitle = t.Desc;
            Tooltip = string.Join("\n", t.Regs.Select(r => r.ToString()).Concat(t.Services.Select(s => $"Dịch vụ {s.Name} → {(s.Start == 4 ? "Disabled" : s.Start.ToString())}")));
        }

        public bool IsChange => IsChecked != IsApplied;

        protected override void OnCheckedChanged() => UpdateBadge();

        public void UpdateBadge()
        {
            if (IsApplied && IsChecked) SetBadge("Đang bật", BadgeKind.Success);
            else if (IsApplied && !IsChecked) SetBadge("Sẽ hoàn tác", BadgeKind.Warn);
            else if (!IsApplied && IsChecked) SetBadge("Sẽ bật", BadgeKind.Info);
            else SetBadge(null, BadgeKind.None);
        }
    }

    public sealed class AppItemVM : ItemVM
    {
        public AppDef Def { get; }
        bool _installed;
        public bool IsInstalled
        {
            get => _installed;
            set
            {
                Set(ref _installed, value);
                IsEnabled = !value;
                if (value) { IsChecked = false; SetBadge("Đã cài", BadgeKind.Success); }
            }
        }

        public AppItemVM(AppDef d)
        {
            Def = d;
            Title = d.Name;
            Subtitle = string.IsNullOrEmpty(d.Desc) ? d.Id : d.Desc;
            Tooltip = (d.Source == "msstore" ? "Microsoft Store: " : "winget: ") + d.Id;
            if (d.Source == "msstore") SetBadge("Store", BadgeKind.Info);
        }
    }

    public sealed class GroupVM : Observable
    {
        public string Name { get; }
        public ObservableCollection<ItemVM> Items { get; } = new ObservableCollection<ItemVM>();

        bool _visible = true;
        public bool Visible { get => _visible; set => Set(ref _visible, value); }

        public GroupVM(string name) { Name = name; }

        public void RefreshVisible() => Visible = Items.Any(i => i.Visible);

        public void ToggleAll()
        {
            var targets = Items.Where(i => i.Visible && i.IsEnabled).ToList();
            var check = targets.Any(i => !i.IsChecked);
            foreach (var i in targets) i.IsChecked = check;
        }
    }
}

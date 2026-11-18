using System;
using System.Collections.Generic;
using System.Linq;
using WindowsOptimize.Core;
using static WindowsOptimize.Core.Reg;

namespace WindowsOptimize.Catalog
{
    public sealed class SvcOp
    {
        public string Name;
        public int Start;        // 2 = Automatic, 3 = Manual, 4 = Disabled
        public int DefaultStart;
    }

    public sealed class Tweak
    {
        public string Id;
        public string Group;
        public string Name;
        public string Desc;
        public bool Recommended;
        public bool Mine;
        public bool ExplorerRestart;
        public List<RegOp> Regs = new List<RegOp>();
        public List<SvcOp> Services = new List<SvcOp>();
        public Func<bool> CustomDetect;
        public Action CustomApply;
        public Action CustomRevert;
    }

    /// <summary>
    /// Tinh chỉnh quyền riêng tư / quảng cáo / giao diện, chọn lọc từ RyTuneX, Win11Debloat
    /// và các policy chính thức của Microsoft. Mọi mục đều hoàn tác được.
    /// "Mine" = cấu hình đang dùng trên máy của chủ repo (đọc từ registry thật).
    /// </summary>
    public static class TweakCatalog
    {
        const string GAds = "Quảng cáo & gợi ý";
        const string GPriv = "Theo dõi & quyền riêng tư";
        const string GAi = "AI & Copilot";
        const string GBar = "Taskbar, Start & Tìm kiếm";
        const string GExp = "File Explorer";
        const string GSys = "Hệ thống & cập nhật";

        const string CU = @"HKCU\Software\Microsoft\Windows\CurrentVersion";
        const string CDM = CU + @"\ContentDeliveryManager";
        const string ADV = CU + @"\Explorer\Advanced";
        const string POL = @"HKLM\SOFTWARE\Policies\Microsoft";
        const string UPOL = @"HKCU\Software\Policies\Microsoft";

        static Tweak T(string id, string group, string name, string desc, bool rec, bool mine, params RegOp[] regs)
            => new Tweak { Id = id, Group = group, Name = name, Desc = desc, Recommended = rec, Mine = mine, Regs = regs.ToList() };

        public static readonly List<Tweak> All = Build();

        static List<Tweak> Build()
        {
            var list = new List<Tweak>
            {
                // ═══ Quảng cáo & gợi ý ═══════════════════════════════════════════════
                T("ads.suggestions", GAds, "Tắt quảng cáo & gợi ý trong Start, Settings",
                    "Gợi ý app, mẹo, \"nội dung đề xuất\" trong Start/Settings/thông báo", true, true,
                    Dword(CDM, "SystemPaneSuggestionsEnabled", 0),
                    Dword(CDM, "SubscribedContent-338388Enabled", 0),
                    Dword(CDM, "SubscribedContent-338389Enabled", 0),
                    Dword(CDM, "SubscribedContent-338393Enabled", 0),
                    Dword(CDM, "SubscribedContent-353694Enabled", 0),
                    Dword(CDM, "SubscribedContent-353696Enabled", 0),
                    Dword(CDM, "SubscribedContent-353698Enabled", 0),
                    Dword(CDM, "SoftLandingEnabled", 0)),

                T("ads.silentinstall", GAds, "Chặn Windows tự cài app quảng cáo",
                    "Ngăn Candy Crush, TikTok, Spotify... tự cài sau khi đăng nhập/cập nhật", true, true,
                    Dword(CDM, "SilentInstalledAppsEnabled", 0),
                    Dword(CDM, "PreInstalledAppsEnabled", 0),
                    Dword(CDM, "PreInstalledAppsEverEnabled", 0),
                    Dword(CDM, "OemPreInstalledAppsEnabled", 0),
                    Dword(CDM, "FeatureManagementEnabled", 0),
                    Dword(POL + @"\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1),
                    Dword(POL + @"\Windows\CloudContent", "DisableConsumerAccountStateContent", 1),
                    Dword(POL + @"\Windows\CloudContent", "DisableCloudOptimizedContent", 1)),

                T("ads.lockscreen", GAds, "Tắt mẹo & quảng cáo trên màn hình khoá",
                    "Bỏ chữ \"Fun facts, tips...\" trên lock screen", true, true,
                    Dword(CDM, "RotatingLockScreenOverlayEnabled", 0),
                    Dword(CDM, "SubscribedContent-338387Enabled", 0)),

                T("ads.contentdelivery", GAds, "Tắt hẳn nội dung Microsoft đẩy xuống",
                    "Tắt Content Delivery: Spotlight, hình nền khoá đổi tự động, nội dung quảng bá", false, true,
                    Dword(CDM, "ContentDeliveryAllowed", 0),
                    Dword(CDM, "RotatingLockScreenEnabled", 0)),

                T("ads.adid", GAds, "Tắt mã quảng cáo (Advertising ID)",
                    "App không dùng được ID để hiện quảng cáo theo sở thích", true, true,
                    Dword(CU + @"\AdvertisingInfo", "Enabled", 0),
                    Dword(POL + @"\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1)),

                T("ads.tailored", GAds, "Tắt \"trải nghiệm cá nhân hoá\"",
                    "Microsoft không dùng dữ liệu chẩn đoán để gợi ý/quảng cáo", true, true,
                    Dword(CU + @"\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0),
                    Dword(UPOL + @"\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1)),

                T("ads.explorer", GAds, "Tắt quảng cáo OneDrive/M365 trong File Explorer",
                    "Ẩn thông báo đồng bộ (sync provider) trong Explorer", true, true,
                    Dword(ADV, "ShowSyncProviderNotifications", 0)),

                T("ads.start", GAds, "Tắt đề xuất web & app trong Start",
                    "Không gợi ý app/website, không hiện thông báo tài khoản trong Start", true, true,
                    Dword(ADV, "Start_IrisRecommendations", 0),
                    Dword(ADV, "Start_AccountNotifications", 0)),

                T("ads.scoobe", GAds, "Tắt nhắc \"Hoàn tất thiết lập thiết bị\"",
                    "Màn hình xanh mời dùng OneDrive/Game Pass/M365 sau mỗi bản cập nhật", true, true,
                    Dword(CU + @"\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0),
                    Dword(CDM, "SubscribedContent-310093Enabled", 0)),

                // ═══ Theo dõi ═══════════════════════════════════════════════════════
                new Tweak
                {
                    Id = "priv.telemetry", Group = GPriv, Recommended = true, Mine = true,
                    Name = "Giảm telemetry về mức tối thiểu",
                    Desc = "Tắt dịch vụ thu thập dữ liệu (DiagTrack) và gửi chẩn đoán",
                    Regs =
                    {
                        Dword(POL + @"\Windows\DataCollection", "AllowTelemetry", 0),
                        Dword(POL + @"\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1),
                        Dword(POL + @"\SQMClient\Windows", "CEIPEnable", 0),
                        Dword(POL + @"\Windows\AppCompat", "AITEnable", 0),
                    },
                    Services =
                    {
                        new SvcOp { Name = "DiagTrack", Start = 4, DefaultStart = 2 },
                        new SvcOp { Name = "dmwappushservice", Start = 4, DefaultStart = 3 },
                    },
                },

                T("priv.activity", GPriv, "Tắt lịch sử hoạt động (Activity History)",
                    "Windows không ghi lại app/file bạn đã mở để gửi lên Microsoft", true, true,
                    Dword(POL + @"\Windows\System", "EnableActivityFeed", 0),
                    Dword(POL + @"\Windows\System", "PublishUserActivities", 0),
                    Dword(POL + @"\Windows\System", "UploadUserActivities", 0)),

                T("priv.applaunch", GPriv, "Không theo dõi app hay mở",
                    "Tắt thống kê app được mở để xếp hạng trong Start/Tìm kiếm", true, true,
                    Dword(ADV, "Start_TrackProgs", 0)),

                T("priv.feedback", GPriv, "Không hỏi ý kiến phản hồi",
                    "Tắt popup \"Bạn đánh giá Windows thế nào?\"", true, true,
                    Dword(@"HKCU\Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0)),

                T("priv.inking", GPriv, "Tắt thu thập dữ liệu gõ phím & viết tay",
                    "Không gửi mẫu nhập liệu để \"cải thiện\" bàn phím/viết tay", true, true,
                    Dword(@"HKCU\Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                    Dword(@"HKCU\Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1),
                    Dword(@"HKCU\Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0),
                    Dword(@"HKCU\Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0),
                    Dword(@"HKCU\Software\Microsoft\Input\TIPC", "Enabled", 0)),

                T("priv.speech", GPriv, "Tắt nhận dạng giọng nói trực tuyến",
                    "Giọng nói không bị gửi lên đám mây Microsoft", true, true,
                    Dword(@"HKCU\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0)),

                T("priv.location", GPriv, "Tắt dịch vụ định vị",
                    "Ảnh hưởng Thời tiết, Find my device, tự đổi múi giờ", false, false,
                    Dword(POL + @"\Windows\LocationAndSensors", "DisableLocation", 1)),

                new Tweak
                {
                    Id = "priv.wer", Group = GPriv, Name = "Tắt báo cáo lỗi Windows (Error Reporting)",
                    Desc = "Không gửi báo cáo khi app bị crash",
                    Regs = { Dword(POL + @"\Windows\Windows Error Reporting", "Disabled", 1) },
                    Services = { new SvcOp { Name = "WerSvc", Start = 4, DefaultStart = 3 } },
                },

                T("priv.edge", GPriv, "Hạn chế Edge thu thập dữ liệu & quảng cáo",
                    "Tắt mua sắm, gợi ý, gửi dữ liệu cá nhân hoá của Edge (Edge sẽ báo \"do tổ chức quản lý\")", false, false,
                    Dword(POL + @"\Edge", "PersonalizationReportingEnabled", 0),
                    Dword(POL + @"\Edge", "DiagnosticData", 0),
                    Dword(POL + @"\Edge", "EdgeShoppingAssistantEnabled", 0),
                    Dword(POL + @"\Edge", "ShowRecommendationsEnabled", 0),
                    Dword(POL + @"\Edge", "SpotlightExperiencesAndRecommendationsEnabled", 0),
                    Dword(POL + @"\Edge", "HubsSidebarEnabled", 0)),

                // ═══ AI ═════════════════════════════════════════════════════════════
                T("ai.copilot", GAi, "Tắt Copilot",
                    "Tắt Copilot của Windows và nút Copilot trên taskbar", true, true,
                    Dword(POL + @"\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                    Dword(UPOL + @"\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                    Dword(ADV, "ShowCopilotButton", 0, optional: true)),

                T("ai.recall", GAi, "Tắt Recall (AI chụp màn hình định kỳ)",
                    "Không lưu ảnh chụp màn hình để AI phân tích", true, true,
                    Dword(POL + @"\Windows\WindowsAI", "DisableAIDataAnalysis", 1),
                    Dword(POL + @"\Windows\WindowsAI", "AllowRecallEnablement", 0),
                    Dword(POL + @"\Windows\WindowsAI", "TurnOffSavingSnapshots", 1),
                    Dword(UPOL + @"\Windows\WindowsAI", "DisableAIDataAnalysis", 1)),

                T("ai.clicktodo", GAi, "Tắt Click to Do",
                    "Tắt lớp AI phân tích nội dung trên màn hình", true, true,
                    Dword(POL + @"\Windows\WindowsAI", "DisableClickToDo", 1),
                    Dword(UPOL + @"\Windows\WindowsAI", "DisableClickToDo", 1)),

                T("ai.apps", GAi, "Tắt AI trong Paint & Notepad",
                    "Ẩn Cocreator, Image Creator, Rewrite...", false, false,
                    Dword(POL + @"\WindowsNotepad", "DisableAIFeatures", 1),
                    Dword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableCocreator", 1),
                    Dword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableImageCreator", 1),
                    Dword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeFill", 1),
                    Dword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeErase", 1),
                    Dword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableRemoveBackground", 1)),

                // ═══ Taskbar / Start / Search ════════════════════════════════════════
                T("bar.widgets", GBar, "Tắt Widgets (tin tức, thời tiết)",
                    "Bỏ bảng tin MSN và icon thời tiết trên taskbar", true, true,
                    Dword(POL + @"\Dsh", "AllowNewsAndInterests", 0),
                    Dword(ADV, "TaskbarDa", 0, optional: true)),

                T("bar.chat", GBar, "Ẩn Chat/Teams trên taskbar", "", true, true,
                    Dword(ADV, "TaskbarMn", 0, optional: true),
                    Dword(POL + @"\Windows\Windows Chat", "ChatIcon", 3)),

                T("bar.taskview", GBar, "Ẩn nút Task View", "Vẫn dùng được Win+Tab", false, true,
                    Dword(ADV, "ShowTaskViewButton", 0)),

                T("bar.search", GBar, "Ẩn ô Tìm kiếm trên taskbar", "Bấm phím Windows rồi gõ để tìm như cũ", false, true,
                    Dword(CU + @"\Search", "SearchboxTaskbarMode", 0)),

                T("bar.websearch", GBar, "Tắt kết quả Bing/web khi tìm trong Start",
                    "Tìm kiếm chỉ trả về app và file trong máy, nhanh hơn", true, true,
                    Dword(CU + @"\Search", "BingSearchEnabled", 0),
                    Dword(UPOL + @"\Windows\Explorer", "DisableSearchBoxSuggestions", 1)),

                T("bar.highlights", GBar, "Tắt Search highlights",
                    "Bỏ hình minh hoạ, sự kiện trong ô tìm kiếm", true, true,
                    Dword(CU + @"\SearchSettings", "IsDynamicSearchBoxEnabled", 0),
                    Dword(POL + @"\Windows\Windows Search", "EnableDynamicContentInSearchBox", 0)),

                T("bar.endtask", GBar, "Thêm \"End task\" khi chuột phải app trên taskbar",
                    "Tắt app bị treo nhanh, không cần Task Manager", true, true,
                    Dword(ADV + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 1)),

                T("bar.left", GBar, "Căn taskbar sang trái", "Giống Windows 10", false, false,
                    Dword(ADV, "TaskbarAl", 0)),

                // ═══ File Explorer ═══════════════════════════════════════════════════
                T("exp.ext", GExp, "Hiện đuôi file (.exe, .pdf, .docx...)", "Tránh mở nhầm file giả mạo", true, true,
                    Dword(ADV, "HideFileExt", 0)),

                T("exp.hidden", GExp, "Hiện file & thư mục ẩn", "", false, true,
                    Dword(ADV, "Hidden", 1)),

                T("exp.thispc", GExp, "Mở File Explorer vào \"This PC\"", "Thay vì trang Home", true, true,
                    Dword(ADV, "LaunchTo", 1)),

                T("exp.classicmenu", GExp, "Menu chuột phải kiểu cũ (Windows 10)",
                    "Bỏ bước \"Show more options\"", false, false,
                    Str(@"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "")),

                T("exp.recent", GExp, "Không hiện file/thư mục gần đây",
                    "Home của Explorer không lộ file bạn vừa mở", false, false,
                    Dword(CU + @"\Explorer", "ShowRecent", 0),
                    Dword(CU + @"\Explorer", "ShowFrequent", 0)),

                // ═══ Hệ thống ════════════════════════════════════════════════════════
                T("sys.dark", GSys, "Bật chế độ tối (Dark mode)", "", false, true,
                    Dword(CU + @"\Themes\Personalize", "AppsUseLightTheme", 0, 1),
                    Dword(CU + @"\Themes\Personalize", "SystemUsesLightTheme", 0, 1)),

                T("sys.gamedvr", GSys, "Tắt Game DVR (quay nền khi chơi game)",
                    "Tăng FPS; Game Bar vẫn mở được nhưng không tự ghi hình", false, true,
                    Dword(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0, 1),
                    Dword(CU + @"\GameDVR", "AppCaptureEnabled", 0),
                    Dword(POL + @"\Windows\GameDVR", "AllowGameDVR", 0)),

                T("sys.faststartup", GSys, "Tắt Fast Startup",
                    "Tắt máy là tắt hẳn: tránh lỗi driver, dual-boot, cập nhật treo", false, true,
                    Dword(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, 1)),

                T("sys.stickykeys", GSys, "Tắt hộp thoại Sticky Keys (Shift 5 lần)", "Hay bật nhầm khi chơi game", true, false,
                    Str(@"HKCU\Control Panel\Accessibility\StickyKeys", "Flags", "506", "510")),

                T("sys.mouseaccel", GSys, "Tắt tăng tốc chuột (Enhance pointer precision)", "Chuột chính xác hơn khi chơi game", false, false,
                    Str(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0", "1"),
                    Str(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0", "6"),
                    Str(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0", "10")),

                T("sys.noautoreboot", GSys, "Không tự khởi động lại khi đang dùng máy",
                    "Windows Update chờ bạn tự restart", true, false,
                    Dword(POL + @"\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1)),

                T("sys.deliveryopt", GSys, "Tắt chia sẻ bản cập nhật qua Internet (P2P)",
                    "Không dùng mạng của bạn để gửi bản cập nhật cho máy khác", true, false,
                    Dword(POL + @"\Windows\DeliveryOptimization", "DODownloadMode", 0)),

                T("sys.wudrivers", GSys, "Không cài driver qua Windows Update",
                    "Dùng khi Windows Update cứ đè driver bạn tự cài", false, false,
                    Dword(POL + @"\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1)),

                T("sys.edgeboost", GSys, "Không cho Edge chạy ngầm",
                    "Tắt Startup boost và chạy nền của Edge", false, false,
                    Dword(POL + @"\Edge", "StartupBoostEnabled", 0),
                    Dword(POL + @"\Edge", "BackgroundModeEnabled", 0)),

                new Tweak
                {
                    Id = "sys.hibernate", Group = GSys, Name = "Tắt ngủ đông (Hibernate)",
                    Desc = "Giải phóng vài GB ổ C (laptop nên giữ)",
                    CustomDetect = () => Get(@"HKLM\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled") is int v && v == 0,
                    CustomApply = () => Shell.Run(System.IO.Path.Combine(Paths.System32, "powercfg.exe"), "/hibernate off"),
                    CustomRevert = () => Shell.Run(System.IO.Path.Combine(Paths.System32, "powercfg.exe"), "/hibernate on"),
                },

                new Tweak
                {
                    Id = "sys.highperf", Group = GSys, Name = "Dùng gói nguồn High performance",
                    Desc = "Cho máy bàn; laptop sẽ hao pin hơn",
                    CustomDetect = () => Shell.Run(System.IO.Path.Combine(Paths.System32, "powercfg.exe"), "/getactivescheme")
                        .Output.IndexOf("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase) >= 0,
                    CustomApply = () => Shell.Run(System.IO.Path.Combine(Paths.System32, "powercfg.exe"), "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
                    CustomRevert = () => Shell.Run(System.IO.Path.Combine(Paths.System32, "powercfg.exe"), "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"),
                },
            };

            // Hoàn tác menu kiểu cũ = xoá hẳn khoá CLSID (khoá rỗng vẫn có thể giữ menu cũ)
            list.First(t => t.Id == "exp.classicmenu").CustomRevert =
                () => Reg.DeleteKey(@"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}");

            foreach (var t in list)
                t.ExplorerRestart = t.Group == GBar || t.Group == GExp || t.Id.StartsWith("ads.") || t.Id == "ai.copilot" || t.Id == "sys.dark";
            return list;
        }
    }
}

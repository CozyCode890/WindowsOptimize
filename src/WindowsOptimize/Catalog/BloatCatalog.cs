using System.Collections.Generic;

namespace WindowsOptimize.Catalog
{
    public enum BloatKind { Appx, OneDrive }

    public sealed class BloatDef
    {
        public string Key;          // định danh ổn định (dùng cho profile)
        public string Name;
        public string Desc;
        public string Group;
        public string[] Patterns;   // tên gói Appx, hỗ trợ ký tự đại diện *
        public bool Recommended;    // preset "Đề xuất": an toàn cho mọi người
        public bool Mine;           // preset "Của tôi"
        public BloatKind Kind = BloatKind.Appx;
    }

    /// <summary>
    /// Danh sách app rác của Windows 11 bản mới cài.
    /// Kiểm chứng bằng VM Windows 11 sạch (xem docs/fresh-install-apps.md).
    /// </summary>
    public static class BloatCatalog
    {
        const string GAds = "Quảng cáo & app tài trợ";
        const string GMs = "App Microsoft ít dùng";
        const string GAi = "AI & Copilot";
        const string GLink = "Liên kết, đồng bộ & mạng xã hội";
        const string GXbox = "Xbox & Game";
        const string GOpt = "Tiện ích tuỳ chọn";

        static BloatDef B(string key, string group, string name, string desc, bool rec, bool mine, params string[] patterns)
            => new BloatDef { Key = key, Group = group, Name = name, Desc = desc, Recommended = rec, Mine = mine, Patterns = patterns.Length > 0 ? patterns : new[] { key } };

        public static readonly List<BloatDef> All = new List<BloatDef>
        {
            // ── Quảng cáo / app bên thứ ba được đẩy xuống ─────────────────────────────
            B("sponsored", GAds, "App quảng cáo bên thứ ba", "Candy Crush, TikTok, Spotify, Disney+, Instagram, Facebook... do Windows tự cài", true, true,
                "*CandyCrush*", "king.com.*", "*BubbleWitch*", "*MarchofEmpires*", "*HiddenCity*", "*Disney*", "BytedancePte.Ltd.TikTok",
                "Facebook.Facebook", "Facebook.Instagram*", "*Twitter*", "SpotifyAB.SpotifyMusic", "*Netflix*", "AmazonVideo.PrimeVideo",
                "*Duolingo*", "*PicsArt*", "*Hulu*", "*Royal Revolt*", "*Asphalt*", "*Plex*", "*Shazam*", "*Flipboard*",
                "*Sling*", "*PandoraMedia*", "*AdobeSystemsIncorporated.AdobePhotoshopExpress*", "*EclipseManager*",
                "*ActiproSoftware*", "*Dolby*", "*Wunderlist*", "*CaesarsSlots*", "*FarmVille*", "*Microsoft.MinecraftEducationEdition*"),
            B("7EE7776C.LinkedInforWindows", GAds, "LinkedIn", "App LinkedIn cài sẵn", true, true),

            // ── Microsoft ít dùng ────────────────────────────────────────────────────
            B("Microsoft.BingNews", GMs, "Microsoft News", "Tin tức Microsoft Start", true, true),
            B("Microsoft.BingWeather", GMs, "Thời tiết (Weather)", "App thời tiết MSN", true, true),
            B("Microsoft.BingSearch", GMs, "Bing Search", "App Bing tách riêng", true, true),
            B("Microsoft.MicrosoftSolitaireCollection", GMs, "Solitaire Collection", "Game bài có quảng cáo", true, true),
            B("Microsoft.Getstarted", GMs, "Tips (Mẹo)", "Mẹo sử dụng Windows", true, true),
            B("Microsoft.GetHelp", GMs, "Get Help", "Trợ giúp trực tuyến", true, true),
            B("Microsoft.WindowsFeedbackHub", GMs, "Feedback Hub", "Gửi phản hồi cho Microsoft", true, true),
            B("Microsoft.Todos", GMs, "Microsoft To Do", "Danh sách việc cần làm", true, true),
            B("Microsoft.PowerAutomateDesktop", GMs, "Power Automate", "Tự động hoá quy trình (doanh nghiệp)", true, true),
            B("Clipchamp.Clipchamp", GMs, "Clipchamp", "Biên tập video online", true, true),
            B("MicrosoftCorporationII.MicrosoftFamily", GMs, "Microsoft Family", "Quản lý gia đình", true, true),
            B("Microsoft.MicrosoftOfficeHub", GMs, "Microsoft 365 Copilot (Office Hub)", "Trang quảng bá Office, không phải Office thật", true, true),
            B("Microsoft.OutlookForWindows", GMs, "Outlook (mới)", "Outlook bản web đóng gói", true, true),
            B("Microsoft.Windows.DevHome", GMs, "Dev Home", "Đã bị Microsoft khai tử", true, true),
            B("Microsoft.MicrosoftJournal", GMs, "Journal", "Ghi chú viết tay", true, true),
            B("Microsoft.Whiteboard", GMs, "Whiteboard", "Bảng trắng", true, true),
            B("Microsoft.MicrosoftPCManager", GMs, "PC Manager", "Dọn dẹp kiểu Microsoft", true, true),
            B("Microsoft.WindowsMaps", GMs, "Bản đồ (Maps)", "Bản đồ Bing", true, true),
            B("Microsoft.People", GMs, "People", "Danh bạ cũ", true, true),
            B("microsoft.windowscommunicationsapps", GMs, "Mail & Calendar (cũ)", "Đã ngừng hỗ trợ", true, true),
            B("Microsoft.ZuneVideo", GMs, "Phim & TV", "Movies & TV", true, true),
            B("Microsoft.SkypeApp", GMs, "Skype", "Đã ngừng hoạt động", true, true),
            B("Microsoft.Office.OneNote", GMs, "OneNote for Windows 10", "Bản UWP cũ", true, true),
            B("Microsoft.MixedReality.Portal", GMs, "Mixed Reality Portal", "Kính thực tế ảo", true, true),
            B("Microsoft.Microsoft3DViewer", GMs, "3D Viewer", "Xem file 3D", true, true),
            B("Microsoft.Messaging", GMs, "Messaging", "Nhắn tin cũ", true, true),
            B("Microsoft.OneConnect", GMs, "Mobile Plans", "Gói cước di động", true, true),
            B("Microsoft.549981C3F5F10", GMs, "Cortana", "Trợ lý ảo cũ", true, true),
            B("Microsoft.Wallet", GMs, "Wallet", "Ví điện tử cũ", true, true),
            B("MSTeams", GMs, "Microsoft Teams", "Teams cài sẵn (cài lại được ở tab Cài ứng dụng)", true, false),

            // ── AI ────────────────────────────────────────────────────────────────────
            B("Microsoft.Copilot", GAi, "Copilot", "App Copilot", true, true, "Microsoft.Copilot", "Microsoft.Windows.Copilot"),
            B("Microsoft.Windows.Ai.Copilot.Provider", GAi, "Copilot Provider", "Thành phần Copilot của Windows", true, true),
            B("MicrosoftWindows.Client.WebExperience", GAi, "Widgets", "Bảng tin tức/thời tiết trên taskbar", true, true),
            B("Microsoft.Edge.GameAssist", GAi, "Edge Game Assist", "Trình duyệt trong game", true, true),

            // ── Liên kết ──────────────────────────────────────────────────────────────
            B("Microsoft.YourPhone", GLink, "Phone Link", "Kết nối điện thoại", false, true),
            B("MicrosoftWindows.CrossDevice", GLink, "Cross Device Experience", "Dùng chung với Phone Link", false, true),
            B("Microsoft.StartExperiencesApp", GLink, "Start Experiences", "Bảng điện thoại trong Start", false, true),
            new BloatDef { Key = "onedrive", Group = GLink, Name = "OneDrive", Desc = "Gỡ OneDrive và không cài lại cho user mới", Recommended = false, Mine = true, Kind = BloatKind.OneDrive, Patterns = new[] { "Microsoft.OneDriveSync" } },

            // ── Xbox ──────────────────────────────────────────────────────────────────
            B("Microsoft.GamingApp", GXbox, "Xbox", "App Xbox / Game Pass", false, true),
            B("Microsoft.XboxGamingOverlay", GXbox, "Xbox Game Bar", "Win+G, quay màn hình game", false, true),
            B("Microsoft.XboxIdentityProvider", GXbox, "Xbox Identity Provider", "Đăng nhập Xbox trong game", false, true),
            B("Microsoft.XboxSpeechToTextOverlay", GXbox, "Xbox Speech to Text", "Chuyển giọng nói trong game", false, true),
            B("Microsoft.Xbox.TCUI", GXbox, "Xbox TCUI", "Giao diện bạn bè Xbox", false, true),
            B("Microsoft.XboxApp", GXbox, "Xbox Console Companion", "App Xbox cũ", true, true),

            // ── Tuỳ chọn ──────────────────────────────────────────────────────────────
            B("Microsoft.MicrosoftStickyNotes", GOpt, "Sticky Notes", "Ghi chú dán", false, true),
            B("Microsoft.WindowsAlarms", GOpt, "Đồng hồ (Clock)", "Báo thức, hẹn giờ, Focus", false, true),
            B("MicrosoftCorporationII.QuickAssist", GOpt, "Quick Assist", "Điều khiển máy từ xa của Microsoft", false, true),
        };

        /// <summary>
        /// App Microsoft LUÔN giữ lại: không xuất hiện để gỡ, Guard không bao giờ đụng tới.
        /// </summary>
        public static readonly List<(string Pattern, string Name)> Keep = new List<(string, string)>
        {
            ("Microsoft.Windows.Photos", "Photos (Ảnh)"),
            ("Microsoft.ScreenSketch", "Snipping Tool"),
            ("Microsoft.WindowsCalculator", "Calculator (Máy tính)"),
            ("Microsoft.WindowsSoundRecorder", "Sound Recorder (Ghi âm)"),
            ("Microsoft.WindowsNotepad", "Notepad"),
            ("Microsoft.Paint", "Paint"),
            ("Microsoft.WindowsTerminal", "Terminal"),
            ("Microsoft.ZuneMusic", "Media Player"),
            ("Microsoft.WindowsCamera", "Camera"),
            ("Microsoft.WindowsStore", "Microsoft Store"),
            ("Microsoft.StorePurchaseApp", "Store Purchase"),
            ("Microsoft.DesktopAppInstaller", "App Installer (winget)"),
            ("Microsoft.SecHealthUI", "Windows Security"),
            ("Microsoft.HEIFImageExtension", "HEIF Image Extension"),
            ("Microsoft.HEVCVideoExtension", "HEVC Video Extension"),
            ("Microsoft.WebpImageExtension", "WebP Image Extension"),
            ("Microsoft.VP9VideoExtensions", "VP9 Video Extension"),
            ("Microsoft.RawImageExtension", "Raw Image Extension"),
            ("Microsoft.AV1VideoExtension", "AV1 Video Extension"),
            ("Microsoft.WebMediaExtensions", "Web Media Extensions"),
            ("Microsoft.AVCEncoderVideoExtension", "AVC Encoder"),
            ("Microsoft.MPEG2VideoExtension", "MPEG-2 Video"),
            ("Microsoft.ApplicationCompatibilityEnhancements", "App Compatibility"),
        };

        /// <summary>Gói hệ thống/thư viện: không bao giờ liệt kê ở mục "Ứng dụng khác".</summary>
        public static readonly string[] SystemPrefixes =
        {
            "Microsoft.VCLibs", "Microsoft.NET.", "Microsoft.UI.Xaml", "Microsoft.WindowsAppRuntime", "Microsoft.Services.Store",
            "Microsoft.DirectXRuntime", "Microsoft.LanguageExperiencePack", "Microsoft.Winget.", "Microsoft.WidgetsPlatformRuntime",
            "MicrosoftCorporationII.WinAppRuntime", "MicrosoftWindows.", "Microsoft.Windows.", "Microsoft.Advertising",
            "Microsoft.Media.PlayReady", "Microsoft.WinJS", "windows.", "Windows.", "Microsoft.AAD.", "Microsoft.AccountsControl",
            "Microsoft.AsyncTextService", "Microsoft.BioEnrollment", "Microsoft.CredDialogHost", "Microsoft.ECApp",
            "Microsoft.LockApp", "Microsoft.MicrosoftEdge", "Microsoft.Win32WebViewHost", "Microsoft.XboxGameCallableUI",
            "Microsoft.Speech", "Microsoft.OneDriveSync", "NcsiUwpApp", "Microsoft.Office.ActionsServer", "Microsoft.OfficePushNotificationUtility",
            "1527c705-839a-4832-9118-54d4Bd6a0c89", "c5e2524a-ea46-4f67-841f-6a9465d9d515", "E2A4F912-2574-4A75-9BB0-0D023378592B",
            "F46D4000-FD22-4DB4-AC8E-4E1DDDE828FE", "Microsoft.SecHealthUI", "Microsoft.Edge", "aimgr", "Microsoft.MicrosoftEdgeDevToolsClient",
        };
    }
}

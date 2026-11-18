using System.Collections.Generic;

namespace WindowsOptimize.Catalog
{
    public sealed class AppDef
    {
        public string Id;            // winget ID hoặc Store ID
        public string Name;
        public string Desc;
        public string Category;
        public string Source = "winget";   // winget | msstore
        public bool Mine;            // preset "Của tôi"
        public bool Popular;         // preset "Phổ biến"
        public bool PopularLtsc;     // chỉ đưa vào "Phổ biến" khi máy là LTSC (thiếu app Microsoft)
        public string AppxName;      // tên gói Appx (để biết đã cài, và gỡ khỏi danh sách chặn của Guard)
        public bool AsUser;          // installer từ chối chạy bằng quyền admin → chạy bằng quyền user
    }

    /// <summary>
    /// Mọi ID đã được kiểm tra bằng `winget show --id ... -e` (11/2026).
    /// "Mine" = app đang cài trên máy của chủ repo.
    /// </summary>
    public static class AppCatalog
    {
        const string CBrowser = "Trình duyệt";
        const string CChat = "Liên lạc & điều khiển từ xa";
        const string CVn = "Gõ tiếng Việt";
        const string COffice = "Văn phòng & tài liệu";
        const string CMedia = "Âm thanh, video & đồ hoạ";
        const string CUtil = "Tiện ích";
        const string CDev = "Lập trình";
        const string CAi = "AI";
        const string CGame = "Game";
        const string CRuntime = "Thư viện hệ thống (runtime)";
        const string CMs = "Ứng dụng Microsoft (cho LTSC / cài lại)";

        static AppDef A(string cat, string id, string name, string desc, bool mine = false, bool pop = false)
            => new AppDef { Category = cat, Id = id, Name = name, Desc = desc, Mine = mine, Popular = pop };

        static AppDef S(string cat, string id, string name, string desc, string appx, bool mine = false, bool popLtsc = false)
            => new AppDef { Category = cat, Id = id, Name = name, Desc = desc, Source = "msstore", AppxName = appx, Mine = mine, PopularLtsc = popLtsc };

        public static readonly List<AppDef> All = new List<AppDef>
        {
            // Trình duyệt
            A(CBrowser, "Google.Chrome", "Google Chrome", "Trình duyệt phổ biến nhất", mine: true, pop: true),
            A(CBrowser, "Mozilla.Firefox", "Firefox", "Trình duyệt mã nguồn mở"),
            A(CBrowser, "Brave.Brave", "Brave", "Chặn quảng cáo sẵn"),
            A(CBrowser, "CocCoc.CocCoc", "Cốc Cốc", "Trình duyệt Việt, tải video"),

            // Liên lạc
            A(CChat, "VNGCorp.Zalo", "Zalo", "Nhắn tin", mine: true, pop: true),
            A(CChat, "Telegram.TelegramDesktop", "Telegram", "Nhắn tin", mine: true, pop: true),
            A(CChat, "Caprine.Caprine", "Caprine", "Messenger cho máy tính", mine: true),
            A(CChat, "Discord.Discord", "Discord", "Chat, gọi thoại cộng đồng"),
            A(CChat, "Zoom.Zoom", "Zoom", "Họp trực tuyến"),
            new AppDef { Category = CChat, Id = "Microsoft.Teams", Name = "Microsoft Teams", Desc = "Họp, học online", Mine = true, AppxName = "MSTeams" },
            A(CChat, "DucFabulous.UltraViewer", "UltraViewer", "Điều khiển máy từ xa", mine: true, pop: true),
            A(CChat, "TeamViewer.TeamViewer", "TeamViewer", "Điều khiển máy từ xa"),
            A(CChat, "AnyDesk.AnyDesk", "AnyDesk", "Điều khiển máy từ xa"),

            // Gõ tiếng Việt
            A(CVn, "UniKey.UniKey", "UniKey", "Bộ gõ tiếng Việt", pop: true),
            A(CVn, "lamquangminh.EVKey", "EVKey", "Bộ gõ tiếng Việt nhẹ"),
            A(CVn, "Tuyenvm.OpenKey", "OpenKey", "Bộ gõ mã nguồn mở"),

            // Văn phòng
            A(COffice, "Microsoft.Office", "Microsoft 365 (Office)", "Word, Excel, PowerPoint...", mine: true),
            A(COffice, "TheDocumentFoundation.LibreOffice", "LibreOffice", "Bộ văn phòng miễn phí"),
            A(COffice, "Foxit.FoxitReader", "Foxit PDF Reader", "Đọc, ký PDF", mine: true, pop: true),
            A(COffice, "SumatraPDF.SumatraPDF", "SumatraPDF", "Đọc PDF siêu nhẹ"),
            A(COffice, "Adobe.Acrobat.Reader.64-bit", "Adobe Acrobat Reader", "Đọc PDF"),
            A(COffice, "Notion.Notion", "Notion", "Ghi chú, quản lý công việc", mine: true),
            A(COffice, "Obsidian.Obsidian", "Obsidian", "Ghi chú Markdown"),
            A(COffice, "JohnMacFarlane.Pandoc", "Pandoc", "Chuyển đổi định dạng tài liệu", mine: true),
            A(COffice, "MiKTeX.MiKTeX", "MiKTeX", "LaTeX", mine: true),
            A(COffice, "UB-Mannheim.TesseractOCR", "Tesseract OCR", "Nhận dạng chữ trong ảnh", mine: true),

            // Media
            A(CMedia, "VideoLAN.VLC", "VLC", "Xem mọi định dạng video", pop: true),
            A(CMedia, "Daum.PotPlayer", "PotPlayer", "Trình xem video"),
            A(CMedia, "CodecGuide.K-LiteCodecPack.Standard", "K-Lite Codec Pack", "Bộ codec video"),
            new AppDef { Category = CMedia, Id = "Spotify.Spotify", Name = "Spotify", Desc = "Nghe nhạc", AsUser = true },
            A(CMedia, "OBSProject.OBSStudio", "OBS Studio", "Quay màn hình, livestream"),
            A(CMedia, "FxSound.FxSound", "FxSound", "Tăng chất lượng âm thanh", mine: true),
            A(CMedia, "Canva.Affinity", "Affinity", "Thiết kế đồ hoạ", mine: true),

            // Tiện ích
            A(CUtil, "7zip.7zip", "7-Zip", "Nén/giải nén", pop: true),
            A(CUtil, "M2Team.NanaZip", "NanaZip", "7-Zip giao diện Windows 11", mine: true),
            A(CUtil, "Microsoft.PowerToys", "PowerToys", "Bộ tiện ích của Microsoft"),
            A(CUtil, "voidtools.Everything", "Everything", "Tìm file tức thì"),
            A(CUtil, "ShareX.ShareX", "ShareX", "Chụp, quay màn hình"),
            A(CUtil, "QL-Win.QuickLook", "QuickLook", "Nhấn Space để xem nhanh file", mine: true),
            A(CUtil, "CodeSector.TeraCopy", "TeraCopy", "Copy file nhanh, kiểm tra lỗi", mine: true),
            A(CUtil, "AntibodySoftware.WizTree", "WizTree", "Xem cái gì chiếm ổ cứng", mine: true),
            A(CUtil, "RevoUninstaller.RevoUninstaller", "Revo Uninstaller", "Gỡ app sạch sẽ", mine: true),
            A(CUtil, "LocalSend.LocalSend", "LocalSend", "Gửi file qua Wi-Fi tới điện thoại", mine: true),
            A(CUtil, "Devolutions.UniGetUI", "UniGetUI", "Giao diện cập nhật mọi app", mine: true),
            A(CUtil, "Cloudflare.Warp", "Cloudflare WARP", "DNS 1.1.1.1 / VPN", mine: true),
            A(CUtil, "qBittorrent.qBittorrent", "qBittorrent", "Tải torrent", mine: true),
            A(CUtil, "Tonec.InternetDownloadManager", "IDM", "Tăng tốc tải file (có phí)"),
            A(CUtil, "Notepad++.Notepad++", "Notepad++", "Soạn thảo văn bản/code"),
            A(CUtil, "Rufus.Rufus", "Rufus", "Tạo USB cài Windows"),
            A(CUtil, "Ventoy.Ventoy", "Ventoy", "USB chứa nhiều file ISO"),
            A(CUtil, "CPUID.CPU-Z", "CPU-Z", "Thông tin phần cứng"),
            A(CUtil, "REALiX.HWiNFO", "HWiNFO", "Theo dõi nhiệt độ, phần cứng"),
            A(CUtil, "CrystalDewWorld.CrystalDiskInfo", "CrystalDiskInfo", "Sức khoẻ ổ cứng"),
            A(CUtil, "Ookla.Speedtest.Desktop", "Speedtest", "Đo tốc độ mạng"),
            A(CUtil, "Bitwarden.Bitwarden", "Bitwarden", "Quản lý mật khẩu"),
            A(CUtil, "BartoszCichecki.LenovoLegionToolkit", "Lenovo Legion Toolkit", "Chỉ cho laptop Lenovo Legion", mine: true),
            S(CUtil, "9NBLGGH1ZBKW", "Dynamic Theme", "Đổi hình nền Bing/Spotlight hằng ngày", "55888ChristopheLavalle.DynamicTheme", mine: true),

            // Lập trình
            A(CDev, "Microsoft.VisualStudioCode", "Visual Studio Code", "Trình soạn code", mine: true),
            A(CDev, "Google.Antigravity", "Antigravity", "IDE AI của Google", mine: true),
            A(CDev, "Git.Git", "Git", "Quản lý mã nguồn", mine: true),
            A(CDev, "GitHub.cli", "GitHub CLI", "Lệnh gh", mine: true),
            A(CDev, "GitHub.GitHubDesktop", "GitHub Desktop", "Git có giao diện"),
            A(CDev, "JesseDuffield.lazygit", "lazygit", "Git trong terminal", mine: true),
            new AppDef { Category = CDev, Id = "Microsoft.WindowsTerminal", Name = "Windows Terminal", Desc = "Terminal hiện đại", Mine = true, AppxName = "Microsoft.WindowsTerminal" },
            A(CDev, "Microsoft.PowerShell", "PowerShell 7", "PowerShell mới nhất", mine: true),
            A(CDev, "Starship.Starship", "Starship", "Prompt terminal đẹp", mine: true),
            A(CDev, "BurntSushi.ripgrep.MSVC", "ripgrep", "Tìm chữ trong code siêu nhanh", mine: true),
            A(CDev, "Python.Python.3.12", "Python 3.12", "", mine: true),
            A(CDev, "Python.Python.3.13", "Python 3.13", ""),
            A(CDev, "CondaForge.Miniforge3", "Miniforge (conda)", "Môi trường Python/khoa học dữ liệu", mine: true),
            A(CDev, "OpenJS.NodeJS.LTS", "Node.js LTS", ""),
            A(CDev, "Microsoft.DotNet.SDK.10", ".NET SDK 10", "", mine: true),
            A(CDev, "EclipseAdoptium.Temurin.21.JDK", "Java JDK 21 (Temurin)", "", mine: true),
            A(CDev, "EclipseAdoptium.Temurin.25.JDK", "Java JDK 25 (Temurin)", "", mine: true),
            A(CDev, "Oracle.MySQL", "MySQL", "Cơ sở dữ liệu", mine: true),
            A(CDev, "Oracle.MySQLWorkbench", "MySQL Workbench", "Quản lý MySQL", mine: true),
            A(CDev, "Docker.DockerDesktop", "Docker Desktop", "Container"),
            A(CDev, "Postman.Postman", "Postman", "Test API"),
            A(CDev, "JetBrains.Toolbox", "JetBrains Toolbox", "Cài IntelliJ, PyCharm..."),
            A(CDev, "Termius.Termius", "Termius", "SSH client", mine: true),
            A(CDev, "SoftwareFreedomConservancy.QEMU", "QEMU", "Giả lập máy ảo", mine: true),
            A(CDev, "Google.PlatformTools", "Android Platform Tools", "adb, fastboot", mine: true),
            S(CDev, "9PPGN2WM50QB", "ADB Explorer", "Quản lý file Android qua adb", "57163AlexSSB.ADBExplorer", mine: true),
            A(CDev, "UniversityOfWaikato.Weka", "Weka", "Khai phá dữ liệu / ML", mine: true),

            // AI
            A(CAi, "Anthropic.Claude", "Claude", "Trợ lý AI của Anthropic", mine: true),
            A(CAi, "Google.GoogleAppForDesktop", "Google app", "Tìm kiếm & Gemini (Alt+Space)", mine: true),

            // Game
            A(CGame, "Valve.Steam", "Steam", ""),
            A(CGame, "EpicGames.EpicGamesLauncher", "Epic Games", ""),
            S(CGame, "9MV0B5HZVK9Z", "Xbox", "Game Pass", "Microsoft.GamingApp"),
            A(CGame, "Microsoft.DirectX", "DirectX (bản cũ)", "Cho game đời cũ báo thiếu d3dx9"),

            // Runtime
            A(CRuntime, "Microsoft.VCRedist.2015+.x64", "Visual C++ 2015-2022 (x64)", "Nhiều app/game cần", mine: true, pop: true),
            A(CRuntime, "Microsoft.VCRedist.2015+.x86", "Visual C++ 2015-2022 (x86)", "Nhiều app/game cần", mine: true, pop: true),
            A(CRuntime, "Microsoft.DotNet.DesktopRuntime.8", ".NET Desktop Runtime 8", "", mine: true, pop: true),
            A(CRuntime, "Microsoft.DotNet.DesktopRuntime.9", ".NET Desktop Runtime 9", "", mine: true),
            A(CRuntime, "Microsoft.DotNet.DesktopRuntime.10", ".NET Desktop Runtime 10", ""),
            A(CRuntime, "Oracle.JavaRuntimeEnvironment", "Java 8 (JRE)", "Cho app Java cũ"),

            // App Microsoft: LTSC không có sẵn, hoặc để cài lại nếu lỡ gỡ
            S(CMs, "9WZDNCRFJBH4", "Photos (Ảnh)", "", "Microsoft.Windows.Photos", mine: true, popLtsc: true),
            S(CMs, "9MZ95KL8MR0L", "Snipping Tool", "Chụp & quay màn hình", "Microsoft.ScreenSketch", mine: true, popLtsc: true),
            S(CMs, "9WZDNCRFHVN5", "Calculator (Máy tính)", "", "Microsoft.WindowsCalculator", mine: true, popLtsc: true),
            S(CMs, "9WZDNCRFHWKN", "Sound Recorder (Ghi âm)", "", "Microsoft.WindowsSoundRecorder", mine: true, popLtsc: true),
            S(CMs, "9MSMLRH6LZF3", "Notepad", "", "Microsoft.WindowsNotepad", mine: true, popLtsc: true),
            S(CMs, "9PCFS5B6T72H", "Paint", "", "Microsoft.Paint", popLtsc: true),
            S(CMs, "9WZDNCRFJ3PT", "Media Player", "", "Microsoft.ZuneMusic", mine: true, popLtsc: true),
            S(CMs, "9WZDNCRFJBBG", "Camera", "", "Microsoft.WindowsCamera"),
            S(CMs, "9WZDNCRFJ3PR", "Clock (Đồng hồ)", "", "Microsoft.WindowsAlarms"),
            S(CMs, "9NBLGGH4QGHW", "Sticky Notes", "", "Microsoft.MicrosoftStickyNotes"),
            S(CMs, "9PMMSR1CGPWG", "HEIF Image Extension", "Mở ảnh .heic từ iPhone", "Microsoft.HEIFImageExtension", popLtsc: true),
            S(CMs, "9PG2DK419DRG", "WebP Image Extension", "", "Microsoft.WebpImageExtension", popLtsc: true),
            S(CMs, "9N4D0MSMP0PT", "VP9 Video Extension", "", "Microsoft.VP9VideoExtensions", popLtsc: true),
            S(CMs, "9NCTDW2W1BH8", "Raw Image Extension", "Ảnh RAW máy ảnh", "Microsoft.RawImageExtension"),
            S(CMs, "9MVZQVXJBQ9V", "AV1 Video Extension", "", "Microsoft.AV1VideoExtension", mine: true, popLtsc: true),
        };
    }
}

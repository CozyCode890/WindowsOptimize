# WindowsOptimize

Thiết lập Windows 10/11 sau khi cài mới chỉ bằng **1 file exe**: gỡ app rác, chặn Windows Update cài lại,
tắt quảng cáo/theo dõi/widget/Copilot, cài sẵn app hay dùng, cài winget/Microsoft Store cho bản LTSC,
mở MAS để kích hoạt.

- Không cần cài gì thêm: chạy trên .NET Framework 4.8 có sẵn trong mọi bản Windows 10 1903+ và Windows 11.
- Không cần `Set-ExecutionPolicy`, không cần clone repo. Mở exe → bấm "Có" ở hộp thoại UAC → dùng.
- Mọi tinh chỉnh đều hoàn tác được, và có tạo điểm khôi phục hệ thống trước khi chạy.

## Cách chạy

**Cách 1:** tải `WindowsOptimize.exe` ở [Releases](https://github.com/CozyCode890/WindowsOptimize/releases/latest) rồi mở.
Nếu Windows hiện "Windows protected your PC" (do exe chưa ký số), bấm **More info → Run anyway**.

**Cách 2:** dùng 1 dòng lệnh, mở PowerShell và dán:

```powershell
irm https://github.com/CozyCode890/WindowsOptimize/releases/latest/download/run.ps1 | iex
```

## Có gì bên trong

| Tab | Làm gì |
|---|---|
| **Tổng quan** | 2 nút 1-click: **Của tôi** và **Đề xuất**. Trạng thái bản quyền, winget, Store, Guard. |
| **Gỡ app rác** | Chỉ liệt kê app *thực sự có trên máy*. Nhóm theo: quảng cáo/app tài trợ, app Microsoft ít dùng, AI/Copilot, Phone Link/OneDrive, Xbox, tiện ích tuỳ chọn. App lạ khác trên máy được liệt kê riêng và không tự tick. |
| **Riêng tư & tinh chỉnh** | 46 tinh chỉnh kiểu RyTuneX: quảng cáo, telemetry, Copilot/Recall, widget, Bing trong Start, Explorer, dark mode... Hiện **trạng thái thật** trên máy (đọc registry), bỏ tick để hoàn tác. |
| **Cài ứng dụng** | ~110 app qua winget/Store, có ô tìm kiếm. App đã cài được đánh dấu và bỏ qua. |
| **Công cụ** | Mở MAS, cài/sửa winget, cài Microsoft Store cho LTSC, bật/tắt Guard, điểm khôi phục, nhật ký. |

### Luôn giữ lại

Photos, Snipping Tool, Calculator, Sound Recorder, Notepad, Paint, Terminal, Media Player, Camera,
Microsoft Store, App Installer (winget), Windows Security và các codec (HEIF, HEVC, WebP, VP9, AV1, Raw).
Các app này không bao giờ xuất hiện ở tab gỡ, và Guard không bao giờ đụng tới chúng.

### Preset

| | Gỡ app | Tinh chỉnh | Cài app |
|---|---|---|---|
| **Của tôi** | Gỡ sạch, gồm cả OneDrive, Xbox, Phone Link, Sticky Notes, Clock | Đúng cấu hình đang dùng trên máy chủ repo (đọc từ registry thật) | Bộ app của chủ repo (cờ `mine: true` trong [AppCatalog.cs](src/WindowsOptimize/Catalog/AppCatalog.cs)) |
| **Đề xuất** | Chỉ app rác an toàn để gỡ; giữ OneDrive, Xbox, Phone Link | Tắt quảng cáo, theo dõi, Copilot/Recall, widget, Bing; hiện đuôi file | Chrome, Zalo, Telegram, UniKey, UltraViewer, VLC, 7-Zip, Foxit, VC++/.NET runtime. Trên LTSC thêm Photos, Calculator, Snipping Tool, Notepad, Paint, Media Player, codec. |

Muốn đổi bộ "Của tôi" mà không build lại: tick theo ý → **Công cụ → Lưu lựa chọn hiện tại**.
File `WindowsOptimize.profile.txt` được tạo cạnh exe; mang cả 2 file sang máy khác là dùng được.

## Chặn cài lại qua Windows Update (Guard)

Khi bật ô **Chặn cài lại** (mặc định), các app đã gỡ được chặn theo 4 lớp:

1. Khoá `AppxAllUserStore\Deprovisioned\<PackageFamilyName>`: cách chính thức để feature update không cài lại gói đã gỡ.
2. Policy `RemoveDefaultMicrosoftStorePackages` (Windows 11 25H2+, có hiệu lực trên Enterprise/Education).
3. Tắt Consumer Features và silent install cho cả user hiện tại lẫn user mới (`C:\Users\Default`), đồng thời xoá trigger tự cài Outlook mới/Dev Home.
4. Task `\WindowsOptimize\AppGuard` chạy bằng SYSTEM khi khởi động, khi đăng nhập, hằng ngày và **sau mỗi lần Windows Update cài xong**, tự gỡ lại nếu app vẫn lọt vào. Nhật ký ghi ở `C:\ProgramData\WindowsOptimize\guard.log`.

Muốn cài lại 1 app đã chặn: tick nó ở tab **Cài ứng dụng** (tự bỏ chặn), hoặc **Công cụ → Tắt & bỏ chặn tất cả**.

## winget & Microsoft Store trên LTSC

- **winget thiếu hoặc quá cũ** (LTSC, Server, máy mới chưa cập nhật Store): công cụ tự đăng ký lại App Installer.
  Nếu vẫn không được, nó tải bản chính thức từ [microsoft/winget-cli](https://github.com/microsoft/winget-cli/releases) (kèm VCLibs, UI.Xaml, WindowsAppRuntime) rồi cài.
- **Không có Microsoft Store** (LTSC/IoT LTSC): chạy `wsreset -i` (Windows tự tải Store từ Microsoft).
  Nếu không được, dùng gói từ [minihub/LTSC-Add-MicrosoftStore](https://github.com/minihub/LTSC-Add-MicrosoftStore). Các gói đều có chữ ký của Microsoft.
- Store chỉ được cài khi bạn chọn app từ Store (Photos, Snipping Tool...), hoặc bấm nút ở tab Công cụ.

## Kích hoạt (MAS)

Nút **Kích hoạt (MAS)** mở [Microsoft Activation Scripts](https://massgrave.dev) trong cửa sổ PowerShell riêng
(`irm https://get.activated.win | iex`, tự chuyển sang DNS-over-HTTPS nếu nhà mạng chặn).
MAS là script mã nguồn mở của bên thứ ba có menu riêng; WindowsOptimize chỉ mở nó, không tự chọn thay bạn.

## Chạy không giao diện

```text
WindowsOptimize.exe --auto mine|recommended [--no-debloat] [--no-tweaks] [--no-apps] [--no-guard] [--no-restore] [--log file]
WindowsOptimize.exe --report file
```

## Build

```cmd
build.cmd            :: → dist\WindowsOptimize.exe  (cần .NET SDK 8+)
```

Đẩy tag `v1.2.3` lên GitHub → Actions tự build và tạo Release có `WindowsOptimize.exe` + `run.ps1`.

## Kiểm thử trên máy ảo Hyper-V

Danh sách app rác được kiểm chứng trên **Windows 11 bản mới cài** trong VM:

```powershell
# 1. Tải ISO Windows 11 chính thức: https://www.microsoft.com/software-download/windows11 (để trong Downloads)
# 2. Tạo VM cài tự động (Gen 2, vTPM, Secure Boot), xong tạo checkpoint "Fresh install"
pwsh -File tools\New-TestVM.ps1
# 3. Chạy exe trong VM, lấy báo cáo trước/sau vào docs\vm-reports\
pwsh -File tools\Test-InVM.ps1 -Restore
```

Kết quả: [docs/fresh-install-apps.md](docs/fresh-install-apps.md).

## Cấu trúc

```text
src/WindowsOptimize/
  Catalog/   BloatCatalog.cs (app rác + danh sách giữ lại) · TweakCatalog.cs · AppCatalog.cs
  Engine/    Appx (quét/gỡ) · Guard · TweakEngine · Winget · StoreHelper · Extras (MAS, restore point)
  ViewModels/ MainVM (preset, kế hoạch chạy) · Items · Profile ("Của tôi")
  Resources/guard.ps1   script Guard chạy bằng SYSTEM
tools/       New-TestVM.ps1 · Test-InVM.ps1 · autounattend.xml
```

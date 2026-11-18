# App có sẵn trên Windows 11 bản mới cài

> **Trạng thái: chưa kiểm chứng trên VM.** Danh sách trong [BloatCatalog.cs](../src/WindowsOptimize/Catalog/BloatCatalog.cs)
> hiện dựa trên tài liệu của Microsoft, Win11Debloat, RyTuneX và máy LTSC của chủ repo.
> Chạy `tools\New-TestVM.ps1` + `tools\Test-InVM.ps1` để cập nhật file này bằng dữ liệu thật
> (báo cáo đầy đủ nằm ở `docs/vm-reports/`).

## Cách lấy dữ liệu

1. Tải ISO Windows 11 chính thức tại <https://www.microsoft.com/software-download/windows11>.
2. `pwsh -File tools\New-TestVM.ps1`: VM Gen 2, vTPM, Secure Boot, cài tự động; vùng Việt Nam;
   màn quyền riêng tư chọn "Accept" như người dùng bình thường. Sau khi vào desktop, chờ thêm 5 phút
   để Content Delivery kịp tải app tài trợ, rồi tạo checkpoint `Fresh install`.
3. `pwsh -File tools\Test-InVM.ps1`: xuất `--report` trước và sau khi chạy `--auto recommended`.

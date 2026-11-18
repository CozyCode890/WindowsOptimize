<#
  WindowsOptimize Guard
  Chạy bằng tài khoản SYSTEM qua Task Scheduler: khi khởi động, khi đăng nhập, mỗi ngày
  và sau mỗi lần Windows Update cài xong. Gỡ lại các app rác mà Windows lén cài lại.

  Danh sách chặn: C:\ProgramData\WindowsOptimize\blocklist.txt (mỗi dòng 1 tên gói, hỗ trợ *)
  Nhật ký:        C:\ProgramData\WindowsOptimize\guard.log
  Gỡ Guard:       mở WindowsOptimize.exe → Công cụ → "Tắt bảo vệ & bỏ chặn"
#>
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
$dir = Join-Path $env:ProgramData 'WindowsOptimize'
$log = Join-Path $dir 'guard.log'
$listFile = Join-Path $dir 'blocklist.txt'

function Write-GuardLog([string]$m) { Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $m) -Encoding UTF8 }

if (-not (Test-Path $listFile)) { exit 0 }
if ((Test-Path $log) -and (Get-Item $log).Length -gt 1MB) { Move-Item $log "$log.old" -Force }

$names = Get-Content $listFile -Encoding UTF8 | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') }
if (-not $names) { exit 0 }

$prov = @(Get-AppxProvisionedPackage -Online)
$removed = 0
foreach ($n in $names) {
    foreach ($p in @(Get-AppxPackage -AllUsers -Name $n)) {
        if ($p.NonRemovable) { continue }
        Remove-AppxPackage -Package $p.PackageFullName -AllUsers
        if ($?) { $removed++; Write-GuardLog "Gỡ lại: $($p.PackageFullName)" }
    }
    foreach ($v in @($prov | Where-Object { $_.DisplayName -like $n })) {
        Remove-AppxProvisionedPackage -Online -PackageName $v.PackageName -AllUsers | Out-Null
        if ($?) { $removed++; Write-GuardLog "Bỏ provisioned: $($v.PackageName)" }
    }
}

# Windows Update dùng các khoá này để tự cài Outlook mới / Dev Home sau OOBE
$orch = 'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe'
$map = @{ 'Microsoft.OutlookForWindows' = 'OutlookUpdate'; 'Microsoft.Windows.DevHome' = 'DevHomeUpdate' }
foreach ($k in $map.Keys) {
    if ($names -contains $k -and (Test-Path "$orch\$($map[$k])")) {
        Remove-Item "$orch\$($map[$k])" -Recurse -Force
        Write-GuardLog "Xoá trigger tự cài: $($map[$k])"
    }
}

Write-GuardLog "Kiểm tra xong ($($names.Count) mục, gỡ $removed)."

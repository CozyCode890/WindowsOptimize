<#
.SYNOPSIS
  Tạo VM Hyper-V Windows 11 sạch (cài tự động) để lấy danh sách app mới cài & thử WindowsOptimize.

.DESCRIPTION
  1. Tìm ISO Windows 11 chính thức (mặc định: file Win11*.iso mới nhất trong thư mục Downloads)
  2. Tạo ISO phụ chứa autounattend.xml (dùng IMAPI2 có sẵn trong Windows, không cần ADK)
  3. Tạo VM Gen 2: Secure Boot + vTPM, 4 GB RAM, 4 CPU, ổ 80 GB (dynamic), mạng "Default Switch"
  4. Khởi động, tự "Press any key to boot from CD", chờ cài xong và đăng nhập
  5. Tháo ISO, tạo checkpoint "Fresh install" để quay lại trạng thái mới cài bất cứ lúc nào

  Cần: thuộc nhóm "Hyper-V Administrators" (không cần chạy admin).
  Tải ISO: https://www.microsoft.com/software-download/windows11
           → "Download Windows 11 Disk Image (ISO) for x64 devices" → English (United States)

.EXAMPLE
  pwsh -File tools\New-TestVM.ps1
  pwsh -File tools\New-TestVM.ps1 -IsoPath D:\Win11_25H2_English_x64.iso -VMName Win11-Test
#>
[CmdletBinding()]
param(
    [string]$IsoPath,
    [string]$VMName = 'Win11-Fresh',
    [int64]$MemoryGB = 4,
    [int]$Cpu = 4,
    [int64]$DiskGB = 80,
    [string]$SwitchName = 'Default Switch',
    [string]$VMRoot,
    [int]$TimeoutMinutes = 90
)
$ErrorActionPreference = 'Stop'
Import-Module Hyper-V -ErrorAction Stop
function Say($m, $c = 'Cyan') { Write-Host ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $m) -ForegroundColor $c }

# ── 1. ISO ───────────────────────────────────────────────────────────────────
if (-not $IsoPath) {
    $dl = Join-Path $env:USERPROFILE 'Downloads'
    $IsoPath = Get-ChildItem $dl -Filter '*.iso' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'Win.?11|Windows.?11' -and $_.Length -gt 3GB } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $IsoPath -or -not (Test-Path $IsoPath)) {
    throw "Không tìm thấy ISO Windows 11. Tải tại https://www.microsoft.com/software-download/windows11 rồi chạy lại (hoặc truyền -IsoPath)."
}
Say "ISO: $IsoPath ($([math]::Round((Get-Item $IsoPath).Length/1GB,2)) GB)"

if (Get-VM -Name $VMName -ErrorAction SilentlyContinue) { throw "VM '$VMName' đã tồn tại. Xoá đi hoặc dùng -VMName khác." }
if (-not (Get-VMSwitch -Name $SwitchName -ErrorAction SilentlyContinue)) { throw "Không có virtual switch '$SwitchName'." }

if (-not $VMRoot) { $VMRoot = Join-Path (Get-VMHost).VirtualHardDiskPath $VMName }
New-Item -ItemType Directory -Force -Path $VMRoot | Out-Null
$vhd = Join-Path $VMRoot "$VMName.vhdx"
$unattendIso = Join-Path $VMRoot 'autounattend.iso'

# ── 2. VM ────────────────────────────────────────────────────────────────────
Say "Tạo VM $VMName (Gen 2, $MemoryGB GB RAM, $Cpu CPU, ổ $DiskGB GB)..."
$vm = New-VM -Name $VMName -Generation 2 -MemoryStartupBytes ($MemoryGB * 1GB) -NewVHDPath $vhd -NewVHDSizeBytes ($DiskGB * 1GB) -SwitchName $SwitchName -Path $VMRoot
Set-VMProcessor -VMName $VMName -Count $Cpu
Set-VMMemory -VMName $VMName -DynamicMemoryEnabled $false
Set-VM -VMName $VMName -AutomaticCheckpointsEnabled $false -CheckpointType Standard
Enable-VMIntegrationService -VMName $VMName -Name 'Guest Service Interface' -ErrorAction SilentlyContinue

$bypass = ''
try {
    Set-VMKeyProtector -VMName $VMName -NewLocalKeyProtector
    Enable-VMTPM -VMName $VMName
    Say 'Đã bật vTPM + Secure Boot (cài chuẩn như máy thật).' Green
} catch {
    Say "Không bật được vTPM ($($_.Exception.Message)) → dùng LabConfig để Setup bỏ qua kiểm tra TPM." Yellow
    $bypass = @'
<RunSynchronous>
        <RunSynchronousCommand wcm:action="add"><Order>1</Order><Path>reg add HKLM\SYSTEM\Setup\LabConfig /v BypassTPMCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>
        <RunSynchronousCommand wcm:action="add"><Order>2</Order><Path>reg add HKLM\SYSTEM\Setup\LabConfig /v BypassSecureBootCheck /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>
      </RunSynchronous>
'@
}

# ── 3. ISO phụ chứa autounattend.xml (IMAPI2, có sẵn trong Windows) ──────────
$stage = Join-Path ([IO.Path]::GetTempPath()) "wo-unattend-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $stage | Out-Null
(Get-Content (Join-Path $PSScriptRoot 'autounattend.xml') -Raw).Replace('{{BYPASS}}', $bypass) |
    Set-Content (Join-Path $stage 'autounattend.xml') -Encoding UTF8
if (-not ('WoIsoWriter' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
public static class WoIsoWriter {
    public static void Write(string path, object stream) {
        var s = (IStream)stream;
        var buf = new byte[1 << 16];
        IntPtr pcb = Marshal.AllocHGlobal(4);
        try {
            using (var fs = System.IO.File.Create(path)) {
                while (true) {
                    s.Read(buf, buf.Length, pcb);
                    int n = Marshal.ReadInt32(pcb);
                    if (n <= 0) break;
                    fs.Write(buf, 0, n);
                }
            }
        } finally { Marshal.FreeHGlobal(pcb); }
    }
}
'@
}
$fsi = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
$fsi.FileSystemsToCreate = 3   # ISO9660 + Joliet
$fsi.VolumeName = 'UNATTEND'
$fsi.Root.AddTree($stage, $false)
[WoIsoWriter]::Write($unattendIso, $fsi.CreateResultImage().ImageStream)
Remove-Item $stage -Recurse -Force
Say "Đã tạo $unattendIso"

$dvd = Add-VMDvdDrive -VMName $VMName -Path $IsoPath -Passthru
Add-VMDvdDrive -VMName $VMName -Path $unattendIso
Set-VMFirmware -VMName $VMName -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows -FirstBootDevice $dvd

# ── 4. Khởi động + bấm phím "boot from CD" ───────────────────────────────────
Say 'Khởi động VM...'
Start-VM -VMName $VMName
$kb = $null
for ($i = 0; $i -lt 20 -and -not $kb; $i++) {
    Start-Sleep -Milliseconds 500
    $kb = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_Keyboard | Where-Object SystemName -eq $vm.Id.Guid
}
for ($i = 0; $i -lt 30; $i++) {
    if ($kb) { Invoke-CimMethod -InputObject $kb -MethodName TypeKey -Arguments @{ keyCode = 13 } | Out-Null }
    Start-Sleep -Milliseconds 500
}
Say 'Windows Setup đang chạy (thường mất 15-40 phút). Có thể xem bằng: vmconnect localhost' $VMName

# ── 5. Chờ cài xong (PowerShell Direct đăng nhập được) ──────────────────────
$cred = New-Object PSCredential('tester', (ConvertTo-SecureString 'Test@12345' -AsPlainText -Force))
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$ready = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 30
    try {
        $r = Invoke-Command -VMName $VMName -Credential $cred -ErrorAction Stop -ScriptBlock {
            [pscustomobject]@{ Explorer = [bool](Get-Process explorer -ErrorAction SilentlyContinue); Build = (Get-CimInstance Win32_OperatingSystem).BuildNumber }
        }
        if ($r.Explorer) { $ready = $true; Say "Đã vào desktop (build $($r.Build))." Green; break }
        Say 'Đã cài xong, chờ đăng nhập...'
    } catch { Write-Host '.' -NoNewline }
}
if (-not $ready) { throw "Quá $TimeoutMinutes phút mà chưa cài xong. Mở vmconnect để xem." }

Say 'Chờ 5 phút để Windows tải/cài nốt app mặc định (Content Delivery, Store)...'
Start-Sleep -Seconds 300

Get-VMDvdDrive -VMName $VMName | Set-VMDvdDrive -Path $null
Checkpoint-VM -VMName $VMName -SnapshotName 'Fresh install'
Say "Xong! Đã tạo checkpoint 'Fresh install'. Chạy tiếp: pwsh -File tools\Test-InVM.ps1 -VMName $VMName" Green

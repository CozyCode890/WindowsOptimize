<#
.SYNOPSIS
  Tạo ISO Windows 11 (UEFI) từ file ESD chính thức của Microsoft, giống cách Media Creation Tool làm.
  Không cần quyền admin (dùng wimlib + IMAPI2 có sẵn trong Windows).

.DESCRIPTION
  ESD tải từ danh mục của Media Creation Tool (go.microsoft.com/fwlink/?LinkId=2156292 → products.cab),
  file nằm trên CDN dl.delivery.mp.microsoft.com, có SHA1 trong danh mục để đối chiếu.
  Cấu trúc ESD: index 1 = Windows Setup Media, 2 = WinPE, 3 = Windows Setup, 4+ = các edition.

.EXAMPLE
  pwsh -File tools\Build-Win11Iso.ps1 -Esd C:\WinIsoBuild\win11.esd -Wimlib C:\WinIsoBuild\wimlib\bin\wimlib-imagex.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Esd,
    [Parameter(Mandatory)][string]$Wimlib,
    [string]$Edition = 'Windows 11 Pro',
    [string]$OutIso,
    [string]$WorkDir = (Join-Path (Split-Path $Esd) 'iso-tree')
)
$ErrorActionPreference = 'Stop'
function Say($m, $c = 'Cyan') { Write-Host ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $m) -ForegroundColor $c }
function Wim { & $Wimlib @args; if ($LASTEXITCODE -ne 0) { throw "wimlib-imagex $($args[0]) lỗi ($LASTEXITCODE)" } }

# ── Đọc danh sách image trong ESD ────────────────────────────────────────────
$info = & $Wimlib info $Esd
$images = @(); $cur = $null
foreach ($l in $info) {
    if ($l -match '^Index:\s+(\d+)') { $cur = [ordered]@{ Index = [int]$matches[1] }; $images += $cur }
    elseif ($cur -and $l -match '^Name:\s+(.+)$') { $cur.Name = $matches[1].Trim() }
    elseif ($cur -and $l -match '^Display Name:\s+(.+)$') { $cur.Display = $matches[1].Trim() }
    elseif ($cur -and $l -match '^Build:\s+(\d+)') { $cur.Build = $matches[1] }
    elseif ($cur -and $l -match '^Service Pack Build:\s+(\d+)') { $cur.Ubr = $matches[1] }
}
$images | ForEach-Object { Write-Host ("  {0,2}. {1}" -f $_.Index, $_.Name) }
$ed = $images | Where-Object { $_.Name -eq $Edition } | Select-Object -First 1
if (-not $ed) { throw "Không thấy edition '$Edition' trong ESD." }
if (-not $OutIso) {
    $OutIso = Join-Path $env:USERPROFILE ("Downloads\Win11_{0}.{1}_{2}_x64_en-US.iso" -f $ed.Build, $ed.Ubr, ($Edition -replace '^Windows 11 ', '' -replace '\s', ''))
}

# ── Dựng cây thư mục ISO ─────────────────────────────────────────────────────
if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }
New-Item -ItemType Directory -Path $WorkDir | Out-Null
Say 'Bung Windows Setup Media (index 1)...'
Wim apply $Esd 1 $WorkDir --no-acls
$boot = Join-Path $WorkDir 'sources\boot.wim'
Say 'Tạo boot.wim (WinPE + Setup)...'
Wim export $Esd 2 $boot --compress=LZX
Wim export $Esd 3 $boot --boot
Say "Xuất '$Edition' (index $($ed.Index)) → sources\install.esd (mất vài phút)..."
Wim export $Esd $ed.Index (Join-Path $WorkDir 'sources\install.esd') --solid

# ── Đóng ISO UEFI bằng IMAPI2 ────────────────────────────────────────────────
if (-not ('WoIsoWriter' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
public static class WoIsoWriter {
    public static void Write(string path, object stream) {
        var s = (IStream)stream;
        var buf = new byte[1 << 20];
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
$efi = Join-Path $WorkDir 'efi\microsoft\boot\efisys_noprompt.bin'
if (-not (Test-Path $efi)) { $efi = Join-Path $WorkDir 'efi\microsoft\boot\efisys.bin' }
Say "Đóng ISO (UEFI, $(Split-Path $efi -Leaf))..."
$bootStream = New-Object -ComObject ADODB.Stream
$bootStream.Open(); $bootStream.Type = 1; $bootStream.LoadFromFile($efi)
$bootOpt = New-Object -ComObject IMAPI2FS.BootOptions
$bootOpt.AssignBootImage($bootStream)
$bootOpt.PlatformId = 0xEF   # UEFI
$bootOpt.Emulation = 0       # no emulation
$fsi = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
$fsi.ChooseImageDefaultsForMediaType(18)   # BD-R: đủ chỗ
$fsi.FileSystemsToCreate = 4               # UDF (file > 4 GB)
$fsi.UDFRevision = 0x102
$fsi.VolumeName = ('CCCOMA_X64FRE_EN-US_DV9')
$fsi.BootImageOptions = $bootOpt
$fsi.Root.AddTree($WorkDir, $false)
if (Test-Path $OutIso) { Remove-Item $OutIso -Force }
[WoIsoWriter]::Write($OutIso, $fsi.CreateResultImage().ImageStream)
$bootStream.Close()
Remove-Item $WorkDir -Recurse -Force
Say ("Xong: {0} ({1:N2} GB)" -f $OutIso, ((Get-Item $OutIso).Length / 1GB)) Green

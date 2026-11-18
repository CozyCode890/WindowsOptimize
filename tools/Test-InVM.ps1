<#
.SYNOPSIS
  Thử WindowsOptimize.exe trong VM (qua PowerShell Direct) và lấy báo cáo trước/sau.

.DESCRIPTION
  - Copy exe vào VM
  - Xuất báo cáo app/tinh chỉnh của bản Windows mới cài  → docs\vm-reports\<tên>-before.txt
  - Chạy --auto <preset> (mặc định: recommended, không cài app)  → log
  - Xuất báo cáo sau khi chạy                            → docs\vm-reports\<tên>-after.txt
  - Kiểm tra Guard: chạy task AppGuard và đọc guard.log

.EXAMPLE
  pwsh -File tools\Test-InVM.ps1                              # Đề xuất, không cài app
  pwsh -File tools\Test-InVM.ps1 -Preset mine -WithApps       # Bộ "Của tôi" + cài app (lâu)
  pwsh -File tools\Test-InVM.ps1 -Restore                     # quay về checkpoint "Fresh install" trước
#>
[CmdletBinding()]
param(
    [string]$VMName = 'Win11-Fresh',
    [ValidateSet('recommended', 'mine')][string]$Preset = 'recommended',
    [switch]$WithApps,
    [switch]$Restore,
    [string]$Exe = (Join-Path $PSScriptRoot '..\dist\WindowsOptimize.exe')
)
$ErrorActionPreference = 'Stop'
function Say($m, $c = 'Cyan') { Write-Host ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $m) -ForegroundColor $c }

$Exe = (Resolve-Path $Exe).Path
$out = Join-Path $PSScriptRoot '..\docs\vm-reports'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$cred = New-Object PSCredential('tester', (ConvertTo-SecureString 'Test@12345' -AsPlainText -Force))

if ($Restore) {
    Say "Quay VM về checkpoint 'Fresh install'..."
    Restore-VMCheckpoint -VMName $VMName -Name 'Fresh install' -Confirm:$false
    if ((Get-VM $VMName).State -ne 'Running') { Start-VM $VMName }
    do { Start-Sleep 10; $ok = $false; try { $ok = Invoke-Command -VMName $VMName -Credential $cred { [bool](Get-Process explorer -ea 0) } -ea Stop } catch {} } until ($ok)
    Start-Sleep 30
}

$s = New-PSSession -VMName $VMName -Credential $cred
try {
    Invoke-Command -Session $s { New-Item -ItemType Directory -Force C:\wo | Out-Null }
    Copy-Item $Exe -Destination C:\wo\WindowsOptimize.exe -ToSession $s
    $tag = Invoke-Command -Session $s { $v = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'; "$($v.DisplayVersion)-$($v.CurrentBuild).$($v.UBR)" }

    Say 'Báo cáo TRƯỚC khi chạy...'
    Invoke-Command -Session $s { Start-Process C:\wo\WindowsOptimize.exe -ArgumentList '--report', 'C:\wo\before.txt' -Wait }
    Copy-Item C:\wo\before.txt -Destination (Join-Path $out "win11-$tag-before.txt") -FromSession $s

    $runArgs = @('--auto', $Preset, '--log', 'C:\wo\run.log')
    if (-not $WithApps) { $runArgs += '--no-apps' }
    Say "Chạy WindowsOptimize $($runArgs -join ' ') ..."
    $code = Invoke-Command -Session $s { param($a) (Start-Process C:\wo\WindowsOptimize.exe -ArgumentList $a -Wait -PassThru).ExitCode } -ArgumentList (, $runArgs)
    Say "Mã thoát: $code" ($(if ($code -eq 0) { 'Green' } else { 'Yellow' }))
    Copy-Item C:\wo\run.log -Destination (Join-Path $out "win11-$tag-run-$Preset.log") -FromSession $s

    Say 'Báo cáo SAU khi chạy...'
    Invoke-Command -Session $s { Start-Process C:\wo\WindowsOptimize.exe -ArgumentList '--report', 'C:\wo\after.txt' -Wait }
    Copy-Item C:\wo\after.txt -Destination (Join-Path $out "win11-$tag-after-$Preset.txt") -FromSession $s

    Say 'Chạy thử Guard...'
    $g = Invoke-Command -Session $s {
        Start-ScheduledTask -TaskPath '\WindowsOptimize\' -TaskName 'AppGuard' -ErrorAction SilentlyContinue
        Start-Sleep 45
        $t = Get-ScheduledTask -TaskPath '\WindowsOptimize\' -TaskName 'AppGuard' -ErrorAction SilentlyContinue
        [pscustomobject]@{
            Task   = if ($t) { $t.State } else { 'không có' }
            Result = if ($t) { (Get-ScheduledTaskInfo $t).LastTaskResult } else { $null }
            Log    = Get-Content C:\ProgramData\WindowsOptimize\guard.log -Tail 5 -ErrorAction SilentlyContinue
        }
    }
    $g | Format-List
    Say "Báo cáo nằm ở $((Resolve-Path $out).Path)" Green
}
finally { Remove-PSSession $s }

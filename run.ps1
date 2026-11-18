<#
  Chạy WindowsOptimize bằng 1 dòng (không cần tải tay, không cần clone repo):

      irm https://github.com/CozyCode890/WindowsOptimize/releases/latest/download/run.ps1 | iex

  Mở PowerShell (không cần quyền admin), dán dòng trên, Enter. Script tải bản exe mới nhất
  từ GitHub Releases vào %TEMP% rồi mở nó (Windows sẽ hỏi quyền admin).
#>
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$url = 'https://github.com/CozyCode890/WindowsOptimize/releases/latest/download/WindowsOptimize.exe'
$dir = Join-Path $env:TEMP 'WindowsOptimize'
$exe = Join-Path $dir 'WindowsOptimize.exe'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

Write-Host 'Đang tải WindowsOptimize...' -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing
Unblock-File $exe -ErrorAction SilentlyContinue
Start-Process -FilePath $exe -Verb RunAs

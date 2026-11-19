@echo off
rem Ban dev KHONG doi quyen admin (chi de xem giao dien), co them che do chup anh:
rem   .build\noelevate\WindowsOptimize.exe --snap <thu muc>
cd /d "%~dp0.."
if exist src\WindowsOptimize\obj rmdir /s /q src\WindowsOptimize\obj
dotnet build src\WindowsOptimize\WindowsOptimize.csproj -c Release -p:NoElevate=true -o .build\noelevate -nologo -v q || exit /b 1
rmdir /s /q src\WindowsOptimize\obj
echo   Xong: %cd%\.build\noelevate\WindowsOptimize.exe

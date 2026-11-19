@echo off
rem Build WindowsOptimize.exe vao thu muc dist\ (can .NET SDK 8+; exe chay tren .NET Framework 4.8 co san trong Windows)
cd /d "%~dp0"
if exist src\WindowsOptimize\obj rmdir /s /q src\WindowsOptimize\obj
dotnet build src\WindowsOptimize\WindowsOptimize.csproj -c Release -o dist -nologo -v q || exit /b 1
del /q dist\*.pdb dist\*.config 2>nul
echo.
echo   Xong: %~dp0dist\WindowsOptimize.exe

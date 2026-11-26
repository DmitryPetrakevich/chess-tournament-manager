@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Windows.ps1"
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Read the messages above.
  pause
  exit /b 1
)
echo.
echo The compiled app is in dist\ChessDraw.exe
pause

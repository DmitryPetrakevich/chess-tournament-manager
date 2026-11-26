@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "LOG=%~dp0startup-diagnostics.txt"
echo ChessDraw startup diagnostics > "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo Working directory: %CD% >> "%LOG%"
echo. >> "%LOG%"
echo ==== dotnet info ==== >> "%LOG%"
where dotnet >> "%LOG%" 2>&1
dotnet --list-runtimes >> "%LOG%" 2>&1
dotnet --list-sdks >> "%LOG%" 2>&1
echo. >> "%LOG%"
echo ==== published files ==== >> "%LOG%"
dir /b "dist" >> "%LOG%" 2>&1
echo. >> "%LOG%"
if not exist "dist\ChessDraw.dll" (
  echo ERROR: dist\ChessDraw.dll is missing. Run Build-Windows.cmd first. >> "%LOG%"
) else (
  echo ==== executing via dotnet host ==== >> "%LOG%"
  dotnet "dist\ChessDraw.dll" >> "%LOG%" 2>&1
  echo Exit code: !ERRORLEVEL! >> "%LOG%"
)
echo. >> "%LOG%"
echo ==== application exception log ==== >> "%LOG%"
if exist "%LOCALAPPDATA%\ChessDraw\startup-errors.log" (
  type "%LOCALAPPDATA%\ChessDraw\startup-errors.log" >> "%LOG%"
) else (
  echo No managed startup exceptions logged. >> "%LOG%"
)
echo.
echo Diagnostics saved to:
echo %LOG%
echo Please send this file if ChessDraw still fails to start.
pause

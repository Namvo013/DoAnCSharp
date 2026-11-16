@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-LiMS.ps1" -Mode Web
if errorlevel 1 (
    echo.
    echo Frontend startup failed. Check SQL Server, Backend API, and port 5500.
    pause
    exit /b 1
)
endlocal

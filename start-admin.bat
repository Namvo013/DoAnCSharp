@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-LiMS.ps1" -Mode Admin
if errorlevel 1 (
    echo.
    echo Admin startup failed. Check SQL Server and Backend API.
    pause
    exit /b 1
)
endlocal

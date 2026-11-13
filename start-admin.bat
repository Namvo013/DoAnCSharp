@echo off
setlocal
cd /d "%~dp0"
dotnet run --project ".\LiMS.Admin\LiMS.Admin.csproj"

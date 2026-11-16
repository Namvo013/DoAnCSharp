@echo off
cd /d "%~dp0"
start "" "http://localhost:5500"
dotnet run --project "LiMS.Web\LiMS.Web.csproj" --launch-profile http

@echo off
:: Run as Administrator

set SERVICE_NAME=FileWatcherService
set EXE_PATH=%~dp0publish\FileWatcherService.exe

echo Building and publishing...
dotnet publish FileWatcherService.csproj -c Release -r win-x64 --self-contained true -o "%~dp0publish"

echo Installing Windows Service: %SERVICE_NAME%
sc create %SERVICE_NAME% binPath= "%EXE_PATH%" start= auto DisplayName= "File Watcher SharePoint Sync"
sc description %SERVICE_NAME% "Monitors a local folder and downloads missing files from SharePoint."
sc start %SERVICE_NAME%

echo Done. Service status:
sc query %SERVICE_NAME%
pause

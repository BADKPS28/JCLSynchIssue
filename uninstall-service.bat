@echo off
:: Run as Administrator

set SERVICE_NAME=FileWatcherService

echo Stopping service...
sc stop %SERVICE_NAME%
timeout /t 3

echo Removing service...
sc delete %SERVICE_NAME%

echo Done.
pause

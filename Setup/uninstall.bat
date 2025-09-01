@echo off
echo Uninstalling Backup App...

:: Remove autostart entry
reg delete "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" /v "BackupApp" /f

:: Remove program files
rmdir /S /Q "%ProgramFiles%\BackupApp"

echo Uninstallation complete!
pause

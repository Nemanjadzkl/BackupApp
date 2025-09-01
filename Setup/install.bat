@echo off
echo Installing Backup App...

:: Create program directory
mkdir "%ProgramFiles%\BackupApp"

:: Copy files
xcopy /Y /E "..\bin\Release\net8.0-windows\*.*" "%ProgramFiles%\BackupApp\"

:: Create autostart entry
reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" /v "BackupApp" /t REG_SZ /d "\"%ProgramFiles%\BackupApp\BackupApp.exe\"" /f

echo Installation complete!
pause

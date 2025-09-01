using System;
using System.IO;
using Microsoft.Win32;

namespace BackupApp.Setup
{
    public static class Install
    {
        private const string AppName = "BackupApp";
        private const string AppExeName = "BackupApp.exe";
        
        public static void InstallApp()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var appDir = Path.Combine(programFiles, AppName);
            
            // Kreiranje instalacionog direktorijuma
            Directory.CreateDirectory(appDir);
            
            // Kopiranje fajlova
            var sourceDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var file in Directory.GetFiles(sourceDir, "*.*"))
            {
                var destFile = Path.Combine(appDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Dodavanje u autostart
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.SetValue(AppName, Path.Combine(appDir, AppExeName));
        }

        public static void UninstallApp()
        {
            // Uklanjanje iz autostarta
            using (var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                key?.DeleteValue(AppName, false);
            }

            // Brisanje instalacionog direktorijuma
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var appDir = Path.Combine(programFiles, AppName);
            
            if (Directory.Exists(appDir))
            {
                Directory.Delete(appDir, true);
            }
        }
    }
}

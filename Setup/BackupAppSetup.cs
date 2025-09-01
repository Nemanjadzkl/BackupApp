using System;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;

namespace BackupApp.Setup
{
    public static class Installer
    {
        private const string AppName = "BackupApp";
        private const string PublisherName = "BackupApp";
        private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BackupApp";
        
        public static void Install()
        {
            var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);
            var startupPath = Path.Combine(installDir, "BackupApp.exe");
            
            // Kreiraj instalacioni direktorijum
            Directory.CreateDirectory(installDir);
            
            // Kopiraj sve fajlove
            foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory))
            {
                File.Copy(file, Path.Combine(installDir, Path.GetFileName(file)), true);
            }
            
            // Dodaj u autostart
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                key?.SetValue(AppName, $"\"{startupPath}\" --background");
            }
            
            // Dodaj u uninstall listu
            using (var key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("Publisher", PublisherName);
                key.SetValue("InstallLocation", installDir);
                key.SetValue("UninstallString", $"\"{startupPath}\" --uninstall");
            }
        }

        public static void Uninstall()
        {
            try
            {
                // Ukloni iz autostarta
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.DeleteValue(AppName, false);
                }
                
                // Ukloni iz uninstall liste
                Registry.LocalMachine.DeleteSubKey(UninstallKeyPath, false);
                
                // Obriši instalacioni direktorijum
                var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);
                if (Directory.Exists(installDir))
                {
                    Directory.Delete(installDir, true);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Greška pri deinstalaciji: {ex.Message}");
            }
        }
    }
}

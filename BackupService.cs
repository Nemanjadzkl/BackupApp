using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using BackupApp.Models;
using BackupApp.Services;
using System.Threading;
using System.Net.Mail;
using System.Text.Json;
using System.Timers;
using System.Text;
using System.Text.Json.Serialization;

namespace BackupApp
{
    public enum BackupType
    {
        Full,
        Incremental
    }

    public class BackupService : Services.IBackupService
    {
        private readonly Services.ScheduleService _scheduleService;

        public delegate void LogHandler(string message, bool isError = false);
        public delegate void ProgressHandler(ProgressInfo progress);
        public event LogHandler? OnLog;
        public event ProgressHandler? OnProgress;

        // Čuvamo informacije o poslednjem backup-u za inkrementalni backup
        private string lastBackupPath = string.Empty;
        private DateTime lastBackupTime = DateTime.MinValue;

        // P/Invoke za direktan pristup Windows API-ju za mountovanje/demountovanje
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DefineDosDevice(uint dwFlags, string lpDeviceName, string lpTargetPath);

        // Dodajemo nove Win32 API pozive
        [DllImport("mountmgr.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool PreventRemovableMediaRemoval(string driveLetter, bool prevent);

        private const uint DDD_REMOVE_DEFINITION = 0x00000002;
        private const uint DDD_RAW_TARGET_PATH = 0x00000001;
        private const uint DDD_NO_BROADCAST_SYSTEM = 0x00000008;

        private const string SCHEDULE_FILE = "backup_schedule.json";
        private const string PATHS_FILE = "backup_paths.json";
        private const string EMAIL_SETTINGS_FILE = "email_settings.json";

        private readonly BackupSettings _settings = new();
        private EmailService _emailService;
        private readonly List<string> _processedFiles = new();
        private readonly List<string> _errors = new();

        public EmailSettings EmailSettings { get; set; } = new();

        public List<string> SavedPaths { get; private set; } = new();

        public BackupService()
        {
            // Kreiramo potrebne direktorijume odmah na početku
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BackupApp");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
                LogMessage($"Kreiran konfiguracioni direktorijum: {appDataPath}", false);
            }

            // Prvo učitaj podešavanja, pa onda inicijalizuj servis
            LoadEmailSettings();
            _emailService = new EmailService(EmailSettings);
            _emailService.OnLog += (message, isError) => OnLog?.Invoke(message, isError);

            _scheduleService = new Services.ScheduleService(this);
        }

        private async Task SendBackupNotificationAsync(bool success, string? error = null)
        {
            if (string.IsNullOrEmpty(EmailSettings.ToEmail)) return;

            var subject = success ? "Backup uspešno završen" : "Greška pri backup-u";
            var body = success
                ? $"Backup je uspešno završen u {DateTime.Now:dd.MM.yyyy HH:mm}."
                : $"Došlo je do greške pri backup-u u {DateTime.Now:dd.MM.yyyy HH:mm}.\nGreška: {error}";

            try
            {
                using var client = new SmtpClient(EmailSettings.SmtpServer, EmailSettings.SmtpPort)
                {
                    EnableSsl = EmailSettings.EnableSsl,
                    Credentials = new System.Net.NetworkCredential(EmailSettings.Username, EmailSettings.Password)
                };
                using var message = new MailMessage(EmailSettings.FromEmail, EmailSettings.ToEmail, subject, body);
                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Greška pri slanju email notifikacije: {ex.Message}", true);
            }
        }

        private BackupSettings? LoadSettings()
        {
            try
            {
                string settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BackupApp",
                    "settings.json");

                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    return System.Text.Json.JsonSerializer.Deserialize<BackupSettings>(json);
                }
            }
            catch (Exception ex)
            {
                Log($"Greška pri učitavanju podešavanja: {ex.Message}", true);
            }
            return null;
        }

        private void LoadEmailSettings()
        {
            try
            {
                string settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BackupApp",
                    "email_settings.json");

                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<EmailSettings>(json);
                    if (settings != null)
                    {
                        EmailSettings = settings;
                        OnLog?.Invoke("Email podešavanja su učitana", false);
                    }
                }
                else
                {
                    EmailSettings = new EmailSettings(); // Fallback to empty settings
                }
            }
            catch (Exception ex)
            {
                Log($"Greška pri učitavanju email podešavanja: {ex.Message}", true);
                EmailSettings = new EmailSettings(); // Fallback on error
            }
        }

        private void CleanupOldBackups()
        {
            try
            {
                if (!Directory.Exists("D:\\"))
                    return;

                var backupFolders = Directory.GetDirectories("D:\\")
                    .Where(d => Path.GetFileName(d).StartsWith("Full_Backup_") ||
                               Path.GetFileName(d).StartsWith("Incr_Backup_"))
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(d => d.CreationTime)
                    .ToList();

                // Zadrži samo najnovijih N backup-ova
                var toDelete = backupFolders.Skip(_settings.MaxBackupCount).ToList();

                // Dodaj i backup-ove starije od definisanog broja dana
                var oldDate = DateTime.Now.AddDays(-_settings.MaxBackupAgeDays);
                toDelete.AddRange(backupFolders.Take(_settings.MaxBackupCount)
                    .Where(d => d.CreationTime < oldDate));

                foreach (var dir in toDelete.Distinct())
                {
                    try
                    {
                        Directory.Delete(dir.FullName, true);
                        Log($"Obrisan stari backup: {dir.Name}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Greška pri brisanju starog backup-a {dir.Name}: {ex.Message}", true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Greška pri čišćenju starih backup-ova: {ex.Message}", true);
            }
        }

        public bool MountDiskD()
        {
            try
            {
                if (Directory.Exists("D:\\"))
                {
                    Log("Disk D je već mountovan");
                    return true;
                }

                // Prvo proveri i online-uj disk
                string checkScript = @"
                    select disk 1
                    online disk noerr
                    select partition 1
                    exit";
                ExecuteDiskPartCommand(checkScript);
                System.Threading.Thread.Sleep(1000);

                // Zatim dodeli slovo D
                string mountScript = @"
                    select disk 1
                    select partition 1
                    assign letter=D noerr
                    exit";
                if (ExecuteDiskPartCommand(mountScript))
                {
                    System.Threading.Thread.Sleep(2000);

                    if (Directory.Exists("D:\\"))
                    {
                        Log("Disk D uspešno mountovan");
                        return true;
                    }
                }

                Log("Nije moguće mountovati disk D", true);
                return false;
            }
            catch (Exception ex)
            {
                Log($"Greška pri mountovanju diska D: {ex.Message}", true);
                return false;
            }
        }

        public bool UnmountDiskD()
        {
            try
            {
                if (!Directory.Exists("D:\\"))
                {
                    Log("Disk D nije mountovan");
                    return true;
                }

                // Force close all open handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.Threading.Thread.Sleep(1000);

                // First try to offline the disk
                string offlineScript = @"
                    select disk 1
                    select partition 1
                    remove letter=D noerr
                    select disk 1
                    offline disk
                    exit";
                if (ExecuteDiskPartCommand(offlineScript))
                {
                    System.Threading.Thread.Sleep(2000);
                    if (!Directory.Exists("D:\\"))
                    {
                        Log("Disk D uspešno demountovan");
                        return true;
                    }
                }

                // If that didn't work, try more aggressively
                string forceScript = @"
                    select disk 1
                    select partition 1
                    remove letter=D noerr
                    select disk 1
                    offline disk noerr
                    select disk 1
                    attribute disk clear readonly
                    offline disk
                    exit";
                ExecuteDiskPartCommand(forceScript);
                System.Threading.Thread.Sleep(2000);

                if (!Directory.Exists("D:\\"))
                {
                    Log("Disk D uspešno demountovan (force)");
                    return true;
                }

                Log("Nije moguće demountovati disk D", true);
                return false;
            }
            catch (Exception ex)
            {
                Log($"Greška pri demountovanju diska D: {ex.Message}", true);
                return false;
            }
        }

        private string ExecuteDiskPartCommandWithOutput(string script)
        {
            string tempScriptPath = Path.Combine(Path.GetTempPath(), $"diskpart_{Guid.NewGuid()}.txt");
            try
            {
                File.WriteAllText(tempScriptPath, script);
                using Process process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "diskpart.exe",
                    Arguments = $"/s \"{tempScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Verb = "runas"
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            finally
            {
                try { File.Delete(tempScriptPath); } catch { }
            }
        }

        private bool ExecuteDiskPartCommand(string script)
        {
            string tempScriptPath = Path.Combine(Path.GetTempPath(), $"diskpart_{Guid.NewGuid()}.txt");
            try
            {
                File.WriteAllText(tempScriptPath, script);
                using Process process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "diskpart.exe",
                    Arguments = $"/s \"{tempScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Verb = "runas"
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log($"Greška pri izvršavanju diskpart komande: {ex.Message}", true);
                return false;
            }
            finally
            {
                try { File.Delete(tempScriptPath); } catch { }
            }
        }

        public string CreateBackupFolder(BackupType backupType = BackupType.Full)
        {
            string typePrefix = backupType == BackupType.Full ? "Full" : "Incr";
            string backupFolderName = $"{typePrefix}_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            string backupPath = Path.Combine("D:\\", backupFolderName);

            Directory.CreateDirectory(backupPath);
            Log($"Kreiran {(backupType == BackupType.Full ? "pun" : "inkrementalni")} backup folder: {backupPath}");

            if (backupType == BackupType.Full)
            {
                lastBackupPath = backupPath;
                lastBackupTime = DateTime.Now;
            }

            return backupPath;
        }

        private class CopyProgress
        {
            public int ProcessedFiles { get; set; }
        }

        private List<FileInfo> GetFilesToBackup(string sourcePath, BackupType backupType)
        {
            var directory = new DirectoryInfo(sourcePath);
            var files = directory.GetFiles("*.*", SearchOption.AllDirectories);
            if (backupType == BackupType.Incremental)
            {
                var lastBackupTime = GetLastBackupTime();
                return files.Where(f => f.LastWriteTime > lastBackupTime).ToList();
            }
            return files.ToList();
        }

        private DateTime GetLastBackupTime()
        {
            if (lastBackupTime == DateTime.MinValue)
            {
                LoadLastBackupInfo();
            }
            return lastBackupTime;
        }

        public async Task PerformBackupAsync(List<string> sourcePaths, BackupType backupType)
        {
            var report = new BackupReport
            {
                StartTime = DateTime.Now,
                BackupType = backupType
            };

            try
            {
                // Prvo pokušavamo mount
                OnLog?.Invoke("Započinjem backup proces - montiram disk...", false);
                if (!MountDiskD())
                {
                    throw new Exception("Nije moguće montirati backup disk");
                }

                // ...existing backup logic...
                var allFiles = new List<FileInfo>();
                foreach (var path in sourcePaths)
                {
                    OnProgress?.Invoke(new ProgressInfo
                    {
                        CurrentOperation = "Pripremam backup...",
                        CurrentFile = path,
                        IsIndeterminate = true
                    });
                    allFiles.AddRange(GetFilesToBackup(path, backupType));
                }

                report.TotalFiles = allFiles.Count;
                report.TotalSize = allFiles.Sum(f => f.Length);

                if (report.TotalFiles == 0)
                {
                    OnLog?.Invoke("Nema fajlova za backup.", false);
                    return;
                }

                // Ostatak koda ostaje isti
                var processedSize = 0L;
                foreach (var file in allFiles)
                {
                    try
                    {
                        OnProgress?.Invoke(new ProgressInfo
                        {
                            Percentage = (int)((processedSize * 100.0) / report.TotalSize),
                            CurrentOperation = $"Backup u toku ({report.SuccessfulFiles + 1}/{report.TotalFiles})",
                            CurrentFile = file.Name,
                            DetailedStatus = $"Veličina: {FormatFileSize(file.Length)}"
                        });
                        await BackupFileAsync(file, backupType, report);
                        processedSize += file.Length;
                        report.SuccessfulFiles++;
                    }
                    catch (Exception ex)
                    {
                        report.FailedFiles++;
                        report.Errors.Add($"{file.Name}: {ex.Message}");
                        OnLog?.Invoke($"Greška pri backup-u fajla {file.Name}: {ex.Message}", true);
                    }
                }

                report.EndTime = DateTime.Now;
                await SendBackupReportEmailAsync(report);
                var successMessage = $"Backup uspešno završen!\n" +
                    $"Ukupno fajlova: {report.TotalFiles}\n" +
                    $"Uspešno kopirano: {report.SuccessfulFiles}\n" +
                    $"Ukupna veličina: {FormatFileSize(report.TotalSize)}\n" +
                    $"Trajanje: {report.Duration.ToString(@"hh\:mm\:ss")}";
                OnLog?.Invoke(successMessage, false);
                OnProgress?.Invoke(new ProgressInfo
                {
                    CurrentOperation = "Backup završen",
                    DetailedStatus = successMessage,
                    Percentage = 100,
                    IsComplete = true
                });
                OnLog?.Invoke("Backup završen, demontiram disk...", false);
                UnmountDiskD();
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Greška: {ex.Message}", true);
                // Pokušaj unmount i u slučaju greške
                try
                {
                    UnmountDiskD();
                }
                catch {} // Ignorišemo greške pri unmount-u u slučaju da je glavni proces već pukao
                throw;
            }
        }

        private async Task SendBackupReportEmailAsync(BackupReport report)
        {
            try
            {
                if (!EmailSettings.IsValid())
                {
                    OnLog?.Invoke("Email podešavanja nisu ispravna. Izveštaj neće biti poslat.", true);
                    return;
                }

                var body = GenerateHtmlReport(report);
                var subject = $"Backup izveštaj - {report.BackupType} - {report.StartTime:dd.MM.yyyy HH:mm}";
                await _emailService.SendEmailAsync(subject, body);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Greška pri slanju email izveštaja: {ex.Message}", true);
            }
        }

        private string GenerateHtmlReport(BackupReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'><style>");
            sb.AppendLine(@"
                body { font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; margin: 0; padding: 0; background: #f5f5f5; }
                .container { max-width: 600px; margin: 20px auto; background: white; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
                .header { background: linear-gradient(135deg, #2196F3 0%, #1976D2 100%); color: white; padding: 20px; border-radius: 8px 8px 0 0; }
                .content { padding: 20px; }
                .metric-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 15px; margin-bottom: 20px; }
                .metric-box { background: #f8f9fa; padding: 15px; border-radius: 6px; border-left: 4px solid #2196F3; }
                .metric-box.success { border-color: #4CAF50; }
                .metric-box.warning { border-color: #FFC107; }
                .metric-box.error { border-color: #f44336; }
                .metric-title { font-size: 12px; text-transform: uppercase; color: #666; margin-bottom: 5px; }
                .metric-value { font-size: 18px; font-weight: bold; color: #333; }
                .progress-bar { background: #e9ecef; height: 8px; border-radius: 4px; margin-top: 10px; }
                .progress-value { background: linear-gradient(90deg, #2196F3 0%, #64B5F6 100%); height: 100%; border-radius: 4px; }
                .footer { text-align: center; padding: 20px; color: #666; font-size: 12px; }
            ");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div class='container'>");
            sb.AppendLine("<div class='header'>");
            sb.AppendLine($"<h2 style='margin:0;'>{report.BackupType} Backup Report</h2>");
            sb.AppendLine($"<div style='opacity:0.8;font-size:14px;margin-top:5px;'>{report.StartTime:dd.MM.yyyy HH:mm:ss}</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='content'>");
            sb.AppendLine("<div class='metric-grid'>");
            // Status Box
            sb.AppendLine($@"<div class='metric-box {(report.FailedFiles == 0 ? "success" : "warning")}'>
                <div class='metric-title'>Status</div>
                <div class='metric-value'>{(report.FailedFiles == 0 ? "✅ Success" : "⚠️ Partial Success")}</div>
            </div>");
            // Duration Box
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Duration</div>
                <div class='metric-value'>⏱️ {report.Duration.ToString(@"hh\:mm\:ss")}</div>
            </div>");
            // Files Box
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Files Processed</div>
                <div class='metric-value'>📁 {report.SuccessfulFiles:N0} / {report.TotalFiles:N0}</div>
                <div class='progress-bar'>
                    <div class='progress-value' style='width: {(report.SuccessfulFiles * 100.0 / report.TotalFiles):F0}%'></div>
                </div>
            </div>");
            // Size Box
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Total Size</div>
                <div class='metric-value'>💾 {FormatFileSize(report.TotalSize)}</div>
            </div>");
            // Speed Box
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Average Speed</div>
                <div class='metric-value'>⚡ {report.AverageSpeedMBps:F2} MB/s</div>
            </div>");
            // Peak Speed Box
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Peak Speed</div>
                <div class='metric-value'>🚀 {report.PeakSpeedMBps:F2} MB/s</div>
            </div>");
            sb.AppendLine("</div>"); // end metric-grid

            if (report.FailedFiles > 0)
            {
                sb.AppendLine("<div class='metric-box error' style='margin-top: 20px;'>");
                sb.AppendLine($"<div class='metric-title'>Failed Files ({report.FailedFiles})</div>");
                sb.AppendLine("<div style='margin-top: 10px; font-size: 14px;'>");
                foreach (var error in report.Errors.Take(5))
                {
                    sb.AppendLine($"❌ {error}<br>");
                }
                if (report.Errors.Count > 5)
                {
                    sb.AppendLine($"<div style='color: #666;'>And {report.Errors.Count - 5} more...</div>");
                }
                sb.AppendLine("</div></div>");
            }

            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("Generated by Backup App");
            sb.AppendLine("</div>");

            sb.AppendLine("</div></body></html>");

            return sb.ToString();
        }

        private async Task BackupFileAsync(FileInfo sourceFile, BackupType backupType, BackupReport report)
        {
            var destinationPath = GetBackupDestinationPath(sourceFile, backupType);

            if (!destinationPath.StartsWith("D:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Nedozvoljena putanja za backup: {destinationPath}. Backup je dozvoljen samo na D: drajv.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            const int bufferSize = 4 * 1024 * 1024; // 4MB buffer
            var buffer = new byte[bufferSize];
            var speedTimer = new System.Diagnostics.Stopwatch();
            using var sourceStream = sourceFile.OpenRead();
            using var destStream = File.Create(destinationPath);

            report.CurrentFile = sourceFile.Name;
            int bytesRead;
            long totalBytesRead = 0;
            speedTimer.Start();

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                report.ProcessedBytes += bytesRead;

                // Računamo trenutnu brzinu na svaki sekund
                if (speedTimer.ElapsedMilliseconds >= 1000)
                {
                    report.CurrentSpeedMBps = totalBytesRead / (1024.0 * 1024.0) / speedTimer.Elapsed.TotalSeconds;
                    report.PeakSpeedMBps = Math.Max(report.PeakSpeedMBps, report.CurrentSpeedMBps);
                    OnProgress?.Invoke(new ProgressInfo
                    {
                        CurrentOperation = $"Kopiranje ({FormatFileSize(report.ProcessedBytes)}/{FormatFileSize(report.TotalSize)})",
                        CurrentFile = report.CurrentFile,
                        DetailedStatus = $"Brzina: {report.CurrentSpeedMBps:F2} MB/s",
                        Percentage = (int)((report.ProcessedBytes * 100.0) / report.TotalSize)
                    });
                    totalBytesRead = 0;
                    speedTimer.Restart();
                }
            }
        }

        private string GetBackupDestinationPath(FileInfo sourceFile, BackupType backupType)
        {
            var backupRoot = "D:\\Backup";
            var datePart = DateTime.Now.ToString("yyyy-MM-dd");
            var typePart = backupType.ToString();
            var relativePath = sourceFile.FullName.Substring(sourceFile.Directory.Root.Name.Length);

            return Path.Combine(backupRoot, typePart, datePart, relativePath);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private bool IsDiskMounted()
        {
            try
            {
                return Directory.Exists("D:\\");
            }
            catch
            {
                return false;
            }
        }

        private int CountFiles(List<string> paths)
        {
            int count = 0;
            foreach (string path in paths)
            {
                try
                {
                    count += Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Length;
                }
                catch (Exception ex)
                {
                    Log($"Greška pri brojanju fajlova u {path}: {ex.Message}", true);
                }
            }
            return count;
        }

        private async Task CopyDirectoryWithProgressAsync(string sourceDir, string destinationDir, CopyProgress progress, int totalFiles)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                try
                {
                    string fileName = Path.GetFileName(filePath);
                    string destFile = Path.Combine(destinationDir, fileName);
                    OnProgress?.Invoke(new ProgressInfo
                    {
                        CurrentOperation = "Kopiranje",
                        CurrentFile = fileName,
                        Percentage = (int)((float)progress.ProcessedFiles / totalFiles * 100)
                    });
                    await CopyFileWithProgressAsync(filePath, destFile);
                    progress.ProcessedFiles++;
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Greška pri kopiranju fajla {filePath}: {ex.Message}", true);
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                await CopyDirectoryWithProgressAsync(
                    subDir,
                    Path.Combine(destinationDir, Path.GetFileName(subDir)),
                    progress,
                    totalFiles
                );
            }
        }

        private async Task CopyFileWithProgressAsync(string source, string destination)
        {
            const int bufferSize = 1024 * 1024; // 1MB buffer
            using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read);
            using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write);

            var buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destStream.WriteAsync(buffer, 0, bytesRead);
            }
        }

        private async Task CopyIncrementalDirectoryWithProgressAsync(string sourceDir, string destinationDir, CopyProgress progress, int totalFiles)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    if (fileInfo.LastWriteTime > lastBackupTime)
                    {
                        string fileName = Path.GetFileName(filePath);
                        string destFile = Path.Combine(destinationDir, fileName);
                        await CopyFileWithProgressAsync(filePath, destFile);
                        progress.ProcessedFiles++;
                        int percentage = (int)((float)progress.ProcessedFiles / totalFiles * 100);
                        OnProgress?.Invoke(new ProgressInfo
                        {
                            CurrentOperation = "Kopiranje",
                            CurrentFile = $"Kopiranje: {fileName}",
                            Percentage = percentage
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log($"Greška pri kopiranju fajla {filePath}: {ex.Message}", true);
                }
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                try
                {
                    string subDirName = Path.GetFileName(subDir);
                    string destSubDir = Path.Combine(destinationDir, subDirName);
                    await CopyIncrementalDirectoryWithProgressAsync(subDir, destSubDir, progress, totalFiles);
                }
                catch (Exception ex)
                {
                    Log($"Greška pri kopiranju direktorijuma {subDir}: {ex.Message}", true);
                }
            }
        }

        // Metode za čuvanje i učitavanje informacija o poslednjem backup-u
        private void SaveLastBackupInfo()
        {
            string infoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackupApp");
            Directory.CreateDirectory(infoPath);

            string infoFile = Path.Combine(infoPath, "LastBackupInfo.txt");
            File.WriteAllText(infoFile, $"{lastBackupPath}|{lastBackupTime:yyyy-MM-dd HH:mm:ss}");
        }

        public void LoadLastBackupInfo()
        {
            string infoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackupApp");
            string infoFile = Path.Combine(infoPath, "LastBackupInfo.txt");
            if (File.Exists(infoFile))
            {
                try
                {
                    string[] info = File.ReadAllText(infoFile).Split('|');
                    if (info.Length == 2)
                    {
                        lastBackupPath = info[0];
                        if (DateTime.TryParse(info[1], out DateTime lastTime))
                        {
                            lastBackupTime = lastTime;
                            Log($"Učitane informacije o poslednjem backup-u: {lastBackupTime}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Greška pri učitavanju informacija o poslednjem backup-u: {ex.Message}", true);
                }
            }
        }

        private void Log(string message, bool isError = false)
        {
            OnLog?.Invoke(message, isError);
        }

        public void MountBackupDrive()
        {
            try
            {
                if (MountDiskD())
                {
                    Log("Disk D: je uspešno montiran");
                }
                else
                {
                    Log("Neuspešno montiranje diska D:", true);
                }
            }
            catch (Exception ex)
            {
                Log($"Greška pri montiranju diska: {ex.Message}", true);
                throw;
            }
        }

        public void UnmountBackupDrive()
        {
            try
            {
                if (UnmountDiskD())
                {
                    Log("Disk D: je uspešno demontiran");
                }
                else
                {
                    Log("Neuspešno demontiranje diska D:", true);
                }
            }
            catch (Exception ex)
            {
                Log($"Greška pri demontiranju diska: {ex.Message}", true);
                throw;
            }
        }

        public void SaveEmailSettings(EmailSettings settings)
        {
            EmailSettings = settings;
            _emailService = new EmailService(settings); // Kreiramo novu instancu sa novim podešavanjima
            // Ostatak implementacije ostaje isti...
        }

        public async Task SendTestEmailAsync()
        {
            using var client = new SmtpClient(EmailSettings.SmtpServer, EmailSettings.SmtpPort)
            {
                EnableSsl = EmailSettings.EnableSsl,
                Credentials = new System.Net.NetworkCredential(EmailSettings.Username, EmailSettings.Password)
            };
            using var message = new MailMessage(
                EmailSettings.FromEmail,
                EmailSettings.ToEmail,
                "Backup App - Test Email",
                "Ovo je test email iz Backup aplikacije. Ako ste primili ovaj email, vaše email podešavanje je ispravno."
            );
            await client.SendMailAsync(message);
        }

        private void SaveEmailSettings()
        {
            try
            {
                string settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BackupApp");

                Directory.CreateDirectory(settingsPath);
                string filePath = Path.Combine(settingsPath, "email_settings.json");
                string json = JsonSerializer.Serialize(EmailSettings);
                File.WriteAllText(filePath, json);
                OnLog?.Invoke("Email podešavanja su sačuvana", false);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Greška pri čuvanju email podešavanja: {ex.Message}", true);
            }
        }

        public BackupSchedule? CurrentSchedule { get; set; }

        public DateTime GetNextBackupTime()
        {
            if (CurrentSchedule == null)
                return DateTime.MinValue;
            var now = DateTime.Now;
            var today = DateTime.Today;
            var scheduledTime = today.Add(CurrentSchedule.Time);

            if (scheduledTime < now)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }
            LogMessage($"Sledeći backup zakazan za: {scheduledTime:dd.MM.yyyy HH:mm}", false);
            return scheduledTime;
        }

        public void SaveSchedule()
        {
            try
            {
                if (CurrentSchedule == null) return;

                string appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BackupApp");
                Directory.CreateDirectory(appFolder);
                string filePath = Path.Combine(appFolder, "backup_schedule.json");

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonTimeSpanConverter() }
                };

                var json = JsonSerializer.Serialize(CurrentSchedule, options);
                File.WriteAllText(filePath, json);

                LogMessage($"Raspored je sačuvan u: {filePath}", false);
                LogMessage($"Sledeći backup zakazan za: {GetNextBackupTime():dd.MM.yyyy HH:mm}", false);
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri čuvanju rasporeda: {ex.Message}", true);
                throw;
            }
        }

        public void LoadSchedule()
        {
            try
            {
                string filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BackupApp",
                    "backup_schedule.json");

                if (File.Exists(filePath))
                {
                    LogMessage($"Učitavam raspored iz: {filePath}", false);
                    string jsonContent = File.ReadAllText(filePath);
                    CurrentSchedule = JsonSerializer.Deserialize<BackupSchedule>(jsonContent);

                    if (CurrentSchedule != null)
                    {
                        LogMessage($"Učitan raspored za {CurrentSchedule.Time:HH:mm}", false);
                        _scheduleService.SetupScheduledTask(CurrentSchedule);
                    }
                }
                else
                {
                    LogMessage($"Fajl sa rasporedom nije pronađen na: {filePath}", false);
                    CurrentSchedule = new BackupSchedule();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri učitavanju rasporeda: {ex.Message}", true);
                CurrentSchedule = new BackupSchedule();
            }
        }

        private void SavePathsInternal()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BackupApp");

                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                var pathsFile = Path.Combine(appDataPath, PATHS_FILE);

                // Filtriramo i čuvamo samo validne putanje
                var validPaths = SavedPaths
                    .Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p))
                    .Select(Path.GetFullPath)
                    .Distinct()
                    .ToList();

                var json = JsonSerializer.Serialize(validPaths, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(pathsFile, json);

                // Ažuriramo listu u memoriji
                SavedPaths = validPaths;

                LogMessage($"Sačuvano {validPaths.Count} foldera za backup", false);
                LogMessage($"Putanje su sačuvane u: {pathsFile}", false);
                LogMessage($"Trenutne putanje: {string.Join(", ", validPaths)}", false);
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri čuvanju foldera: {ex.Message}", true);
            }
        }

        public void SavePaths()
        {
            SavePathsInternal();
        }

        public void AddPath(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    LogMessage($"Folder ne postoji: {path}", true);
                    return;
                }

                path = Path.GetFullPath(path);

                if (!SavedPaths.Contains(path))
                {
                    SavedPaths.Add(path);
                    LogMessage($"Dodat folder: {path}", false);
                    SavePathsInternal(); // Odmah sačuvaj promene
                    LogMessage($"Trenutno sačuvani folderi: {string.Join(", ", SavedPaths)}", false);
                }
                else
                {
                    LogMessage($"Folder je već dodat: {path}", false);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri dodavanju foldera: {ex.Message}", true);
            }
        }

        public void RemovePath(string path)
        {
            if (SavedPaths.Remove(path))
            {
                LogMessage($"Uklonjen folder: {path}", false);
                SavePathsInternal();
            }
        }

        private bool ValidateBackupFolders()
        {
            if (SavedPaths == null || SavedPaths.Count == 0)
            {
                LogMessage("Nije izabran nijedan folder za backup!", true);
                return false;
            }

            var invalidPaths = SavedPaths
                .Where(path => !Directory.Exists(path))
                .ToList();

            foreach (var path in invalidPaths)
            {
                LogMessage($"Folder nije pronađen: {path}", true);
                SavedPaths.Remove(path);
            }

            if (SavedPaths.Count == 0)
            {
                LogMessage("Svi izabrani folderi su nevalidni!", true);
                return false;
            }

            return true;
        }

        public void LoadPaths()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BackupApp");

                var pathsFile = Path.Combine(appDataPath, PATHS_FILE);

                LogMessage($"Pokušavam učitati putanje iz: {pathsFile}", false);

                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                    LogMessage($"Kreiran direktorijum: {appDataPath}", false);
                }

                if (File.Exists(pathsFile))
                {
                    string json = File.ReadAllText(pathsFile);
                    LogMessage($"Pročitan sadržaj: {json}", false);

                    var paths = JsonSerializer.Deserialize<List<string>>(json);
                    if (paths != null && paths.Any())
                    {
                        SavedPaths = paths
                            .Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p))
                            .Select(Path.GetFullPath)
                            .Distinct()
                            .ToList();

                        LogMessage($"Učitano {SavedPaths.Count} validnih foldera za backup", false);
                        foreach (var path in SavedPaths)
                        {
                            LogMessage($"Učitan folder: {path}", false);
                        }
                    }
                    else
                    {
                        SavedPaths = new List<string>();
                        LogMessage("Nema sačuvanih putanja u fajlu", false);
                    }
                }
                else
                {
                    SavedPaths = new List<string>();
                    LogMessage($"Fajl sa putanjama još ne postoji: {pathsFile}", false);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri učitavanju foldera: {ex.Message}", true);
                SavedPaths = new List<string>();
            }
        }

        public void Initialize()
        {
            try
            {
                LoadPaths();
                LoadSchedule();
                LogMessage("Inicijalizacija backup servisa...", false);
                LogMessage($"Učitano {SavedPaths.Count} foldera za backup", false);

                if (CurrentSchedule?.IsEnabled == true)
                {
                    var nextBackup = GetNextBackupTime();
                    LogMessage($"Backup je zakazan za {nextBackup:dd.MM.yyyy HH:mm}", false);
                    LogMessage($"Tip backup-a: {CurrentSchedule.BackupType}", false);

                    // Proveri da li je task registrovan
                    if (_scheduleService.IsTaskScheduled())
                        LogMessage("Windows Task je uspešno registrovan", false);
                    else
                        LogMessage("Windows Task nije registrovan!", true);
                }
                else
                {
                    LogMessage("Backup nije zakazan", false);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri inicijalizaciji: {ex.Message}", true);
            }
        }

        public async Task<bool> TestScheduleAsync()
        {
            try
            {
                OnLog?.Invoke("Testiram scheduled backup...", false);

                // Provera da li je scheduling omogućen
                if (CurrentSchedule == null || !CurrentSchedule.IsEnabled)
                {
                    OnLog?.Invoke("Scheduling nije omogućen", true);
                    return false;
                }

                // Test mount/unmount
                OnLog?.Invoke("Test 1/3: Provera mount/unmount...", false);
                if (!await TestMountUnmountAsync())
                {
                    OnLog?.Invoke("Test mount/unmount nije uspeo", true);
                    return false;
                }

                // Test task schedulera
                OnLog?.Invoke("Test 2/3: Provera Windows Task Scheduler-a...", false);
                if (!_scheduleService.IsTaskScheduled())
                {
                    OnLog?.Invoke("Task nije pravilno registrovan u Windows Task Scheduler-u", true);
                    return false;
                }

                // Test pristupa folderima
                OnLog?.Invoke("Test 3/3: Provera pristupa backup folderima...", false);
                if (!await TestBackupFoldersAsync())
                {
                    OnLog?.Invoke("Problem sa pristupom backup folderima", true);
                    return false;
                }

                OnLog?.Invoke("Sve provere su uspešne!", false);
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Greška pri testiranju: {ex.Message}", true);
                return false;
            }
        }

        private async Task<bool> TestMountUnmountAsync()
        {
            try
            {
                if (!MountDiskD())
                    return false;

                await Task.Delay(2000); // Sačekaj malo da se disk stabilizuje

                if (!UnmountDiskD())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestBackupFoldersAsync()
        {
            try
            {
                if (!MountDiskD())
                    return false;

                string testPath = Path.Combine("D:\\", "BackupTest");
                try
                {
                    Directory.CreateDirectory(testPath);
                    await File.WriteAllTextAsync(Path.Combine(testPath, "test.txt"), "test");
                    return true;
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(testPath))
                            Directory.Delete(testPath, true);
                    }
                    catch { }
                    UnmountDiskD();
                }
            }
            catch
            {
                return false;
            }
        }

        public void LogMessage(string message, bool isError = false)
        {
            OnLog?.Invoke(message, isError);
        }

        public string GetScheduleStatus()
        {
            if (CurrentSchedule == null)
                return "Backup nije konfigurisan";

            if (!CurrentSchedule.IsEnabled)
                return "Backup je onemogućen";

            var nextRun = GetNextBackupTime();
            return $"Sledeći backup: {nextRun:dd.MM.yyyy HH:mm}";
        }
    }

    // Dodajemo converter za TimeSpan
    public class JsonTimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return TimeSpan.Parse(value ?? "00:00:00");
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("hh\\:mm"));
        }
    }
}

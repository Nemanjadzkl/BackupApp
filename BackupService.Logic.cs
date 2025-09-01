using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BackupApp.Models;
using System.Diagnostics;
using System.Net.Mail;

namespace BackupApp
{
    public partial class BackupService
    {
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
            var backupStartTime = DateTime.Now;
            var report = new BackupReport
            {
                StartTime = backupStartTime,
                BackupType = backupType
            };

            try
            {
                OnLog?.Invoke("Započinjem backup proces - montiram disk...", false);
                if (!MountDiskD())
                {
                    throw new Exception("Nije moguće montirati backup disk");
                }

                string typePrefix = backupType == BackupType.Full ? "Full" : "Incr";
                string backupFolderName = $"{typePrefix}_Backup_{backupStartTime:yyyy-MM-dd_HH-mm-ss}";
                string backupDestinationRoot = Path.Combine("D:\\", backupFolderName);
                Directory.CreateDirectory(backupDestinationRoot);
                Log($"Kreiran backup folder: {backupDestinationRoot}");

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
                    OnLog?.Invoke("Nema novih ili izmenjenih fajlova za backup.", false);
                    UnmountDiskD();
                    return;
                }

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
                        await BackupFileAsync(file, backupDestinationRoot, report);
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

                lastBackupTime = backupStartTime;
                lastBackupPath = backupDestinationRoot;
                SaveLastBackupInfo();

                OnLog?.Invoke("Backup završen, demontiram disk...", false);
                UnmountDiskD();
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Greška: {ex.Message}", true);
                try
                {
                    UnmountDiskD();
                }
                catch { }
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
            sb.AppendLine($@"<div class='metric-box {(report.FailedFiles == 0 ? "success" : "warning")}'>
                <div class='metric-title'>Status</div>
                <div class='metric-value'>{(report.FailedFiles == 0 ? "✅ Success" : "⚠️ Partial Success")}</div>
            </div>");
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Duration</div>
                <div class='metric-value'>⏱️ {report.Duration.ToString(@"hh\:mm\:ss")}</div>
            </div>");
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Files Processed</div>
                <div class='metric-value'>📁 {report.SuccessfulFiles:N0} / {report.TotalFiles:N0}</div>
                <div class='progress-bar'>
                    <div class='progress-value' style='width: {(report.SuccessfulFiles * 100.0 / report.TotalFiles):F0}%'></div>
                </div>
            </div>");
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Total Size</div>
                <div class='metric-value'>💾 {FormatFileSize(report.TotalSize)}</div>
            </div>");
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Average Speed</div>
                <div class='metric-value'>⚡ {report.AverageSpeedMBps:F2} MB/s</div>
            </div>");
            sb.AppendLine($@"<div class='metric-box'>
                <div class='metric-title'>Peak Speed</div>
                <div class='metric-value'>🚀 {report.PeakSpeedMBps:F2} MB/s</div>
            </div>");
            sb.AppendLine("</div>");

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

        private async Task BackupFileAsync(FileInfo sourceFile, string backupDestinationRoot, BackupReport report)
        {
            var relativePath = sourceFile.FullName.Substring(Path.GetPathRoot(sourceFile.FullName).Length);
            var destinationPath = Path.Combine(backupDestinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            const int bufferSize = 4 * 1024 * 1024;
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
    }
}

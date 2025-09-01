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

    public partial class BackupService : Services.IBackupService
    {
        private readonly Services.ScheduleService _scheduleService;
        private readonly NetworkTimeService _networkTimeService;

        public delegate void LogHandler(string message, bool isError = false);
        public delegate void ProgressHandler(ProgressInfo progress);
        public event LogHandler? OnLog;
        public event ProgressHandler? OnProgress;

        private string lastBackupPath = string.Empty;
        private DateTime lastBackupTime = DateTime.MinValue;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DefineDosDevice(uint dwFlags, string lpDeviceName, string lpTargetPath);

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

        private readonly System.Timers.Timer _scheduleTimer;
        private DateTime _lastRunTime = DateTime.MinValue;
        public List<string> SavedPaths { get; private set; } = new();

        public BackupService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BackupApp");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
                LogMessage($"Kreiran konfiguracioni direktorijum: {appDataPath}", false);
            }

            _networkTimeService = new NetworkTimeService();
            EmailSettings = new EmailSettings
            {
                SmtpServer = "smtp.aol.com",
                SmtpPort = 587,
                Username = "dzekn@aol.com",
                Password = "ibewcjgpnjxketvi",
                FromEmail = "dzekn@aol.com",
                ToEmail = "dzekn@aol.com",
                EnableSsl = true
            };

            _emailService = new EmailService(EmailSettings);
            _emailService.OnLog += (message, isError) => OnLog?.Invoke(message, isError);

            _scheduleTimer = new System.Timers.Timer(30000);
            _scheduleTimer.Elapsed += CheckSchedule;
            _scheduleTimer.AutoReset = true;
            _scheduleTimer.Enabled = true;

            LoadEmailSettings();
            _scheduleService = new Services.ScheduleService(this);
        }

        public delegate Task ButtonClickHandler();
        public event ButtonClickHandler? OnMountButtonClick;
        public event ButtonClickHandler? OnBackupButtonClick;
        public event ButtonClickHandler? OnUnmountButtonClick;

        private void CheckSchedule(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (CurrentSchedule == null || !CurrentSchedule.IsEnabled)
                    return;

                LoadPaths();
                LogMessage($"Provera rasporeda - trenutno imamo {SavedPaths.Count} foldera", false);

                if (SavedPaths.Count == 0)
                {
                    LogMessage("Nema sačuvanih foldera za backup", true);
                    return;
                }

                var networkTime = _networkTimeService.GetNetworkTime();
                var scheduledTime = DateTime.Today.Add(CurrentSchedule.Time);

                LogMessage($"Provera - vreme: {networkTime:HH:mm:ss}, zakazano: {scheduledTime:HH:mm}", false);
                LogMessage($"Putanje: {string.Join(", ", SavedPaths)}", false);

                if (networkTime.Hour == scheduledTime.Hour &&
                    networkTime.Minute == scheduledTime.Minute &&
                    _lastRunTime.Date != networkTime.Date)
                {
                    _lastRunTime = networkTime;
                    LogMessage($"Pokretanje backup-a za {SavedPaths.Count} foldera!", false);
                    Task.Run(ExecuteScheduledBackupAsync);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Greška u CheckSchedule: {ex.Message}", true);
            }
        }

        private async Task ExecuteScheduledBackupAsync()
        {
            try
            {
                if (CurrentSchedule == null)
                {
                    LogMessage("Backup nije konfigurisan", true);
                    return;
                }

                LogMessage("Započinjem zakazani backup...", false);
                var report = new BackupReport
                {
                    StartTime = DateTime.Now,
                    BackupType = CurrentSchedule.BackupType
                };

                LogMessage("Korak 1/3: Montiram disk...", false);
                if (OnMountButtonClick != null)
                {
                    await OnMountButtonClick.Invoke();
                    await Task.Delay(2000);
                }

                LogMessage("Korak 2/3: Pokrećem backup...", false);
                if (OnBackupButtonClick != null)
                {
                    await OnBackupButtonClick.Invoke();
                    await Task.Delay(1000);
                }

                LogMessage("Korak 3/3: Demontiram disk...", false);
                if (OnUnmountButtonClick != null)
                {
                    await OnUnmountButtonClick.Invoke();
                }

                report.EndTime = DateTime.Now;
                await SendBackupReportEmailAsync(report);
                LogMessage("Zakazani backup je uspešno završen", false);
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri izvršavanju backup-a: {ex.Message}", true);
                await SendBackupNotificationAsync(false, ex.Message);

                try
                {
                    if (OnUnmountButtonClick != null)
                        await OnUnmountButtonClick.Invoke();
                }
                catch { }
            }
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
            }
            catch (Exception ex)
            {
                Log($"Greška pri učitavanju email podešavanja: {ex.Message}", true);
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

                var toDelete = backupFolders.Skip(_settings.MaxBackupCount).ToList();

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

                string checkScript = @"
                    select disk 1
                    online disk noerr
                    select partition 1
                    exit";
                ExecuteDiskPartCommand(checkScript);
                System.Threading.Thread.Sleep(1000);

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

                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.Threading.Thread.Sleep(1000);

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
            _emailService = new EmailService(settings);
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
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
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

                _lastRunTime = DateTime.MinValue;
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
                    SavePathsInternal();
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

                    if (_scheduleService.IsTaskScheduled())
                        LogMessage("Windows Task je uspešno registrovan", false);
                    else
                        LogMessage("Windows Task nije registrovan!", true);
                }
                else
                {
                    LogMessage("Backup nije zakazan", false);
                }
                _scheduleTimer.Start();
                LogMessage("Servis je pokrenut i prati raspored", false);
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri inicijalizaciji: {ex.Message}", true);
            }
        }

        public void InitializeEmail()
        {
            EmailSettings = new EmailSettings
            {
                SmtpServer = "smtp.aol.com",
                SmtpPort = 587,
                Username = "dzekn@aol.com",
                Password = "ibewcjgpnjxketvi",
                FromEmail = "dzekn@aol.com",
                ToEmail = "dzekn@aol.com",
                EnableSsl = true
            };

            _emailService = new EmailService(EmailSettings);
            _emailService.OnLog += (message, isError) => OnLog?.Invoke(message, isError);

            SaveEmailSettings();
        }

        public async Task<bool> TestScheduleAsync()
        {
            try
            {
                OnLog?.Invoke("Testiram scheduled backup...", false);

                if (CurrentSchedule == null || !CurrentSchedule.IsEnabled)
                {
                    OnLog?.Invoke("Scheduling nije omogućen", true);
                    return false;
                }

                OnLog?.Invoke("Test 1/3: Provera mount/unmount...", false);
                if (!await TestMountUnmountAsync())
                {
                    OnLog?.Invoke("Test mount/unmount nije uspeo", true);
                    return false;
                }

                OnLog?.Invoke("Test 2/3: Provera Windows Task Scheduler-a...", false);
                if (!_scheduleService.IsTaskScheduled())
                {
                    OnLog?.Invoke("Task nije pravilno registrovan u Windows Task Scheduler-u", true);
                    return false;
                }

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

                await Task.Delay(2000);

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

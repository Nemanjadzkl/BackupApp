namespace BackupApp.Services
{
    public interface IBackupService
    {
        event BackupApp.BackupService.LogHandler? OnLog;
        BackupApp.Models.BackupSchedule? CurrentSchedule { get; set; }
        System.Collections.Generic.List<string> SavedPaths { get; }
        void Initialize();
        void LogMessage(string message, bool isError = false);
    }
}

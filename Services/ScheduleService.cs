using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32.TaskScheduler;
using BackupApp.Models;

namespace BackupApp.Services
{
    public class ScheduleService
    {
        private const string TASK_NAME = "BackupAppScheduledTask";
        private readonly IBackupService _backupService;

        public ScheduleService(IBackupService backupService)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        }

        public void SetupScheduledTask(BackupSchedule schedule)
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            try
            {
                using var ts = new TaskService();
                RemoveScheduledTask();

                var td = ts.NewTask();
                td.RegistrationInfo.Description = "Backup App Scheduled Task";
                td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                td.Principal.RunLevel = TaskRunLevel.Highest;

                // Kreiranje trigera sa fiksnim vremenom
                var nextRun = schedule.GetNextRunTime();
                var trigger = new DailyTrigger
                {
                    StartBoundary = nextRun,
                    DaysInterval = 1
                };
                
                td.Triggers.Add(trigger);

                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                    throw new InvalidOperationException("Ne mogu pronaći izvršni fajl");

                td.Actions.Add(new ExecAction(exePath, "--scheduled-backup"));

                ts.RootFolder.RegisterTaskDefinition(
                    TASK_NAME,
                    td,
                    TaskCreation.CreateOrUpdate,
                    "NT AUTHORITY\\SYSTEM",
                    null,
                    TaskLogonType.ServiceAccount);

                _backupService.LogMessage($"Windows Task uspešno zakazan za {schedule.Time:hh\\:mm}", false);
            }
            catch (Exception ex)
            {
                _backupService.LogMessage($"Greška pri kreiranju Windows Task-a: {ex.Message}", true);
                throw;
            }
        }

        public bool IsTaskScheduled()
        {
            try
            {
                using var ts = new TaskService();
                var task = ts.FindTask(TASK_NAME);
                return task != null && task.IsActive;
            }
            catch (Exception ex)
            {
                _backupService.LogMessage($"Greška pri proveri Task Scheduler-a: {ex.Message}", true);
                return false;
            }
        }

        public void RemoveScheduledTask()
        {
            try
            {
                using var ts = new TaskService();
                var task = ts.FindTask(TASK_NAME);
                if (task != null)
                {
                    ts.RootFolder.DeleteTask(TASK_NAME, false);
                }
            }
            catch (Exception ex)
            {
                _backupService.LogMessage($"Greška pri brisanju postojećeg taska: {ex.Message}", true);
            }
        }
    }
}

using System;

namespace BackupApp.Models
{
    public class BackupSettings
    {
        public int MaxBackupCount { get; set; } = 5;
        public int MaxBackupAgeDays { get; set; } = 30;
    }
}

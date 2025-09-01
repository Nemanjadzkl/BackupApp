using System;
using System.Collections.Generic;

namespace BackupApp.Models
{
    public class BackupReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public BackupType BackupType { get; set; }
        public long TotalSize { get; set; }
        public int TotalFiles { get; set; }
        public int SuccessfulFiles { get; set; }
        public int FailedFiles { get; set; }
        public List<string> ProcessedPaths { get; } = new();
        public List<string> Errors { get; } = new();
        public TimeSpan Duration => EndTime - StartTime;
        public double AverageSpeedMBps => TotalSize / (1024.0 * 1024.0) / Duration.TotalSeconds;
        public double CurrentSpeedMBps { get; set; }
        public double PeakSpeedMBps { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public int ProcessedBytes { get; set; }
    }
}

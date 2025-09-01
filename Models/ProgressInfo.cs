namespace BackupApp.Models
{
    public class ProgressInfo
    {
        public int Percentage { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public string DetailedStatus { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
        public bool IsIndeterminate { get; set; }
        public bool IsError { get; set; }
        public bool IsComplete { get; set; }
    }
}

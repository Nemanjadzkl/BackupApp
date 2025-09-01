using System;
using System.Text.Json.Serialization;

namespace BackupApp.Models
{
    public class BackupSchedule
    {
        private TimeSpan _time;
        
        public DayOfWeek Day { get; set; }
        
        public TimeSpan Time 
        { 
            get => _time;
            set => _time = new TimeSpan(value.Hours, value.Minutes, 0);
        }
        
        public BackupType BackupType { get; set; }
        public bool IsEnabled { get; set; }

        public BackupSchedule()
        {
            Day = DayOfWeek.Monday;
            Time = new TimeSpan(22, 0, 0);
            BackupType = BackupType.Full;
            IsEnabled = true;
        }

        public DateTime GetNextRunTime()
        {
            var now = DateTime.Now;
            var today = DateTime.Today.Add(Time);
            
            if (today < now)
            {
                today = today.AddDays(1);
            }
            
            return today;
        }
    }
}

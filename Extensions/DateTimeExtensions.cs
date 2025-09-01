using System;

namespace BackupApp.Extensions
{
    public static class DateTimeExtensions
    {
        public static bool HasSameDateTime(this DateTime dt1, DateTime dt2)
        {
            return dt1.Date == dt2.Date && 
                   dt1.Hour == dt2.Hour && 
                   dt1.Minute == dt2.Minute;
        }
    }
}

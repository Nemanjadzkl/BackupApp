using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using BackupApp.Models;

namespace BackupApp.Services
{
    public class BackupService
    {
        public async Task PerformBackupAsync(List<string> sourcePaths, BackupType backupType)
        {
            // TODO: Implement backup logic
            await Task.Delay(100); // Placeholder for actual backup operation
        }

        public void MountBackupDrive()
        {
            // TODO: Implement mount logic
            Console.WriteLine("Mounting drive...");
        }

        public void UnmountBackupDrive()
        {
            // TODO: Implement unmount logic
            Console.WriteLine("Unmounting drive...");
        }
    }
}
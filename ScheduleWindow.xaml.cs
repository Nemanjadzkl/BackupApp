using System;
using System.Windows;
using System.Windows.Controls;
using BackupApp.Models;

namespace BackupApp
{
    public partial class ScheduleWindow : Window
    {
        public BackupSchedule Schedule { get; private set; }

        public ScheduleWindow(BackupSchedule? currentSchedule)
        {
            InitializeComponent();
            Schedule = currentSchedule ?? new BackupSchedule();
            
            LoadDays();
            LoadHours();
            LoadMinutes();
            LoadBackupTypes();
            LoadCurrentSchedule();
        }

        private void LoadDays()
        {
            string[] days = { "Ponedeljak", "Utorak", "Sreda", "Četvrtak", "Petak", "Subota", "Nedelja" };
            cmbDay.ItemsSource = days;
        }

        private void LoadHours()
        {
            for (int i = 0; i < 24; i++)
                cmbHour.Items.Add(i.ToString("00"));
        }

        private void LoadMinutes()
        {
            for (int i = 0; i < 60; i++)
                cmbMinute.Items.Add(i.ToString("00"));
        }

        private void LoadBackupTypes()
        {
            cmbBackupType.ItemsSource = Enum.GetValues(typeof(BackupType));
        }

        private void LoadCurrentSchedule()
        {
            chkEnabled.IsChecked = Schedule.IsEnabled;
            cmbDay.SelectedIndex = ((int)Schedule.Day + 6) % 7; // Convert to Monday-based index
            cmbHour.SelectedIndex = Schedule.Time.Hours;
            cmbMinute.SelectedIndex = Schedule.Time.Minutes;
            cmbBackupType.SelectedItem = Schedule.BackupType;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Schedule.IsEnabled = chkEnabled.IsChecked ?? false;
            Schedule.Day = (DayOfWeek)((cmbDay.SelectedIndex + 1) % 7); // Convert back to Sunday-based DayOfWeek
            Schedule.Time = new TimeSpan(cmbHour.SelectedIndex, cmbMinute.SelectedIndex, 0);
            Schedule.BackupType = (BackupType)cmbBackupType.SelectedItem;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

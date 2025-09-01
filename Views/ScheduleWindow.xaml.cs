using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BackupApp.Models;
using BackupApp.Services;
using MessageBox = System.Windows.MessageBox;

namespace BackupApp.Views
{
    public partial class ScheduleWindow : Window
    {
        private readonly BackupService _backupService;
        private bool _isSaving;

        public BackupSchedule Schedule { get; private set; }

        public ScheduleWindow(BackupService backupService, BackupSchedule? currentSchedule)
        {
            InitializeComponent();
            _backupService = backupService;
            Schedule = currentSchedule ?? new BackupSchedule();
            
            LoadDays();
            LoadHours();
            LoadMinutes();
            LoadBackupTypes();
            LoadCurrentSchedule();
        }

        private void LoadDays()
        {
            try
            {
                string[] days = { "Ponedeljak", "Utorak", "Sreda", "Četvrtak", "Petak", "Subota", "Nedelja" };
                cmbDay.ItemsSource = days;
                cmbDay.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri učitavanju dana: {ex.Message}",
                    "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadHours()
        {
            for (int i = 0; i < 24; i++)
                cmbHour.Items.Add(i.ToString("00"));
            cmbHour.SelectedIndex = DateTime.Now.Hour;
        }

        private void LoadMinutes()
        {
            for (int i = 0; i < 60; i++)
                cmbMinute.Items.Add(i.ToString("00"));
            cmbMinute.SelectedIndex = DateTime.Now.Minute;
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

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving) return;
            _isSaving = true;

            try
            {
                SetControlsEnabled(false);
                progressBar.Visibility = Visibility.Visible;

                // Parsiranje vremena iz ComboBox-ova
                if (!int.TryParse(cmbHour.SelectedItem?.ToString(), out int hour) ||
                    !int.TryParse(cmbMinute.SelectedItem?.ToString(), out int minute))
                {
                    ShowError("Neispravan format vremena");
                    return;
                }

                if (!ValidateInputs())
                    return;

                Schedule.IsEnabled = chkEnabled.IsChecked ?? false;
                Schedule.Day = (DayOfWeek)((cmbDay.SelectedIndex + 1) % 7); // Convert back to Sunday-based DayOfWeek
                Schedule.Time = new TimeSpan(cmbHour.SelectedIndex, cmbMinute.SelectedIndex, 0);
                Schedule.BackupType = (BackupType)cmbBackupType.SelectedItem;

                _backupService.CurrentSchedule = Schedule;
                await Task.Run(() => _backupService.SaveSchedule());

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Greška: {ex.Message}", true);
                MessageBox.Show(
                    $"Greška pri čuvanju rasporeda:\n\n{ex.Message}",
                    "Greška",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (IsLoaded)
                {
                    _isSaving = false;
                    SetControlsEnabled(true);
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private bool ValidateInputs()
        {
            if (!chkEnabled.IsChecked ?? true)
                return true; // Ako nije enabled, sve je ok

            if (cmbDay.SelectedIndex < 0)
            {
                ShowError("Molimo izaberite dan.");
                return false;
            }

            if (cmbHour.SelectedIndex < 0 || cmbMinute.SelectedIndex < 0)
            {
                ShowError("Molimo izaberite vreme.");
                return false;
            }

            if (cmbBackupType.SelectedIndex < 0)
            {
                ShowError("Molimo izaberite tip backup-a.");
                return false;
            }

            return true;
        }

        private void ShowError(string message)
        {
            UpdateStatus(message, true);
            MessageBox.Show(message, "Validacija", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            txtStatus.Text = message;
            txtStatus.Foreground = isError ? System.Windows.Media.Brushes.Red 
                                         : System.Windows.Media.Brushes.Gray;
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnSave.IsEnabled = enabled;
            btnTest.IsEnabled = enabled;
            btnCancel.IsEnabled = enabled;
            chkEnabled.IsEnabled = enabled;
            cmbDay.IsEnabled = enabled;
            cmbHour.IsEnabled = enabled;
            cmbMinute.IsEnabled = enabled;
            cmbBackupType.IsEnabled = enabled;
            progressBar.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnTest.IsEnabled = false;
                btnSave.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                // Create temporary schedule for testing
                var testSchedule = new BackupSchedule
                {
                    IsEnabled = chkEnabled.IsChecked ?? false,
                    Day = (DayOfWeek)((cmbDay.SelectedIndex + 1) % 7),
                    Time = new TimeSpan(cmbHour.SelectedIndex, cmbMinute.SelectedIndex, 0),
                    BackupType = (BackupType)cmbBackupType.SelectedItem
                };

                await Task.Delay(100); // Give UI time to update

                if (await _backupService.TestScheduleAsync())
                {
                    MessageBox.Show("Test uspešan! Sve komponente rade ispravno.", 
                        "Test", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri testiranju: {ex.Message}", 
                    "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (IsLoaded)
                {
                    btnTest.IsEnabled = true;
                    btnSave.IsEnabled = true;
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}

using System;
using System.Windows;
using BackupApp.Models;
using BackupApp.Services;
using MessageBox = System.Windows.MessageBox;

namespace BackupApp.Views
{
    public partial class EmailSettingsWindow : Window
    {
        private readonly BackupService _backupService;
        public EmailSettings Settings { get; private set; }

        public EmailSettingsWindow(BackupService backupService, EmailSettings settings)
        {
            InitializeComponent();
            _backupService = backupService;
            Settings = settings;
            LoadSettings();
        }

        private void LoadSettings()
        {
            txtToEmail.Text = Settings.ToEmail;
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnTest.IsEnabled = false;
                btnTest.Content = "Slanje...";

                Settings.ToEmail = txtToEmail.Text;

                var emailService = new EmailService(Settings);
                var success = await emailService.SendEmailAsync(
                    "Test Email - Backup aplikacija",
                    "Ovo je test email. Ako ste primili ovaj email, vaša email podešavanja su ispravna.",
                    true);

                if (success)
                {
                    MessageBox.Show("Test email je uspešno poslat!", "Uspeh", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška: {ex.Message}", "Greška", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnTest.IsEnabled = true;
                btnTest.Content = "TEST";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Settings.ToEmail = txtToEmail.Text;
            _backupService.SaveEmailSettings(Settings); // Dodajemo Settings parametar
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

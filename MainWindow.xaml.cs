using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Controls = System.Windows.Controls;
using System.Threading.Tasks;
using BackupApp.Models;
using Microsoft.Win32;
using BackupApp.Views;
using System.ComponentModel;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using System.IO;
using WPFApp = System.Windows.Application;

namespace BackupApp
{
    public partial class MainWindow : Window
    {
        private readonly BackupService _backupService;
        private Forms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            
            _backupService = new BackupService();
            InitializeBackupService();
            
            foreach (var path in _backupService.SavedPaths)
            {
                lstFolders.Items.Add(path);
            }
            
            UpdateNextBackupTime();
            InitializeSystemTray();
        }

        private void InitializeBackupService()
        {
            _backupService.OnLog += LogMessage;
            _backupService.OnProgress += UpdateProgress;

            _backupService.OnMountButtonClick += async () => 
            {
                await MountAsync();
            };
            
            _backupService.OnBackupButtonClick += async () => 
            {
                await StartBackupAsync();
            };
            
            _backupService.OnUnmountButtonClick += async () => 
            {
                await UnmountAsync();
            };

            _backupService.Initialize();
        }

        private async Task MountAsync()
        {
            try
            {
                await Task.Run(() => _backupService.MountBackupDrive());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri montiranju diska: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartBackupAsync()
        {
            try
            {
                var paths = _backupService.SavedPaths;
                if (paths.Any())
                {
                    var backupType = radIncrementalBackup.IsChecked == true ? BackupType.Incremental : BackupType.Full;
                    await _backupService.PerformBackupAsync(paths, backupType);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri backup-u: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UnmountAsync()
        {
            try
            {
                await Task.Run(() => _backupService.UnmountBackupDrive());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri demontiranju diska: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMount_Click(object sender, RoutedEventArgs e)
        {
            _backupService.MountBackupDrive();
        }

        private void BtnStartBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_backupService.SavedPaths.Any())
            {
                var backupType = radIncrementalBackup.IsChecked == true ? BackupType.Incremental : BackupType.Full;
                Task.Run(() => _backupService.PerformBackupAsync(_backupService.SavedPaths, backupType));
            }
        }

        private void BtnUnmount_Click(object sender, RoutedEventArgs e)
        {
            _backupService.UnmountBackupDrive();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void UpdateProgress(ProgressInfo progress)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = progress.Percentage;
                txtCurrentOperation.Text = progress.CurrentOperation;
                txtCurrentFile.Text = progress.CurrentFile;
                
                if (progress.IsComplete)
                {
                    progressBar.Value = 0;
                    txtCurrentOperation.Text = string.Empty;
                    txtCurrentFile.Text = string.Empty;
                }
            });
        }

        private async Task StartBackup(BackupType backupType)
        {
            if (!_backupService.SavedPaths.Any())
            {
                MessageBox.Show("Molimo dodajte folder za backup.", "Upozorenje");
                return;
            }

            try
            {
                SetControlsEnabled(false);
                await _backupService.PerformBackupAsync(_backupService.SavedPaths, backupType);
                MessageBox.Show("Backup uspešno završen.", "Uspeh");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška: {ex.Message}", "Greška");
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnStartBackup.IsEnabled = enabled;
            btnAddFolder.IsEnabled = enabled;
        }

        private void BtnSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var scheduleWindow = new Views.ScheduleWindow(_backupService, _backupService.CurrentSchedule);
                if (scheduleWindow.ShowDialog() == true)
                {
                    UpdateNextBackupTime();
                    LogMessage("Backup raspored je ažuriran");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri podešavanju rasporeda: {ex.Message}", true);
                MessageBox.Show(
                    $"Greška pri podešavanju rasporeda:\n\n{ex.Message}",
                    "Greška",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateNextBackupTime()
        {
            var nextTime = _backupService.GetNextBackupTime();
            txtNextBackup.Text = nextTime > DateTime.MinValue 
                ? $"Sledeći backup: {nextTime:dd.MM.yyyy HH:mm}"
                : "Backup nije zakazan";
        }

        private void BtnMountDisk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _backupService.MountBackupDrive();
                LogMessage("Disk D: je uspešno montiran");
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri montiranju diska: {ex.Message}", true);
                MessageBox.Show($"Greška pri montiranju diska: {ex.Message}", "Greška", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUnmountDisk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _backupService.UnmountBackupDrive();
                LogMessage("Disk D: je uspešno demontiran");
            }
            catch (Exception ex)
            {
                LogMessage($"Greška pri demontiranju diska: {ex.Message}", true);
                MessageBox.Show($"Greška pri demontiranju diska: {ex.Message}", "Greška", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEmailSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new EmailSettingsWindow(_backupService, _backupService.EmailSettings);
            if (settingsWindow.ShowDialog() == true)
            {
                _backupService.SaveEmailSettings(settingsWindow.Settings);
                LogMessage("Email podešavanja su ažurirana");
            }
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                RootFolder = Environment.SpecialFolder.Desktop,
                Description = "Izaberite folder za backup",
                UseDescriptionForTitle = true
            };

            var networkPath = new Controls.TextBox 
            { 
                Width = 300,
                Margin = new Thickness(10)
            };
            
            var inputDialog = new Window
            {
                Title = "Dodaj folder",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var panel = new Controls.StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new Controls.TextBlock { Text = "Unesite mrežnu putanju ili kliknite Browse" });
            panel.Children.Add(networkPath);
            
            var buttonPanel = new Controls.StackPanel 
            { 
                Orientation = Controls.Orientation.Horizontal, 
                Margin = new Thickness(10) 
            };
            
            var browseButton = new Controls.Button { Content = "Browse", Margin = new Thickness(0,0,10,0) };
            var okButton = new Controls.Button { Content = "OK", Margin = new Thickness(0,0,10,0) };
            var cancelButton = new Controls.Button { Content = "Cancel" };
            
            browseButton.Click += (s, args) => 
            {
                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    networkPath.Text = dialog.SelectedPath;
                }
            };
            
            okButton.Click += (s, args) => 
            {
                if (!string.IsNullOrWhiteSpace(networkPath.Text))
                {
                    var path = networkPath.Text;
                    if (Directory.Exists(path))
                    {
                        _backupService.AddPath(path);
                        RefreshFoldersList();
                        inputDialog.DialogResult = true;
                    }
                    else
                    {
                        MessageBox.Show("Uneta putanja nije dostupna", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                inputDialog.Close();
            };
            
            cancelButton.Click += (s, args) => inputDialog.Close();
            
            buttonPanel.Children.Add(browseButton);
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);
            
            inputDialog.Content = panel;
            inputDialog.ShowDialog();
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedIndex = lstFolders.SelectedIndex;
            if (selectedIndex >= 0)
            {
                var path = _backupService.SavedPaths[selectedIndex];
                _backupService.RemovePath(path);
                RefreshFoldersList();
            }
        }

        private void RefreshFoldersList()
        {
            lstFolders.Items.Clear();
            foreach (var path in _backupService.SavedPaths)
            {
                lstFolders.Items.Add(path);
            }
        }

        private void LogMessage(string message, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {(isError ? "ERROR: " : "")}{message}\n");
                txtLog.ScrollToEnd();
            });
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName),
                Text = "Backup App",
                Visible = true
            };

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Otvori", null, (s, e) => ShowMainWindow());
            contextMenu.Items.Add("Izađi", null, (s, e) => CloseApplication());
            
            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void CloseApplication()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            WPFApp.Current.Shutdown();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
            base.OnStateChanged(e);
        }
    }
}
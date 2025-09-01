using System;
using System.Windows;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using WpfApp = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace BackupApp
{
    public partial class App : WpfApp
    {
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();

            if (e.Args.Length > 0)
            {
                HandleCommandLineArgs(e.Args);
            }
            else
            {
                mainWindow.Show();
            }

            // Ako je aplikacija pokrenuta sa command line argumentom --background
            if (e.Args.Contains("--background"))
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.Hide();
            }
        }

        private async void HandleCommandLineArgs(string[] args)
        {
            try
            {
                if (args.Contains("--scheduled-backup"))
                {
                    var backupService = new BackupService();
                    backupService.OnLog += (msg, isError) => 
                        File.AppendAllText("backup_log.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
                    
                    backupService.LoadPaths();
                    await backupService.PerformBackupAsync(backupService.SavedPaths, BackupType.Incremental);
                }
                else
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("backup_error.txt", 
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error: {ex.Message}\n{ex.StackTrace}\n");
            }
            finally
            {
                if (args.Contains("--scheduled-backup"))
                {
                    Shutdown();
                }
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"Došlo je do neočekivane greške:\n\n{e.Exception.Message}\n\nDetalji su sačuvani u error.log",
                "Greška",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            var errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error:\n{e.Exception}\n\n";
            File.AppendAllText("error.log", errorLog);

            e.Handled = true;
        }
    }
}

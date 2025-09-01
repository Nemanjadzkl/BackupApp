using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace BackupApp
{
    public partial class Form1 : Form
    {
        private readonly BackupService _backupService;
        private readonly List<string> _sourcePaths = new();

        public Form1()
        {
            InitializeComponent();
            _backupService = new BackupService();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadSourcePaths();
        }

        private void btnAddFolder_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _sourcePaths.Add(dialog.SelectedPath);
                lstSourceFolders.Items.Add(dialog.SelectedPath);
            }
        }

        private void btnRemoveFolder_Click(object sender, EventArgs e)
        {
            if (lstSourceFolders.SelectedIndex >= 0)
            {
                _sourcePaths.RemoveAt(lstSourceFolders.SelectedIndex);
                lstSourceFolders.Items.RemoveAt(lstSourceFolders.SelectedIndex);
            }
        }

        private void btnViewLog_Click(object sender, EventArgs e)
        {
            // TODO: Implement log viewing
        }

        private async void btnIncrementalBackup_Click(object sender, EventArgs e)
        {
            await PerformBackupAsync(BackupType.Incremental);
        }

        private void btnMountDisk_Click(object sender, EventArgs e)
        {
            // TODO: Implement disk mounting
        }

        private void btnUnmountDisk_Click(object sender, EventArgs e)
        {
            // TODO: Implement disk unmounting
        }

        private void btnScheduleBackup_Click(object sender, EventArgs e)
        {
            // TODO: Implement backup scheduling
        }

        private async void btnStartBackup_Click(object sender, EventArgs e)
        {
            if (_sourcePaths.Count == 0)
            {
                MessageBox.Show("Dodajte folder za backup.");
                return;
            }

            try
            {
                await _backupService.PerformBackupAsync(_sourcePaths, BackupType.Full);
                MessageBox.Show("Backup završen.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public async Task ProcessCommandLineArgsAsync(string[] args)
        {
            if (args.Length > 0)
            {
                await _backupService.PerformBackupAsync(_sourcePaths, BackupType.Incremental);
            }
        }

        private void LoadSourcePaths()
        {
            lstSourceFolders.Items.Clear();
            _sourcePaths.Clear();

            if (File.Exists("backup_paths.txt"))
            {
                var paths = File.ReadAllLines("backup_paths.txt");
                foreach (var path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        _sourcePaths.Add(path);
                        lstSourceFolders.Items.Add(path);
                    }
                }
            }
        }

        private async Task PerformBackupAsync(BackupType backupType)
        {
            if (_sourcePaths.Count == 0)
            {
                MessageBox.Show("Molimo dodajte folder za backup.", "Upozorenje", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await _backupService.PerformBackupAsync(_sourcePaths, backupType);
                MessageBox.Show($"{(backupType == BackupType.Full ? "Pun" : "Inkrementalni")} backup je uspešno završen.", 
                    "Uspeh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška: {ex.Message}", "Greška", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

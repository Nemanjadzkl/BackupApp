using System;
using System.Windows.Forms;
using BackupApp.Setup;

namespace BackupApp.Setup;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        
        if (args.Length > 0 && args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
        {
            if (MessageBox.Show(
                "Da li ste sigurni da želite da deinstalirate BackupApp?",
                "Potvrda deinstalacije",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Installer.Uninstall();
            }
        }
        else
        {
            Installer.Install();
            MessageBox.Show(
                "BackupApp je uspešno instaliran.",
                "Instalacija završena");
        }
    }
}

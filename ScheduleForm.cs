using System;
using System.Windows.Forms;

namespace BackupApp
{
    public partial class ScheduleForm : Form
    {
        public DayOfWeek SelectedDay { get; private set; }
        public TimeSpan SelectedTime { get; private set; }

        public ScheduleForm()
        {
            InitializeComponent();
            
            // Popuni dane u nedelji
            cmbDay.Items.Add("Ponedeljak");
            cmbDay.Items.Add("Utorak");
            cmbDay.Items.Add("Sreda");
            cmbDay.Items.Add("Četvrtak");
            cmbDay.Items.Add("Petak");
            cmbDay.Items.Add("Subota");
            cmbDay.Items.Add("Nedelja");
            
            // Postavi podrazumevane vrednosti
            cmbDay.SelectedIndex = 0; // Ponedeljak
            dtpTime.Value = DateTime.Today.AddHours(22); // 22:00
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Konvertuj izabrani dan u DayOfWeek
            switch (cmbDay.SelectedIndex)
            {
                case 0: SelectedDay = DayOfWeek.Monday; break;
                case 1: SelectedDay = DayOfWeek.Tuesday; break;
                case 2: SelectedDay = DayOfWeek.Wednesday; break;
                case 3: SelectedDay = DayOfWeek.Thursday; break;
                case 4: SelectedDay = DayOfWeek.Friday; break;
                case 5: SelectedDay = DayOfWeek.Saturday; break;
                case 6: SelectedDay = DayOfWeek.Sunday; break;
                default: SelectedDay = DayOfWeek.Monday; break;
            }
            
            // Uzmi izabrano vreme
            SelectedTime = dtpTime.Value.TimeOfDay;
            
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
 
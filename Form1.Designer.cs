namespace BackupApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lstSourceFolders = new System.Windows.Forms.ListBox();
            this.btnAddFolder = new System.Windows.Forms.Button();
            this.btnRemoveFolder = new System.Windows.Forms.Button();
            this.btnStartBackup = new System.Windows.Forms.Button();
            this.btnViewLog = new System.Windows.Forms.Button();
            this.lstLog = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnIncrementalBackup = new System.Windows.Forms.Button();
            this.btnMountDisk = new System.Windows.Forms.Button();
            this.btnUnmountDisk = new System.Windows.Forms.Button();
            this.btnScheduleBackup = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lstSourceFolders
            // 
            this.lstSourceFolders.FormattingEnabled = true;
            this.lstSourceFolders.ItemHeight = 15;
            this.lstSourceFolders.Location = new System.Drawing.Point(12, 32);
            this.lstSourceFolders.Name = "lstSourceFolders";
            this.lstSourceFolders.Size = new System.Drawing.Size(560, 154);
            this.lstSourceFolders.TabIndex = 0;
            // 
            // btnAddFolder
            // 
            this.btnAddFolder.Location = new System.Drawing.Point(12, 192);
            this.btnAddFolder.Name = "btnAddFolder";
            this.btnAddFolder.Size = new System.Drawing.Size(120, 30);
            this.btnAddFolder.TabIndex = 1;
            this.btnAddFolder.Text = "Dodaj folder";
            this.btnAddFolder.UseVisualStyleBackColor = true;
            this.btnAddFolder.Click += new System.EventHandler(this.btnAddFolder_Click);
            // 
            // btnRemoveFolder
            // 
            this.btnRemoveFolder.Location = new System.Drawing.Point(138, 192);
            this.btnRemoveFolder.Name = "btnRemoveFolder";
            this.btnRemoveFolder.Size = new System.Drawing.Size(120, 30);
            this.btnRemoveFolder.TabIndex = 2;
            this.btnRemoveFolder.Text = "Ukloni folder";
            this.btnRemoveFolder.UseVisualStyleBackColor = true;
            this.btnRemoveFolder.Click += new System.EventHandler(this.btnRemoveFolder_Click);
            // 
            // btnStartBackup
            // 
            this.btnStartBackup.Location = new System.Drawing.Point(452, 192);
            this.btnStartBackup.Name = "btnStartBackup";
            this.btnStartBackup.Size = new System.Drawing.Size(120, 30);
            this.btnStartBackup.TabIndex = 3;
            this.btnStartBackup.Text = "Pun backup";
            this.btnStartBackup.UseVisualStyleBackColor = true;
            this.btnStartBackup.Click += new System.EventHandler(this.btnStartBackup_Click);
            // 
            // btnViewLog
            // 
            this.btnViewLog.Location = new System.Drawing.Point(452, 415);
            this.btnViewLog.Name = "btnViewLog";
            this.btnViewLog.Size = new System.Drawing.Size(120, 30);
            this.btnViewLog.TabIndex = 4;
            this.btnViewLog.Text = "Prikaži log fajl";
            this.btnViewLog.UseVisualStyleBackColor = true;
            this.btnViewLog.Click += new System.EventHandler(this.btnViewLog_Click);
            // 
            // lstLog
            // 
            this.lstLog.FormattingEnabled = true;
            this.lstLog.ItemHeight = 15;
            this.lstLog.Location = new System.Drawing.Point(12, 255);
            this.lstLog.Name = "lstLog";
            this.lstLog.Size = new System.Drawing.Size(560, 154);
            this.lstLog.TabIndex = 5;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(108, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "Folderi za backup:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 237);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(30, 15);
            this.label2.TabIndex = 7;
            this.label2.Text = "Log:";
            // 
            // btnIncrementalBackup
            // 
            this.btnIncrementalBackup.Location = new System.Drawing.Point(326, 192);
            this.btnIncrementalBackup.Name = "btnIncrementalBackup";
            this.btnIncrementalBackup.Size = new System.Drawing.Size(120, 30);
            this.btnIncrementalBackup.TabIndex = 8;
            this.btnIncrementalBackup.Text = "Inkrementalni";
            this.btnIncrementalBackup.UseVisualStyleBackColor = true;
            this.btnIncrementalBackup.Click += new System.EventHandler(this.btnIncrementalBackup_Click);
            // 
            // btnMountDisk
            // 
            this.btnMountDisk.Location = new System.Drawing.Point(12, 415);
            this.btnMountDisk.Name = "btnMountDisk";
            this.btnMountDisk.Size = new System.Drawing.Size(120, 30);
            this.btnMountDisk.TabIndex = 9;
            this.btnMountDisk.Text = "Mountuj disk D";
            this.btnMountDisk.UseVisualStyleBackColor = true;
            this.btnMountDisk.Click += new System.EventHandler(this.btnMountDisk_Click);
            // 
            // btnUnmountDisk
            // 
            this.btnUnmountDisk.Location = new System.Drawing.Point(138, 415);
            this.btnUnmountDisk.Name = "btnUnmountDisk";
            this.btnUnmountDisk.Size = new System.Drawing.Size(120, 30);
            this.btnUnmountDisk.TabIndex = 10;
            this.btnUnmountDisk.Text = "Demountuj disk D";
            this.btnUnmountDisk.UseVisualStyleBackColor = true;
            this.btnUnmountDisk.Click += new System.EventHandler(this.btnUnmountDisk_Click);
            // 
            // btnScheduleBackup
            // 
            this.btnScheduleBackup.Location = new System.Drawing.Point(264, 415);
            this.btnScheduleBackup.Name = "btnScheduleBackup";
            this.btnScheduleBackup.Size = new System.Drawing.Size(182, 30);
            this.btnScheduleBackup.TabIndex = 11;
            this.btnScheduleBackup.Text = "Zakaži nedeljni backup";
            this.btnScheduleBackup.UseVisualStyleBackColor = true;
            this.btnScheduleBackup.Click += new System.EventHandler(this.btnScheduleBackup_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 461);
            this.Controls.Add(this.btnScheduleBackup);
            this.Controls.Add(this.btnUnmountDisk);
            this.Controls.Add(this.btnMountDisk);
            this.Controls.Add(this.btnIncrementalBackup);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lstLog);
            this.Controls.Add(this.btnViewLog);
            this.Controls.Add(this.btnStartBackup);
            this.Controls.Add(this.btnRemoveFolder);
            this.Controls.Add(this.btnAddFolder);
            this.Controls.Add(this.lstSourceFolders);
            this.Name = "Form1";
            this.Text = "Backup Aplikacija";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ListBox lstSourceFolders;
        private System.Windows.Forms.Button btnAddFolder;
        private System.Windows.Forms.Button btnRemoveFolder;
        private System.Windows.Forms.Button btnStartBackup;
        private System.Windows.Forms.Button btnViewLog;
        private System.Windows.Forms.ListBox lstLog;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnIncrementalBackup;
        private System.Windows.Forms.Button btnMountDisk;
        private System.Windows.Forms.Button btnUnmountDisk;
        private System.Windows.Forms.Button btnScheduleBackup;
    }
}

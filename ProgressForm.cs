using System;
using System.Windows.Forms;

namespace IGAE_GUI
{
    /// <summary>
    /// Simple progress form for displaying long-running operations
    /// </summary>
    public partial class ProgressForm : Form
    {
        private Label? lblStatus;
        private ProgressBar? prgProgress;
        private Button? btnCancel;

        public bool IsCancelling { get; private set; }

        public ProgressForm(string title)
        {
            InitializeComponent();
            Text = title;
        }

        private void InitializeComponent()
        {
            lblStatus = new Label();
            prgProgress = new ProgressBar();
            btnCancel = new Button();

            // lblStatus
            lblStatus.AutoSize = true;
            lblStatus.Location = new System.Drawing.Point(12, 9);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(138, 13);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "Operation in progress...";

            // prgProgress
            prgProgress.Location = new System.Drawing.Point(12, 25);
            prgProgress.Name = "prgProgress";
            prgProgress.Size = new System.Drawing.Size(360, 23);
            prgProgress.TabIndex = 1;
            prgProgress.Minimum = 0;
            prgProgress.Maximum = 100;

            // btnCancel
            btnCancel.Location = new System.Drawing.Point(297, 54);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(75, 23);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;

            // ProgressForm
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(384, 89);
            Controls.Add(btnCancel);
            Controls.Add(prgProgress);
            Controls.Add(lblStatus);
            Name = "ProgressForm";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            FormClosing += ProgressForm_FormClosing;
        }

        public void UpdateProgress(int value, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(value, status)));
                return;
            }

            prgProgress!.Value = Math.Min(value, 100);
            lblStatus!.Text = status;
            Application.DoEvents();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            IsCancelling = true;
            btnCancel!.Enabled = false;
        }

        private void ProgressForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            IsCancelling = true;
        }
    }
}

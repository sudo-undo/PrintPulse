using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrintPulse
{
    public class ReprintDialog : Form
    {
        public ErrorRecoveryChoice SelectedChoice { get; private set; }
        public int ResumePageNumber { get; private set; }

        private RadioButton _rbFromPage;
        private RadioButton _rbEntire;
        private RadioButton _rbSkip;
        private RadioButton _rbAbort;
        private NumericUpDown _numPage;

        public ReprintDialog(PrintJobItem item, string errorMessage, int failedPage)
        {
            SelectedChoice = ErrorRecoveryChoice.ReprintFromPage;
            ResumePageNumber = Math.Max(1, failedPage);

            InitComponents(item, errorMessage, failedPage);
        }

        private void InitComponents(PrintJobItem item, string errorMessage, int failedPage)
        {
            this.Text = "Print Alert - Action Required";
            this.Size = new Size(480, 340);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Warning Icon Header
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(254, 242, 242)
            };

            Label lblTitle = new Label
            {
                Text = "Printer Alert Detected",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(153, 27, 27),
                Location = new Point(20, 12),
                AutoSize = true
            };

            Label lblSubtitle = new Label
            {
                Text = string.Format("Issue on: {0} (Page {1})", item.FileName, failedPage),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(185, 28, 28),
                Location = new Point(20, 38),
                AutoSize = true
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);

            // Reason label
            Label lblReason = new Label
            {
                Text = "Reason: " + (string.IsNullOrEmpty(errorMessage) ? "Hardware / Spooler Error" : errorMessage),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(20, 85),
                Size = new Size(440, 35)
            };

            // Group box options
            GroupBox group = new GroupBox
            {
                Text = " Choose Action: ",
                Location = new Point(20, 125),
                Size = new Size(425, 125),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            _rbFromPage = new RadioButton
            {
                Text = "Resume / Reprint starting from Page:",
                Location = new Point(15, 25),
                Size = new Size(240, 24),
                Checked = true
            };

            _numPage = new NumericUpDown
            {
                Location = new Point(260, 25),
                Size = new Size(60, 24),
                Minimum = 1,
                Maximum = Math.Max(1, item.TotalPages),
                Value = Math.Min(item.TotalPages, Math.Max(1, failedPage))
            };

            _rbEntire = new RadioButton
            {
                Text = "Reprint Entire Document from Page 1",
                Location = new Point(15, 52),
                Size = new Size(300, 24)
            };

            _rbSkip = new RadioButton
            {
                Text = "Skip this document and proceed with remaining files",
                Location = new Point(15, 76),
                Size = new Size(360, 24)
            };

            _rbAbort = new RadioButton
            {
                Text = "Stop / Abort entire batch print session",
                Location = new Point(15, 98),
                Size = new Size(300, 24)
            };

            group.Controls.Add(_rbFromPage);
            group.Controls.Add(_numPage);
            group.Controls.Add(_rbEntire);
            group.Controls.Add(_rbSkip);
            group.Controls.Add(_rbAbort);

            // Proceed Button
            Button btnProceed = new Button
            {
                Text = "Proceed",
                DialogResult = DialogResult.OK,
                Location = new Point(335, 260),
                Size = new Size(110, 32),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnProceed.FlatAppearance.BorderSize = 0;
            btnProceed.Click += (s, e) =>
            {
                if (_rbFromPage.Checked)
                {
                    SelectedChoice = ErrorRecoveryChoice.ReprintFromPage;
                    ResumePageNumber = (int)_numPage.Value;
                }
                else if (_rbEntire.Checked)
                {
                    SelectedChoice = ErrorRecoveryChoice.ReprintEntireFile;
                    ResumePageNumber = 1;
                }
                else if (_rbSkip.Checked)
                {
                    SelectedChoice = ErrorRecoveryChoice.SkipFile;
                }
                else if (_rbAbort.Checked)
                {
                    SelectedChoice = ErrorRecoveryChoice.AbortBatch;
                }
                this.Close();
            };

            this.Controls.Add(headerPanel);
            this.Controls.Add(lblReason);
            this.Controls.Add(group);
            this.Controls.Add(btnProceed);
        }
    }
}

using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace PrintPulse
{
    public class ReportForm : Form
    {
        private BatchReport _report;

        public ReportForm(BatchReport report)
        {
            _report = report;
            InitComponents();
        }

        private void InitComponents()
        {
            this.Text = "PrintPulse - Session Summary Report";
            this.Size = new Size(720, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Header Banner
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = (_report.FailedOrSkippedFiles == 0) ? Color.FromArgb(240, 253, 244) : Color.FromArgb(254, 252, 232)
            };

            Label lblTitle = new Label
            {
                Text = (_report.FailedOrSkippedFiles == 0) ? "Batch Print Complete - All Successful" : "Batch Print Complete - With Issues / Skipped",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = (_report.FailedOrSkippedFiles == 0) ? Color.FromArgb(22, 101, 52) : Color.FromArgb(133, 77, 14),
                Location = new Point(20, 15),
                AutoSize = true
            };

            Label lblMeta = new Label
            {
                Text = string.Format("Printer: {0}   |   Duration: {1:mm\\:ss}   |   Date: {2:yyyy-MM-dd HH:mm}",
                    _report.PrinterName, _report.Duration, _report.SessionDate),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(22, 45),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblMeta);

            // Metrics Cards Strip
            Panel pnlCards = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.White
            };

            AddMetricCard(pnlCards, "Files Done", _report.SuccessfulFiles + " / " + _report.TotalFilesQueued, 10);
            AddMetricCard(pnlCards, "Pages Printed", _report.TotalPagesPrinted.ToString(), 150);
            AddMetricCard(pnlCards, "Sheets of Paper", _report.TotalSheetsUsed.ToString(), 290);
            AddMetricCard(pnlCards, "Color / B&W", _report.ColorPages + " / " + _report.BwPages, 430);
            AddMetricCard(pnlCards, "Calculated Bill", "$" + _report.CalculatedBill.ToString("0.00"), 570);

            // Audit Table
            ListView lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.White
            };
            lv.Columns.Add("#", 40);
            lv.Columns.Add("File Name", 240);
            lv.Columns.Add("Pages", 65);
            lv.Columns.Add("Copies", 55);
            lv.Columns.Add("Mode", 65);
            lv.Columns.Add("Status", 90);
            lv.Columns.Add("Notes", 140);

            int i = 1;
            foreach (var item in _report.ItemsAudit)
            {
                ListViewItem lvi = new ListViewItem((i++).ToString());
                lvi.SubItems.Add(item.FileName);
                lvi.SubItems.Add(item.ActualPagesPrinted.ToString());
                lvi.SubItems.Add(item.Copies.ToString());
                lvi.SubItems.Add(item.ColorMode.ToString());
                lvi.SubItems.Add(item.Status.ToString());
                lvi.SubItems.Add(!string.IsNullOrEmpty(item.ErrorDetails) ? item.ErrorDetails : (item.StatusMessage ?? "-"));

                if (item.Status == PrintItemStatus.Completed)
                    lvi.ForeColor = Color.FromArgb(22, 101, 52);
                else if (item.Status == PrintItemStatus.Error)
                    lvi.ForeColor = Color.FromArgb(185, 28, 28);
                else
                    lvi.ForeColor = Color.FromArgb(100, 116, 139);

                lv.Items.Add(lvi);
            }

            // Bottom Buttons Panel
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = Color.FromArgb(241, 245, 249)
            };

            Button btnCsv = new Button
            {
                Text = "Export to CSV",
                Location = new Point(15, 12),
                Size = new Size(110, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCsv.Click += (s, e) =>
            {
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = "PrintPulse_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"
                };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _report.ExportCsv(sfd.FileName);
                    MessageBox.Show("Report exported successfully to CSV!", "Exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            Button btnTxt = new Button
            {
                Text = "Save Summary (.txt)",
                Location = new Point(135, 12),
                Size = new Size(140, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnTxt.Click += (s, e) =>
            {
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt",
                    FileName = "PrintPulse_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"
                };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(sfd.FileName, _report.GenerateTextSummary());
                    MessageBox.Show("Summary saved successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            Button btnReceipt = new Button
            {
                Text = "Print Receipt",
                Location = new Point(285, 12),
                Size = new Size(110, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnReceipt.Click += (s, e) =>
            {
                PrintDocument pd = new PrintDocument();
                pd.DocumentName = "PrintPulse Receipt";
                pd.PrinterSettings.PrinterName = _report.PrinterName;
                pd.PrintPage += (sender, pe) =>
                {
                    using (Font f = new Font("Courier New", 9))
                    using (Brush b = new SolidBrush(Color.Black))
                    {
                        pe.Graphics.DrawString(_report.GenerateTextSummary(), f, b, 20, 20);
                    }
                };
                try
                {
                    pd.Print();
                    MessageBox.Show("Receipt sent to printer: " + _report.PrinterName, "Receipt Printed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not print receipt: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            Button btnClose = new Button
            {
                Text = "Close",
                Location = new Point(590, 12),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.Add(btnCsv);
            pnlBottom.Controls.Add(btnTxt);
            pnlBottom.Controls.Add(btnReceipt);
            pnlBottom.Controls.Add(btnClose);

            // Assemble form
            this.Controls.Add(lv);
            this.Controls.Add(pnlCards);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottom);
        }

        private void AddMetricCard(Panel parent, string title, string val, int x)
        {
            Panel card = new Panel
            {
                Location = new Point(x, 10),
                Size = new Size(130, 50),
                BackColor = Color.FromArgb(248, 250, 252)
            };

            Label lblT = new Label
            {
                Text = title.ToUpperInvariant(),
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(8, 6),
                AutoSize = true
            };

            Label lblV = new Label
            {
                Text = val,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(8, 22),
                AutoSize = true
            };

            card.Controls.Add(lblT);
            card.Controls.Add(lblV);
            parent.Controls.Add(card);
        }
    }
}

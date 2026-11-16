using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace PrintPulse
{
    public class FeedbackForm : Form
    {
        private TextBox _txtEmail;
        private TextBox _txtSubject;
        private TextBox _txtMessage;
        private ComboBox _cbCategory;
        private CheckBox _chkTelemetry;
        private Button _btnSubmit;
        private Label _lblStatus;

        public FeedbackForm()
        {
            InitComponents();
        }

        private void InitComponents()
        {
            this.Text = "PrintPulse - Feedback & Feature Suggestions";
            this.Size = new Size(540, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Header Banner
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(238, 242, 255)
            };

            Label lblTitle = new Label
            {
                Text = "Send Feedback & Feature Suggestions",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(67, 56, 202),
                Location = new Point(20, 12),
                AutoSize = true
            };

            Label lblSubtitle = new Label
            {
                Text = "Your ideas go directly to the development team without needing an email app.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(99, 102, 241),
                Location = new Point(22, 38),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // Form Body
            Label lblCat = new Label { Text = "Category:", Location = new Point(25, 85), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85) };
            _cbCategory = new ComboBox
            {
                Location = new Point(25, 108),
                Size = new Size(470, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cbCategory.Items.AddRange(new object[] {
                "Feature Suggestion / Improvement",
                "Bug Report / Printer Issue",
                "New File Format Request",
                "UI / Usability Feedback",
                "General Question"
            });
            _cbCategory.SelectedIndex = 0;

            Label lblEmail = new Label { Text = "Your Email (optional, if you'd like a response):", Location = new Point(25, 142), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85) };
            _txtEmail = new TextBox
            {
                Location = new Point(25, 164),
                Size = new Size(470, 25)
            };

            Label lblSub = new Label { Text = "Subject:", Location = new Point(25, 198), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85) };
            _txtSubject = new TextBox
            {
                Location = new Point(25, 220),
                Size = new Size(470, 25),
                Text = "Feature Idea: "
            };

            Label lblMsg = new Label { Text = "Your Message / Details:", Location = new Point(25, 254), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85) };
            _txtMessage = new TextBox
            {
                Location = new Point(25, 276),
                Size = new Size(470, 100),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            _chkTelemetry = new CheckBox
            {
                Text = "Include anonymous diagnostic details (OS version & printer status) to help fix bugs",
                Location = new Point(25, 384),
                Size = new Size(470, 22),
                Checked = TelemetryService.IsTelemetryEnabled,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            _chkTelemetry.CheckedChanged += (s, e) => { TelemetryService.IsTelemetryEnabled = _chkTelemetry.Checked; };

            _lblStatus = new Label
            {
                Text = "",
                Location = new Point(25, 410),
                Size = new Size(470, 20),
                ForeColor = Color.FromArgb(16, 185, 129),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            // Buttons
            _btnSubmit = new Button
            {
                Text = "🚀 Submit Feedback",
                Location = new Point(25, 435),
                Size = new Size(160, 36),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnSubmit.FlatAppearance.BorderSize = 0;
            _btnSubmit.Click += (s, e) => SubmitDirect();

            Button btnCopy = new Button
            {
                Text = "📋 Copy to Clipboard",
                Location = new Point(195, 435),
                Size = new Size(160, 36),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCopy.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnCopy.Click += (s, e) => CopyDetails();

            Button btnClose = new Button
            {
                Text = "Close",
                Location = new Point(395, 435),
                Size = new Size(100, 36),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(pnlHeader);
            this.Controls.Add(lblCat);
            this.Controls.Add(_cbCategory);
            this.Controls.Add(lblEmail);
            this.Controls.Add(_txtEmail);
            this.Controls.Add(lblSub);
            this.Controls.Add(_txtSubject);
            this.Controls.Add(lblMsg);
            this.Controls.Add(_txtMessage);
            this.Controls.Add(_chkTelemetry);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(_btnSubmit);
            this.Controls.Add(btnCopy);
            this.Controls.Add(btnClose);
        }

        private void SubmitDirect()
        {
            if (string.IsNullOrEmpty(_txtMessage.Text.Trim()))
            {
                MessageBox.Show("Please enter your message before submitting.", "Message Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _btnSubmit.Enabled = false;
            _btnSubmit.Text = "Sending...";
            _lblStatus.Text = "Submitting directly to server...";
            _lblStatus.ForeColor = Color.FromArgb(79, 70, 229);

            string category = _cbCategory.SelectedItem != null ? _cbCategory.SelectedItem.ToString() : "General";
            string email = _txtEmail.Text.Trim();
            string subject = _txtSubject.Text.Trim();
            string message = _txtMessage.Text.Trim();

            ThreadPool.QueueUserWorkItem(state =>
            {
                string responseMsg;
                bool ok = TelemetryService.SubmitFeedback(category, email, subject, message, out responseMsg);

                this.BeginInvoke(new Action(() =>
                {
                    _btnSubmit.Enabled = true;
                    _btnSubmit.Text = "🚀 Submit Feedback";

                    if (ok)
                    {
                        _lblStatus.Text = "✔ Feedback received! Thank you for helping improve PrintPulse.";
                        _lblStatus.ForeColor = Color.FromArgb(22, 101, 52);
                        MessageBox.Show("Thank you! Your feedback has been sent directly to the development team.", "Feedback Submitted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close();
                    }
                    else
                    {
                        _lblStatus.Text = "Could not send directly: " + responseMsg;
                        _lblStatus.ForeColor = Color.FromArgb(225, 29, 72);
                        MessageBox.Show("Network issue sending feedback:\n" + responseMsg + "\n\nYou can click 'Copy to Clipboard' to save your message.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }));
            });
        }

        private void CopyDetails()
        {
            string category = _cbCategory.SelectedItem != null ? _cbCategory.SelectedItem.ToString() : "General";
            string text = string.Format("Category: {0}\nEmail: {1}\nSubject: {2}\n\nMessage:\n{3}\n\n(PrintPulse v1.0 on Windows {4})",
                category, _txtEmail.Text.Trim(), _txtSubject.Text.Trim(), _txtMessage.Text.Trim(), Environment.OSVersion.VersionString);

            Clipboard.SetText(text);
            MessageBox.Show("Feedback details copied to clipboard!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

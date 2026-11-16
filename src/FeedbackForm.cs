using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PrintPulse
{
    public class FeedbackForm : Form
    {
        private const string TargetEmail = "hs123253@gmail.com";
        private TextBox _txtSubject;
        private TextBox _txtMessage;
        private ComboBox _cbCategory;

        public FeedbackForm()
        {
            InitComponents();
        }

        private void InitComponents()
        {
            this.Text = "PrintPulse - Feedback & Feature Suggestions";
            this.Size = new Size(540, 480);
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
                Text = "Send Feedback & Suggestions",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(67, 56, 202),
                Location = new Point(20, 12),
                AutoSize = true
            };

            Label lblSubtitle = new Label
            {
                Text = "We value your ideas! Messages are routed to: " + TargetEmail,
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
                "Bug Report / Printing Issue",
                "New File Format Request",
                "UI / Usability Feedback",
                "General Question / Inquiries"
            });
            _cbCategory.SelectedIndex = 0;

            Label lblSub = new Label { Text = "Subject:", Location = new Point(25, 145), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85) };
            _txtSubject = new TextBox
            {
                Location = new Point(25, 168),
                Size = new Size(470, 25),
                Text = "PrintPulse Feedback: New Feature Idea"
            };

            Label lblMsg = new Label { Text = "Your Feedback / Message:", Location = new Point(25, 205), AutoSize = true, ForeColor = Color.FromArgb(51, 65, 85) };
            _txtMessage = new TextBox
            {
                Location = new Point(25, 228),
                Size = new Size(470, 130),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            // Buttons
            Button btnSend = new Button
            {
                Text = "✉ Send via Email",
                Location = new Point(25, 380),
                Size = new Size(160, 36),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += (s, e) => SendMail();

            Button btnCopy = new Button
            {
                Text = "📋 Copy to Clipboard",
                Location = new Point(195, 380),
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
                Location = new Point(395, 380),
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
            this.Controls.Add(lblSub);
            this.Controls.Add(_txtSubject);
            this.Controls.Add(lblMsg);
            this.Controls.Add(_txtMessage);
            this.Controls.Add(btnSend);
            this.Controls.Add(btnCopy);
            this.Controls.Add(btnClose);
        }

        private void SendMail()
        {
            try
            {
                string category = _cbCategory.SelectedItem != null ? _cbCategory.SelectedItem.ToString() : "General";
                string subject = Uri.EscapeDataString(string.Format("[PrintPulse - {0}] {1}", category, _txtSubject.Text.Trim()));
                string body = Uri.EscapeDataString(_txtMessage.Text.Trim() + "\n\n(Sent from PrintPulse v1.0 on Windows " + Environment.OSVersion.VersionString + ")");

                string mailtoUrl = string.Format("mailto:{0}?subject={1}&body={2}", TargetEmail, subject, body);
                Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not launch your email application automatically:\n" + ex.Message + "\n\nPlease click 'Copy to Clipboard' and send manually to " + TargetEmail,
                    "Email Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CopyDetails()
        {
            string category = _cbCategory.SelectedItem != null ? _cbCategory.SelectedItem.ToString() : "General";
            string text = string.Format("To: {0}\nSubject: [PrintPulse - {1}] {2}\n\nMessage:\n{3}\n\n(OS: Windows {4})",
                TargetEmail, category, _txtSubject.Text.Trim(), _txtMessage.Text.Trim(), Environment.OSVersion.VersionString);

            Clipboard.SetText(text);
            MessageBox.Show("Feedback details copied to clipboard!\nYou can paste it directly into your webmail (Gmail, Outlook, Yahoo) to " + TargetEmail,
                "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

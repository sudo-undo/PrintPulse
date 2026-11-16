using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PrintPulse
{
    public class ManualDuplexWizard : Form
    {
        public bool ResumeConfirmed { get; private set; }

        public ManualDuplexWizard(string documentName, int currentStep, int totalSheets)
        {
            this.Text = "PrintPulse — Manual Duplex Assistant";
            this.Size = new Size(580, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            BuildUI(documentName, currentStep, totalSheets);
        }

        private void BuildUI(string docName, int step, int totalSheets)
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.FromArgb(15, 23, 42)
            };

            Label lblTitle = new Label
            {
                Text = "Manual Double-Sided Printing Assistant",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(24, 18),
                AutoSize = true
            };

            Label lblSubtitle = new Label
            {
                Text = string.Format("Step {0} of 2 — Front sides printed ({1} sheets)", step, totalSheets),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(26, 48),
                AutoSize = true
            };

            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSubtitle);
            this.Controls.Add(header);

            // Instructions Panel
            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 15, 24, 15),
                BackColor = Color.FromArgb(248, 250, 252)
            };

            PictureBox pbDiagram = new PictureBox
            {
                Size = new Size(510, 150),
                Location = new Point(25, 10),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            pbDiagram.Paint += delegate(object sender, PaintEventArgs e)
            {
                DrawFlipDiagram(e.Graphics, pbDiagram.Width, pbDiagram.Height);
            };
            body.Controls.Add(pbDiagram);

            Label lblInstructions = new Label
            {
                Text = "1. Remove the printed stack from the output tray (do NOT shuffle the pages).\n" +
                       "2. Turn the stack 180° (face down, top edge in first as shown above).\n" +
                       "3. Place the stack back into Paper Tray 1 and click 'Resume Printing' below.",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(25, 170),
                Size = new Size(510, 60)
            };
            body.Controls.Add(lblInstructions);
            this.Controls.Add(body);

            // Footer
            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 65,
                BackColor = Color.White
            };

            Button btnCancel = new Button
            {
                Text = "Cancel Batch",
                Font = new Font("Segoe UI", 9.5f),
                Size = new Size(120, 38),
                Location = new Point(280, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(71, 85, 105),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnCancel.Click += delegate
            {
                ResumeConfirmed = false;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            Button btnResume = new Button
            {
                Text = "Resume Printing (Back Sides) →",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Size = new Size(210, 38),
                Location = new Point(415, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnResume.FlatAppearance.BorderSize = 0;
            btnResume.Click += delegate
            {
                ResumeConfirmed = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            footer.Controls.Add(btnCancel);
            footer.Controls.Add(btnResume);
            this.Controls.Add(footer);
        }

        private void DrawFlipDiagram(Graphics g, int w, int h)
        {
            g.SmoothingMode = SmoothingMode.HighQuality;

            // Box 1: Output tray
            Rectangle r1 = new Rectangle(40, 25, 110, 95);
            using (Brush b = new SolidBrush(Color.FromArgb(238, 242, 255)))
                g.FillRectangle(b, r1);
            using (Pen p = new Pen(Color.FromArgb(99, 102, 241), 2))
                g.DrawRectangle(p, r1);

            using (Font f = new Font("Segoe UI", 9, FontStyle.Bold))
            using (Brush br = new SolidBrush(Color.FromArgb(67, 56, 202)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("1. Remove\nFrom Tray", f, br, r1, sf);
            }

            // Arrow 1 -> 2
            using (Pen arrowPen = new Pen(Color.FromArgb(79, 70, 229), 3))
            {
                arrowPen.CustomEndCap = new AdjustableArrowCap(5, 5);
                g.DrawLine(arrowPen, 160, 72, 210, 72);
            }

            // Box 2: Flip 180°
            Rectangle r2 = new Rectangle(220, 25, 110, 95);
            using (Brush b = new SolidBrush(Color.FromArgb(254, 243, 199)))
                g.FillRectangle(b, r2);
            using (Pen p = new Pen(Color.FromArgb(245, 158, 11), 2))
                g.DrawRectangle(p, r2);

            using (Font f = new Font("Segoe UI", 9, FontStyle.Bold))
            using (Brush br = new SolidBrush(Color.FromArgb(180, 83, 9)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("2. Turn 180°\n(Face Down)", f, br, r2, sf);
            }

            // Arrow 2 -> 3
            using (Pen arrowPen = new Pen(Color.FromArgb(245, 158, 11), 3))
            {
                arrowPen.CustomEndCap = new AdjustableArrowCap(5, 5);
                g.DrawLine(arrowPen, 340, 72, 390, 72);
            }

            // Box 3: Feed Tray
            Rectangle r3 = new Rectangle(400, 25, 110, 95);
            using (Brush b = new SolidBrush(Color.FromArgb(220, 252, 231)))
                g.FillRectangle(b, r3);
            using (Pen p = new Pen(Color.FromArgb(34, 197, 94), 2))
                g.DrawRectangle(p, r3);

            using (Font f = new Font("Segoe UI", 9, FontStyle.Bold))
            using (Brush br = new SolidBrush(Color.FromArgb(21, 128, 61)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("3. Insert In\nTray 1", f, br, r3, sf);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PrintPulse
{
    public enum SlipSheetMode
    {
        None,
        BlankPage,
        DetailedSlip
    }

    public class AdvancedOptionsForm : Form
    {
        private ComboBox _cmbSlipSheet;
        private ComboBox _cmbPresets;
        private CheckBox _chkSkipBlank;
        private TextBox _txtWatchFolder;
        private Label _lblWatchStatus;
        private Button _btnToggleWatch;

        private MainForm _mainForm;

        public static SlipSheetMode CurrentSlipSheetMode = SlipSheetMode.None;
        public static bool GlobalSkipBlankPages = false;

        public AdvancedOptionsForm(MainForm mainForm)
        {
            _mainForm = mainForm;

            this.Text = "PrintPulse — Advanced Batch Options";
            this.Size = new Size(720, 640);
            this.MinimumSize = new Size(680, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252);

            TelemetryService.TrackFunnelStep("AdvancedOptions", "Opened");

            BuildUI();

            this.FormClosing += delegate
            {
                TelemetryService.TrackFunnelStep("AdvancedOptions", "Closed");
            };
        }

        private void BuildUI()
        {
            // Header
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(15, 23, 42)
            };

            Label lblTitle = new Label
            {
                Text = "Advanced Print & Automation Options",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 12),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = "Configure job separators, profiles, drop folder watching, and PDF export",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(22, 38),
                AutoSize = true
            };

            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            // Footer
            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = Color.White,
                Padding = new Padding(0, 0, 20, 0)
            };

            Button btnClose = CreateStyledButton("Done / Close", 0, 11, 120, 32, Color.FromArgb(37, 99, 235), Color.White);
            btnClose.Dock = DockStyle.Right;
            btnClose.Click += delegate { this.Close(); };
            footer.Controls.Add(btnClose);

            // Scrollable Content
            Panel content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 10, 15, 15),
                AutoScroll = true,
                AutoScrollMinSize = new Size(640, 520),
                BackColor = Color.FromArgb(248, 250, 252)
            };

            int y = 12;

            // Group 1: Presets & Profiles
            GroupBox gbPresets = CreateGroupBox("⚡ Quick Print Presets (Profiles)", 15, y, 650, 75);
            _cmbPresets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(20, 30), Size = new Size(410, 26), Font = new Font("Segoe UI", 9.5f) };
            _cmbPresets.Items.Add("Default Office (Fit, B&W, Single-Sided)");
            _cmbPresets.Items.Add("Eco-Draft (2-Up, Duplex, Toner Saver, Grayscale)");
            _cmbPresets.Items.Add("High-Quality Presentation (Color, Actual Size, Single-Sided)");
            _cmbPresets.Items.Add("Folded Booklet (Booklet Imposition, Duplex)");
            _cmbPresets.SelectedIndex = 0;
            gbPresets.Controls.Add(_cmbPresets);

            Button btnApplyPreset = CreateStyledButton("Apply to Queue", 445, 28, 185, 30, Color.FromArgb(37, 99, 235), Color.White);
            btnApplyPreset.Click += delegate
            {
                ApplySelectedPreset(_cmbPresets.SelectedIndex);
                TelemetryService.TrackFunnelStep("AdvancedOptions", "PresetApplied_" + _cmbPresets.SelectedIndex);
                MessageBox.Show("Preset applied to all queued items!", "PrintPulse", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            gbPresets.Controls.Add(btnApplyPreset);
            content.Controls.Add(gbPresets);
            y += 88;

            // Group 2: Slip-Sheet & Blank Page
            GroupBox gbSlip = CreateGroupBox("📄 Document Separation & Paper Saving", 15, y, 650, 80);
            Label lblSlip = new Label { Text = "Job Separator:", Location = new Point(20, 32), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
            gbSlip.Controls.Add(lblSlip);

            _cmbSlipSheet = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(125, 29), Size = new Size(220, 26), Font = new Font("Segoe UI", 9.5f) };
            _cmbSlipSheet.Items.Add("None (Print Continuously)");
            _cmbSlipSheet.Items.Add("Blank Sheet Between Files");
            _cmbSlipSheet.Items.Add("Document Summary Slip (Title & Info)");
            _cmbSlipSheet.SelectedIndex = (int)CurrentSlipSheetMode;
            _cmbSlipSheet.SelectedIndexChanged += delegate
            {
                CurrentSlipSheetMode = (SlipSheetMode)_cmbSlipSheet.SelectedIndex;
                TelemetryService.TrackFunnelStep("AdvancedOptions", "SlipSheetMode_" + CurrentSlipSheetMode);
            };
            gbSlip.Controls.Add(_cmbSlipSheet);

            _chkSkipBlank = new CheckBox
            {
                Text = "Smart Blank-Page Suppression (skip empty sheets)",
                Location = new Point(365, 31),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f),
                Checked = GlobalSkipBlankPages
            };
            _chkSkipBlank.CheckedChanged += delegate
            {
                GlobalSkipBlankPages = _chkSkipBlank.Checked;
                if (_mainForm != null) _mainForm.ApplyGlobalBlankPageSetting(GlobalSkipBlankPages);
                TelemetryService.TrackFunnelStep("AdvancedOptions", "SkipBlankPages_" + GlobalSkipBlankPages);
            };
            gbSlip.Controls.Add(_chkSkipBlank);
            content.Controls.Add(gbSlip);
            y += 93;

            // Group 3: Queue Auto-Sorting
            GroupBox gbSort = CreateGroupBox("🔀 Queue Auto-Sorting", 15, y, 650, 75);
            Button btnSortName = CreateStyledButton("By Name (A-Z)", 20, 28, 140, 32, Color.White, Color.FromArgb(30, 41, 59));
            btnSortName.Click += delegate { if (_mainForm != null) _mainForm.SortQueue(SortMode.Name); TelemetryService.TrackFunnelStep("AdvancedOptions", "Sort_Name"); };
            gbSort.Controls.Add(btnSortName);

            Button btnSortPages = CreateStyledButton("By Page Count", 175, 28, 140, 32, Color.White, Color.FromArgb(30, 41, 59));
            btnSortPages.Click += delegate { if (_mainForm != null) _mainForm.SortQueue(SortMode.PageCount); TelemetryService.TrackFunnelStep("AdvancedOptions", "Sort_Pages"); };
            gbSort.Controls.Add(btnSortPages);

            Button btnSortType = CreateStyledButton("By File Type", 330, 28, 140, 32, Color.White, Color.FromArgb(30, 41, 59));
            btnSortType.Click += delegate { if (_mainForm != null) _mainForm.SortQueue(SortMode.FileType); TelemetryService.TrackFunnelStep("AdvancedOptions", "Sort_Type"); };
            gbSort.Controls.Add(btnSortType);

            Button btnSortSize = CreateStyledButton("By File Size", 485, 28, 140, 32, Color.White, Color.FromArgb(30, 41, 59));
            btnSortSize.Click += delegate { if (_mainForm != null) _mainForm.SortQueue(SortMode.FileSize); TelemetryService.TrackFunnelStep("AdvancedOptions", "Sort_Size"); };
            gbSort.Controls.Add(btnSortSize);
            content.Controls.Add(gbSort);
            y += 88;

            // Group 4: Watch Folder
            GroupBox gbWatch = CreateGroupBox("📁 Watch Folder (Drop Folder Automation)", 15, y, 650, 95);
            _txtWatchFolder = new TextBox { Location = new Point(20, 28), Size = new Size(410, 24), Font = new Font("Segoe UI", 9.5f) };
            if (_mainForm != null && _mainForm.WatchService != null && !string.IsNullOrEmpty(_mainForm.WatchService.WatchPath))
                _txtWatchFolder.Text = _mainForm.WatchService.WatchPath;
            else
                _txtWatchFolder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PrintDrop");
            gbWatch.Controls.Add(_txtWatchFolder);

            Button btnBrowse = CreateStyledButton("Browse...", 440, 26, 90, 28, Color.White, Color.FromArgb(51, 65, 85));
            btnBrowse.Click += delegate
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog { Description = "Select folder to automatically watch for print jobs:" })
                {
                    if (fbd.ShowDialog(this) == DialogResult.OK)
                        _txtWatchFolder.Text = fbd.SelectedPath;
                }
            };
            gbWatch.Controls.Add(btnBrowse);

            bool isWatching = _mainForm != null && _mainForm.WatchService != null && _mainForm.WatchService.IsRunning;
            _btnToggleWatch = CreateStyledButton(isWatching ? "Stop Watch" : "Start Watch", 540, 26, 95, 28, isWatching ? Color.FromArgb(239, 68, 68) : Color.FromArgb(16, 185, 129), Color.White);
            _btnToggleWatch.Click += delegate
            {
                ToggleWatchFolder();
            };
            gbWatch.Controls.Add(_btnToggleWatch);

            _lblWatchStatus = new Label
            {
                Text = isWatching ? "Active: Automatically queuing files dropped into this directory." : "Status: Stopped (click Start Watch to automate)",
                Location = new Point(22, 60),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = isWatching ? Color.FromArgb(21, 128, 61) : Color.FromArgb(100, 116, 139)
            };
            gbWatch.Controls.Add(_lblWatchStatus);
            content.Controls.Add(gbWatch);
            y += 108;

            // Group 5: Merged PDF Export
            GroupBox gbPdf = CreateGroupBox("📑 Consolidated PDF Export", 15, y, 650, 75);
            Label lblPdfInfo = new Label { Text = "Combine all active files and ranges into a single PDF document.", Location = new Point(20, 32), AutoSize = true, Font = new Font("Segoe UI", 9.5f) };
            gbPdf.Controls.Add(lblPdfInfo);

            Button btnExportPdf = CreateStyledButton("Export as Merged PDF...", 410, 26, 220, 32, Color.FromArgb(79, 70, 229), Color.White);
            btnExportPdf.Click += delegate
            {
                if (_mainForm != null) _mainForm.TriggerMergedPdfExport();
            };
            gbPdf.Controls.Add(btnExportPdf);
            content.Controls.Add(gbPdf);

            // Add in order so Docking works without overlapping:
            this.Controls.Add(content);
            this.Controls.Add(footer);
            this.Controls.Add(header);

            header.BringToFront();
            footer.BringToFront();
            content.BringToFront();
        }

        private void ToggleWatchFolder()
        {
            if (_mainForm == null) return;
            if (_mainForm.WatchService.IsRunning)
            {
                _mainForm.WatchService.Stop();
                _btnToggleWatch.Text = "Start Watch";
                _btnToggleWatch.BackColor = Color.FromArgb(16, 185, 129);
                _lblWatchStatus.Text = "Status: Stopped (click Start Watch to automate)";
                _lblWatchStatus.ForeColor = Color.FromArgb(100, 116, 139);
                TelemetryService.TrackFunnelStep("WatchFolder", "Stopped");
            }
            else
            {
                string path = _txtWatchFolder.Text.Trim();
                if (!Directory.Exists(path))
                {
                    try { Directory.CreateDirectory(path); }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not create watch folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                _mainForm.WatchService.Start(path);
                _btnToggleWatch.Text = "Stop Watch";
                _btnToggleWatch.BackColor = Color.FromArgb(239, 68, 68);
                _lblWatchStatus.Text = "Active: Automatically queuing files dropped into " + path;
                _lblWatchStatus.ForeColor = Color.FromArgb(21, 128, 61);
                TelemetryService.TrackFunnelStep("WatchFolder", "Started");
            }
        }

        private void ApplySelectedPreset(int presetIdx)
        {
            if (_mainForm == null) return;
            switch (presetIdx)
            {
                case 0: // Default Office
                    _mainForm.ApplyPresetToQueue(PageScalingMode.Fit, ColorPrintMode.Monochrome, DuplexPrintMode.Simplex, PageLayoutMode.OneUp, false);
                    break;
                case 1: // Eco-Draft
                    _mainForm.ApplyPresetToQueue(PageScalingMode.Fit, ColorPrintMode.Monochrome, DuplexPrintMode.DuplexLongEdge, PageLayoutMode.TwoUp, true);
                    break;
                case 2: // High Quality Presentation
                    _mainForm.ApplyPresetToQueue(PageScalingMode.ActualSize, ColorPrintMode.Color, DuplexPrintMode.Simplex, PageLayoutMode.OneUp, false);
                    break;
                case 3: // Folded Booklet
                    _mainForm.ApplyPresetToQueue(PageScalingMode.Fit, ColorPrintMode.Monochrome, DuplexPrintMode.DuplexShortEdge, PageLayoutMode.Booklet, false);
                    break;
            }
        }

        private GroupBox CreateGroupBox(string text, int x, int y, int w, int h)
        {
            GroupBox gb = new GroupBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.White
            };
            return gb;
        }

        private Button CreateStyledButton(string text, int x, int y, int w, int h, Color bg, Color fg)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            if (bg == Color.White)
            {
                btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            }
            else
            {
                btn.FlatAppearance.BorderSize = 0;
            }
            return btn;
        }
    }

    public enum SortMode
    {
        Name,
        PageCount,
        FileType,
        FileSize
    }
}

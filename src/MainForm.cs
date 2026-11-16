using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PrintPulse
{
    public class MainForm : Form
    {
        private List<PrintJobItem> _queue = new List<PrintJobItem>();
        private PrintJobItem _selectedItem = null;
        private int _currentPreviewPage = 1;

        private BatchPrintController _controller;
        private BatchReport _lastReport;

        // UI Controls - Top
        private ComboBox _cbPrinters;
        private Label _lblPrinterStatus;

        // UI Controls - Left Queue
        private ListView _lvQueue;

        // UI Controls - Right Preview
        private PictureBox _pbPreview;
        private Label _lblPreviewMeta;
        private Button _btnPrevPage;
        private Button _btnNextPage;
        private Button _btnRotate;

        // Per-Item Property Controls
        private TextBox _txtPageRange;
        private NumericUpDown _numCopies;
        private ComboBox _cbColorMode;
        private ComboBox _cbDuplexMode;
        private ComboBox _cbNUp;

        // UI Controls - Bottom
        private ProgressBar _progressBar;
        private Label _lblStatusText;
        private Label _lblSummaryText;
        private Button _btnStart;
        private Button _btnPause;
        private Button _btnCancel;
        private Button _btnViewReport;

        private SplitContainer _split;
        private int _draggedItemIndex = -1;

        public MainForm()
        {
            _controller = new BatchPrintController();
            _controller.OnItemStarted += Controller_OnItemStarted;
            _controller.OnPageProgress += Controller_OnPageProgress;
            _controller.OnItemFinished += Controller_OnItemFinished;
            _controller.OnErrorDetected += Controller_OnErrorDetected;
            _controller.OnBatchFinished += Controller_OnBatchFinished;

            InitializeUI();
            RefreshPrinterList();
            UpdateSummaryMetrics();
        }

        private void InitializeUI()
        {
            this.Text = "PrintPulse - Batch Print Studio (Fully Unlocked)";
            this.Size = new Size(1100, 720);
            this.MinimumSize = new Size(880, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;
            this.Resize += MainForm_Resize;

            // =========================================================================
            // 1. TOP TOOLBAR (RESPONSIVE SPLIT: Left Actions Flow + Right Printer Dock)
            // =========================================================================
            Panel topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(8, 6, 8, 6)
            };

            // Top-Right: Printer Selection Panel
            Panel pnlTopRight = new Panel
            {
                Dock = DockStyle.Right,
                Width = 360,
                BackColor = Color.Transparent
            };

            Label lblPrinter = new Label
            {
                Text = "Target Printer:",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(5, 5),
                AutoSize = true
            };

            _lblPrinterStatus = new Label
            {
                Text = "Checking...",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(95, 6),
                AutoSize = true
            };

            _cbPrinters = new ComboBox
            {
                Location = new Point(5, 24),
                Size = new Size(310, 26),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cbPrinters.SelectedIndexChanged += (s, e) => UpdateSelectedPrinterStatus();

            Button btnRefreshPrinters = CreateModernButton("⟳", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 320, 23, 34, 27);
            btnRefreshPrinters.Click += (s, e) => RefreshPrinterList();

            pnlTopRight.Controls.Add(lblPrinter);
            pnlTopRight.Controls.Add(_lblPrinterStatus);
            pnlTopRight.Controls.Add(_cbPrinters);
            pnlTopRight.Controls.Add(btnRefreshPrinters);

            // Top-Left: Queue & Feedback Flow Panel
            FlowLayoutPanel flowTopLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = Color.Transparent
            };

            Button btnAddFiles = CreateModernButton("+ Add Files", Color.FromArgb(79, 70, 229), Color.White, 0, 0, 95, 32);
            btnAddFiles.Click += (s, e) => AddFilesDialog();

            Button btnAddFolder = CreateModernButton("+ Folder", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 75, 32);
            btnAddFolder.Click += (s, e) => AddFolderDialog();

            Button btnClear = CreateModernButton("Clear", Color.FromArgb(241, 245, 249), Color.FromArgb(225, 29, 72), 0, 0, 60, 32);
            btnClear.Click += (s, e) => ClearQueue();

            Button btnMoveUp = CreateModernButton("▲ Up", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 60, 32);
            btnMoveUp.Click += (s, e) => MoveSelectedItem(-1);

            Button btnMoveDown = CreateModernButton("▼ Down", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 70, 32);
            btnMoveDown.Click += (s, e) => MoveSelectedItem(1);

            Button btnRemove = CreateModernButton("✕ Remove", Color.FromArgb(241, 245, 249), Color.FromArgb(100, 116, 139), 0, 0, 80, 32);
            btnRemove.Click += (s, e) => RemoveSelectedItem();

            Button btnFeedback = CreateModernButton("💡 Feedback", Color.FromArgb(254, 243, 199), Color.FromArgb(180, 83, 9), 0, 0, 95, 32);
            btnFeedback.Click += (s, e) => OpenFeedback();

            flowTopLeft.Controls.Add(btnAddFiles);
            flowTopLeft.Controls.Add(btnAddFolder);
            flowTopLeft.Controls.Add(btnClear);
            flowTopLeft.Controls.Add(btnMoveUp);
            flowTopLeft.Controls.Add(btnMoveDown);
            flowTopLeft.Controls.Add(btnRemove);
            flowTopLeft.Controls.Add(btnFeedback);

            topBar.Controls.Add(flowTopLeft);
            topBar.Controls.Add(pnlTopRight);

            // =========================================================================
            // 2. BOTTOM ACTION BAR (RESPONSIVE SPLIT: Left Status Info + Right Buttons)
            // =========================================================================
            Panel bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 75,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };

            // Bottom-Right Action Buttons
            FlowLayoutPanel flowBottomRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 470,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Color.Transparent
            };

            _btnViewReport = CreateModernButton("📊 View Report", Color.FromArgb(241, 245, 249), Color.FromArgb(79, 70, 229), 0, 0, 110, 42);
            _btnViewReport.Enabled = false;
            _btnViewReport.Click += (s, e) => ShowReport();

            _btnStart = CreateModernButton("▶ START PRINT", Color.FromArgb(16, 185, 129), Color.White, 0, 0, 135, 42);
            _btnStart.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnStart.Click += (s, e) => StartBatch();

            _btnPause = CreateModernButton("❚❚ Pause", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 85, 42);
            _btnPause.Enabled = false;
            _btnPause.Click += (s, e) => TogglePause();

            _btnCancel = CreateModernButton("■ Stop", Color.FromArgb(241, 245, 249), Color.FromArgb(225, 29, 72), 0, 0, 75, 42);
            _btnCancel.Enabled = false;
            _btnCancel.Click += (s, e) => CancelBatch();

            flowBottomRight.Controls.Add(_btnViewReport);
            flowBottomRight.Controls.Add(_btnStart);
            flowBottomRight.Controls.Add(_btnPause);
            flowBottomRight.Controls.Add(_btnCancel);

            // Bottom-Left Status & Summary
            Panel pnlBottomLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 2, 8, 0)
            };

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 16,
                Style = ProgressBarStyle.Continuous
            };

            _lblStatusText = new Label
            {
                Text = "Ready - Drag & drop files or click Add Files to begin.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                Dock = DockStyle.Top,
                Height = 20,
                Padding = new Padding(0, 2, 0, 0)
            };

            _lblSummaryText = new Label
            {
                Text = "Queue: 0 Files | 0 Pages | 0 Sheets",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(79, 70, 229),
                Dock = DockStyle.Top,
                Height = 20
            };

            pnlBottomLeft.Controls.Add(_lblSummaryText);
            pnlBottomLeft.Controls.Add(_lblStatusText);
            pnlBottomLeft.Controls.Add(_progressBar);

            bottomBar.Controls.Add(pnlBottomLeft);
            bottomBar.Controls.Add(flowBottomRight);

            // =========================================================================
            // 3. RESPONSIVE SPLIT CONTAINER: QUEUE (LEFT) vs PREVIEW/SETTINGS (RIGHT)
            // =========================================================================
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(226, 232, 240)
            };

            // LEFT PANEL: QUEUE
            Panel pnlQueue = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Label lblQueueHeader = new Label
            {
                Text = "PRINT QUEUE (DRAG TO REORDER)",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(10, 6, 0, 0)
            };

            _lvQueue = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true,
                HideSelection = false,
                AllowDrop = true
            };
            _lvQueue.Columns.Add("", 28);
            _lvQueue.Columns.Add("#", 32);
            _lvQueue.Columns.Add("File Name", 220);
            _lvQueue.Columns.Add("Pages", 50);
            _lvQueue.Columns.Add("Copies", 50);
            _lvQueue.Columns.Add("Color", 60);
            _lvQueue.Columns.Add("Duplex", 70);
            _lvQueue.Columns.Add("Status", 90);

            _lvQueue.SelectedIndexChanged += LvQueue_SelectedIndexChanged;
            _lvQueue.ItemCheck += LvQueue_ItemCheck;
            _lvQueue.ItemDrag += LvQueue_ItemDrag;
            _lvQueue.DragEnter += (s, e) => e.Effect = DragDropEffects.Move;
            _lvQueue.DragOver += LvQueue_DragOver;
            _lvQueue.DragDrop += LvQueue_DragDrop;

            pnlQueue.Controls.Add(_lvQueue);
            pnlQueue.Controls.Add(lblQueueHeader);
            _split.Panel1.Controls.Add(pnlQueue);

            // RIGHT PANEL: PREVIEW & CUSTOM SETTINGS
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            Label lblPreviewHeader = new Label
            {
                Text = "DOCUMENT PREVIEW & CUSTOM SETTINGS",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(10, 6, 0, 0)
            };

            // Preview Navigation Bar (FlowLayout so it never clips)
            FlowLayoutPanel pnlPreviewBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                WrapContents = false,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(6, 4, 6, 4)
            };

            _btnPrevPage = CreateModernButton("◄ Prev", Color.White, Color.FromArgb(51, 65, 85), 0, 0, 65, 28);
            _btnPrevPage.Click += (s, e) => ChangePreviewPage(-1);

            _lblPreviewMeta = new Label
            {
                Text = "No file selected",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                Size = new Size(130, 26),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _btnNextPage = CreateModernButton("Next ►", Color.White, Color.FromArgb(51, 65, 85), 0, 0, 65, 28);
            _btnNextPage.Click += (s, e) => ChangePreviewPage(1);

            _btnRotate = CreateModernButton("⟲ Rotate 90°", Color.White, Color.FromArgb(79, 70, 229), 0, 0, 95, 28);
            _btnRotate.Click += (s, e) => RotateCurrentDocument();

            pnlPreviewBar.Controls.Add(_btnPrevPage);
            pnlPreviewBar.Controls.Add(_lblPreviewMeta);
            pnlPreviewBar.Controls.Add(_btnNextPage);
            pnlPreviewBar.Controls.Add(_btnRotate);

            // Document View Box
            _pbPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(241, 245, 249),
                SizeMode = PictureBoxSizeMode.CenterImage
            };

            // Responsive Settings Panel (TableLayout to ensure zero clipping!)
            Panel pnlSettingsWrapper = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 150,
                BackColor = Color.FromArgb(250, 252, 255),
                Padding = new Padding(8, 6, 8, 6)
            };

            Label lblSettingsTitle = new Label
            {
                Text = "SETTINGS FOR SELECTED ITEM",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(79, 70, 229),
                Dock = DockStyle.Top,
                Height = 20
            };

            TableLayoutPanel tableSettings = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 76,
                ColumnCount = 4,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            tableSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
            tableSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tableSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
            tableSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // Row 0
            Label lblRange = new Label { Text = "Pages:", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            _txtPageRange = new TextBox { Text = "All", Dock = DockStyle.Fill };
            _txtPageRange.TextChanged += (s, e) => UpdateSelectedSettings();

            Label lblCopies = new Label { Text = "Copies:", AutoSize = true, Margin = new Padding(4, 6, 0, 0) };
            _numCopies = new NumericUpDown { Minimum = 1, Maximum = 99, Value = 1, Dock = DockStyle.Fill };
            _numCopies.ValueChanged += (s, e) => UpdateSelectedSettings();

            tableSettings.Controls.Add(lblRange, 0, 0);
            tableSettings.Controls.Add(_txtPageRange, 1, 0);
            tableSettings.Controls.Add(lblCopies, 2, 0);
            tableSettings.Controls.Add(_numCopies, 3, 0);

            // Row 1
            Label lblDuplex = new Label { Text = "Duplex:", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            _cbDuplexMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _cbDuplexMode.Items.AddRange(new object[] { "1-Sided", "2-Sided (Long)", "2-Sided (Short)" });
            _cbDuplexMode.SelectedIndex = 0;
            _cbDuplexMode.SelectedIndexChanged += (s, e) => UpdateSelectedSettings();

            Label lblColor = new Label { Text = "Color:", AutoSize = true, Margin = new Padding(4, 6, 0, 0) };
            _cbColorMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _cbColorMode.Items.AddRange(new object[] { "B&W", "Color" });
            _cbColorMode.SelectedIndex = 0;
            _cbColorMode.SelectedIndexChanged += (s, e) => UpdateSelectedSettings();

            tableSettings.Controls.Add(lblDuplex, 0, 1);
            tableSettings.Controls.Add(_cbDuplexMode, 1, 1);
            tableSettings.Controls.Add(lblColor, 2, 1);
            tableSettings.Controls.Add(_cbColorMode, 3, 1);

            // Row 2: Layout & Apply Button
            FlowLayoutPanel flowSettingsBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Color.Transparent
            };

            Label lblNUp = new Label { Text = "Layout:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) };
            _cbNUp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
            _cbNUp.Items.AddRange(new object[] { "1 Page / Sheet", "2 Pages / Sheet", "4 Pages / Sheet" });
            _cbNUp.SelectedIndex = 0;
            _cbNUp.SelectedIndexChanged += (s, e) => UpdateSelectedSettings();

            Button btnApplyAll = CreateModernButton("Apply to All Items in Queue", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 185, 28);
            btnApplyAll.Click += (s, e) => ApplySettingsToAll();

            flowSettingsBottom.Controls.Add(lblNUp);
            flowSettingsBottom.Controls.Add(_cbNUp);
            flowSettingsBottom.Controls.Add(btnApplyAll);

            pnlSettingsWrapper.Controls.Add(flowSettingsBottom);
            pnlSettingsWrapper.Controls.Add(tableSettings);
            pnlSettingsWrapper.Controls.Add(lblSettingsTitle);

            pnlRight.Controls.Add(_pbPreview);
            pnlRight.Controls.Add(pnlPreviewBar);
            pnlRight.Controls.Add(lblPreviewHeader);
            pnlRight.Controls.Add(pnlSettingsWrapper);
            _split.Panel2.Controls.Add(pnlRight);

            // Assemble main form
            this.Controls.Add(_split);
            this.Controls.Add(topBar);
            this.Controls.Add(bottomBar);

            this.Shown += (s, e) => SetProportionalSplit();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            SetProportionalSplit();
        }

        private void SetProportionalSplit()
        {
            try
            {
                if (_split != null && _split.Width > 200)
                {
                    int targetDistance = (int)(_split.Width * 0.52);
                    if (targetDistance > 100 && targetDistance < _split.Width - 100)
                    {
                        _split.SplitterDistance = targetDistance;
                    }
                }
            }
            catch { }
        }

        private Button CreateModernButton(string text, Color bg, Color fg, int x, int y, int w, int h = 32)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(2, 1, 2, 1)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void OpenFeedback()
        {
            FeedbackForm fb = new FeedbackForm();
            fb.ShowDialog(this);
        }

        private void RefreshPrinterList()
        {
            _cbPrinters.Items.Clear();
            List<NativeSpooler.PrinterDevice> printers = NativeSpooler.GetInstalledPrinters();
            int defaultIndex = 0;

            for (int i = 0; i < printers.Count; i++)
            {
                _cbPrinters.Items.Add(printers[i]);
                if (printers[i].IsDefault)
                    defaultIndex = i;
            }

            if (_cbPrinters.Items.Count > 0)
                _cbPrinters.SelectedIndex = defaultIndex;

            UpdateSelectedPrinterStatus();
        }

        private void UpdateSelectedPrinterStatus()
        {
            NativeSpooler.PrinterDevice dev = _cbPrinters.SelectedItem as NativeSpooler.PrinterDevice;
            if (dev != null)
            {
                uint status = NativeSpooler.GetPrinterStatus(dev.Name);
                dev.Status = status;
                _lblPrinterStatus.Text = dev.StatusDescription;
                if (status == 0)
                    _lblPrinterStatus.ForeColor = Color.FromArgb(16, 185, 129);
                else
                    _lblPrinterStatus.ForeColor = Color.FromArgb(225, 29, 72);
            }
        }

        // File Ingestion
        private void AddFilesDialog()
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Documents and Images to Batch Print",
                Filter = "All Supported Files|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif;*.txt;*.csv;*.rtf|PDF Documents (*.pdf)|*.pdf|Image Files (*.jpg;*.png;*.bmp)|*.jpg;*.png;*.bmp|Text Files (*.txt;*.csv)|*.txt;*.csv|All Files (*.*)|*.*"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                AddFilePaths(ofd.FileNames);
            }
        }

        private void AddFolderDialog()
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog
            {
                Description = "Select a folder containing documents to batch print"
            };
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                string[] files = Directory.GetFiles(fbd.SelectedPath, "*.*", SearchOption.TopDirectoryOnly);
                AddFilePaths(files);
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFilePaths(files);
            }
        }

        private void AddFilePaths(string[] filePaths)
        {
            foreach (string path in filePaths)
            {
                if (Directory.Exists(path))
                {
                    AddFilePaths(Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly));
                    continue;
                }

                if (!File.Exists(path)) continue;

                PrintJobItem item = new PrintJobItem(path);
                item.TotalPages = DocumentRenderer.InspectPageCount(item);
                _queue.Add(item);
            }

            RefreshQueueListView();
            UpdateSummaryMetrics();
        }

        private void RefreshQueueListView()
        {
            _lvQueue.BeginUpdate();
            _lvQueue.Items.Clear();

            for (int i = 0; i < _queue.Count; i++)
            {
                PrintJobItem item = _queue[i];
                ListViewItem lvi = new ListViewItem("");
                lvi.Checked = item.IsEnabled;
                lvi.Tag = item;
                lvi.SubItems.Add((i + 1).ToString());
                lvi.SubItems.Add(item.FileName);
                lvi.SubItems.Add(item.TotalPages.ToString());
                lvi.SubItems.Add(item.Copies.ToString());
                lvi.SubItems.Add(item.ColorMode == ColorPrintMode.Color ? "Color" : "B&W");
                lvi.SubItems.Add(item.DuplexMode == DuplexPrintMode.Simplex ? "1-Sided" : "2-Sided");
                lvi.SubItems.Add(item.Status.ToString());

                if (item.Status == PrintItemStatus.Completed)
                    lvi.ForeColor = Color.FromArgb(22, 101, 52);
                else if (item.Status == PrintItemStatus.Printing)
                    lvi.ForeColor = Color.FromArgb(79, 70, 229);
                else if (item.Status == PrintItemStatus.Error)
                    lvi.ForeColor = Color.FromArgb(225, 29, 72);

                _lvQueue.Items.Add(lvi);
            }

            _lvQueue.EndUpdate();
        }

        private void LvQueue_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_lvQueue.SelectedItems.Count > 0)
            {
                _selectedItem = _lvQueue.SelectedItems[0].Tag as PrintJobItem;
                _currentPreviewPage = 1;
                LoadItemPropertiesToInputs(_selectedItem);
                RenderPreview();
            }
        }

        private void LvQueue_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.Index >= 0 && e.Index < _queue.Count)
            {
                _queue[e.Index].IsEnabled = (e.NewValue == CheckState.Checked);
                this.BeginInvoke(new Action(UpdateSummaryMetrics));
            }
        }

        // Drag & Drop Queue Reordering
        private void LvQueue_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (_lvQueue.SelectedIndices.Count > 0)
            {
                _draggedItemIndex = _lvQueue.SelectedIndices[0];
                _lvQueue.DoDragDrop(_lvQueue.SelectedItems[0], DragDropEffects.Move);
            }
        }

        private void LvQueue_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void LvQueue_DragDrop(object sender, DragEventArgs e)
        {
            Point targetPoint = _lvQueue.PointToClient(new Point(e.X, e.Y));
            ListViewItem targetItem = _lvQueue.GetItemAt(targetPoint.X, targetPoint.Y);

            if (_draggedItemIndex >= 0 && targetItem != null)
            {
                int targetIndex = targetItem.Index;
                if (_draggedItemIndex != targetIndex)
                {
                    PrintJobItem item = _queue[_draggedItemIndex];
                    _queue.RemoveAt(_draggedItemIndex);
                    _queue.Insert(targetIndex, item);

                    RefreshQueueListView();
                    _lvQueue.Items[targetIndex].Selected = true;
                    UpdateSummaryMetrics();
                }
            }
            _draggedItemIndex = -1;
        }

        private void MoveSelectedItem(int direction)
        {
            if (_lvQueue.SelectedIndices.Count == 0) return;
            int currentIndex = _lvQueue.SelectedIndices[0];
            int newIndex = currentIndex + direction;

            if (newIndex >= 0 && newIndex < _queue.Count)
            {
                PrintJobItem item = _queue[currentIndex];
                _queue.RemoveAt(currentIndex);
                _queue.Insert(newIndex, item);

                RefreshQueueListView();
                _lvQueue.Items[newIndex].Selected = true;
                _lvQueue.EnsureVisible(newIndex);
            }
        }

        private void RemoveSelectedItem()
        {
            if (_lvQueue.SelectedIndices.Count == 0) return;
            int idx = _lvQueue.SelectedIndices[0];
            _queue.RemoveAt(idx);
            _selectedItem = null;
            RefreshQueueListView();
            UpdateSummaryMetrics();
            if (_pbPreview.Image != null) _pbPreview.Image.Dispose();
            _pbPreview.Image = null;
            _lblPreviewMeta.Text = "No file selected";
        }

        private void ClearQueue()
        {
            if (_queue.Count == 0) return;
            if (MessageBox.Show("Are you sure you want to clear the entire queue?", "Clear Queue", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _queue.Clear();
                _selectedItem = null;
                RefreshQueueListView();
                UpdateSummaryMetrics();
                if (_pbPreview.Image != null) _pbPreview.Image.Dispose();
                _pbPreview.Image = null;
                _lblPreviewMeta.Text = "No file selected";
            }
        }

        // Preview rendering
        private void RenderPreview()
        {
            if (_selectedItem == null) return;

            int targetW = _pbPreview.Width > 50 ? _pbPreview.Width - 16 : 380;
            int targetH = _pbPreview.Height > 50 ? _pbPreview.Height - 16 : 420;

            if (_pbPreview.Image != null) _pbPreview.Image.Dispose();
            _pbPreview.Image = DocumentRenderer.RenderPageThumbnail(_selectedItem, _currentPreviewPage, targetW, targetH);
            _lblPreviewMeta.Text = string.Format("Page {0} of {1}", _currentPreviewPage, _selectedItem.TotalPages);
        }

        private void ChangePreviewPage(int delta)
        {
            if (_selectedItem == null) return;
            int newPage = _currentPreviewPage + delta;
            if (newPage >= 1 && newPage <= _selectedItem.TotalPages)
            {
                _currentPreviewPage = newPage;
                RenderPreview();
            }
        }

        private void RotateCurrentDocument()
        {
            if (_selectedItem == null) return;
            _selectedItem.Rotate90();
            RenderPreview();
        }

        // Properties bindings
        private void LoadItemPropertiesToInputs(PrintJobItem item)
        {
            if (item == null) return;
            _txtPageRange.Text = item.PageRange;
            _numCopies.Value = Math.Max(1, item.Copies);
            _cbColorMode.SelectedIndex = (item.ColorMode == ColorPrintMode.Color) ? 1 : 0;
            _cbDuplexMode.SelectedIndex = (int)item.DuplexMode;
            _cbNUp.SelectedIndex = (item.NUp == 4) ? 2 : (item.NUp == 2 ? 1 : 0);
        }

        private void UpdateSelectedSettings()
        {
            if (_selectedItem == null) return;
            _selectedItem.PageRange = _txtPageRange.Text;
            _selectedItem.Copies = (int)_numCopies.Value;
            _selectedItem.ColorMode = (_cbColorMode.SelectedIndex == 1) ? ColorPrintMode.Color : ColorPrintMode.Monochrome;
            _selectedItem.DuplexMode = (DuplexPrintMode)_cbDuplexMode.SelectedIndex;
            _selectedItem.NUp = (_cbNUp.SelectedIndex == 2) ? 4 : (_cbNUp.SelectedIndex == 1 ? 2 : 1);

            UpdateSummaryMetrics();
        }

        private void ApplySettingsToAll()
        {
            if (_selectedItem == null) return;
            foreach (var item in _queue)
            {
                item.PageRange = _txtPageRange.Text;
                item.Copies = (int)_numCopies.Value;
                item.ColorMode = (_cbColorMode.SelectedIndex == 1) ? ColorPrintMode.Color : ColorPrintMode.Monochrome;
                item.DuplexMode = (DuplexPrintMode)_cbDuplexMode.SelectedIndex;
                item.NUp = (_cbNUp.SelectedIndex == 2) ? 4 : (_cbNUp.SelectedIndex == 1 ? 2 : 1);
            }
            RefreshQueueListView();
            UpdateSummaryMetrics();
            MessageBox.Show("Settings applied to all items in queue!", "Settings Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateSummaryMetrics()
        {
            int totalFiles = 0;
            int totalPages = 0;
            int totalSheets = 0;

            foreach (var item in _queue)
            {
                if (!item.IsEnabled) continue;
                totalFiles++;
                int pages = item.ResolvePageIndices().Count * item.Copies;
                totalPages += pages;
                totalSheets += item.CalculateSheetCount();
            }

            _lblSummaryText.Text = string.Format("Active: {0} Files | {1} Pages | {2} Sheets to Print",
                totalFiles, totalPages, totalSheets);
        }

        // Batch Execution
        private void StartBatch()
        {
            if (_queue.Count == 0)
            {
                MessageBox.Show("Please add at least one file to the print queue.", "Queue Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_cbPrinters.SelectedItem == null)
            {
                MessageBox.Show("Please select a target printer.", "No Printer Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            NativeSpooler.PrinterDevice dev = (NativeSpooler.PrinterDevice)_cbPrinters.SelectedItem;

            _btnStart.Enabled = false;
            _btnPause.Enabled = true;
            _btnCancel.Enabled = true;
            _btnViewReport.Enabled = false;

            _progressBar.Value = 0;
            _progressBar.Maximum = Math.Max(1, _queue.Count);

            _controller.StartBatch(new List<PrintJobItem>(_queue), dev.Name);
        }

        private void TogglePause()
        {
            if (_btnPause.Text.Contains("Pause"))
            {
                _controller.RequestPause();
                _btnPause.Text = "▶ Resume";
            }
            else
            {
                _controller.RequestResume();
                _btnPause.Text = "❚❚ Pause";
            }
        }

        private void CancelBatch()
        {
            if (MessageBox.Show("Are you sure you want to stop the print batch?", "Confirm Stop", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _controller.RequestCancel();
            }
        }

        private void ShowReport()
        {
            if (_lastReport != null)
            {
                ReportForm rf = new ReportForm(_lastReport);
                rf.ShowDialog(this);
            }
        }

        // Controller Event Handlers
        private void Controller_OnItemStarted(PrintJobItem item)
        {
            this.BeginInvoke(new Action(() =>
            {
                _lblStatusText.Text = "Printing " + item.FileName + "...";
                RefreshQueueListView();
            }));
        }

        private void Controller_OnPageProgress(PrintJobItem item, int current, int total)
        {
            this.BeginInvoke(new Action(() =>
            {
                _lblStatusText.Text = string.Format("Printing {0} - Page {1} of {2}...", item.FileName, current, total);
            }));
        }

        private void Controller_OnItemFinished(PrintJobItem item)
        {
            this.BeginInvoke(new Action(() =>
            {
                if (_progressBar.Value < _progressBar.Maximum)
                    _progressBar.Value++;
                RefreshQueueListView();
            }));
        }

        private void Controller_OnErrorDetected(PrintJobItem item, string error, int page, Action<ErrorRecoveryChoice, int> resumeCallback)
        {
            this.Invoke(new Action(() =>
            {
                ReprintDialog dlg = new ReprintDialog(item, error, page);
                dlg.ShowDialog(this);
                resumeCallback(dlg.SelectedChoice, dlg.ResumePageNumber);
            }));
        }

        private void Controller_OnBatchFinished(BatchReport report)
        {
            this.BeginInvoke(new Action(() =>
            {
                _lastReport = report;
                _btnStart.Enabled = true;
                _btnPause.Enabled = false;
                _btnPause.Text = "❚❚ Pause";
                _btnCancel.Enabled = false;
                _btnViewReport.Enabled = true;
                _lblStatusText.Text = "Batch Completed! Check report for details.";
                _progressBar.Value = _progressBar.Maximum;

                RefreshQueueListView();
                ShowReport();
            }));
        }
    }
}

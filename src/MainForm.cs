using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public WatchFolderService WatchService { get; private set; }

        // UI Controls - Top
        private ComboBox _cbPrinters;
        private Label _lblPrinterStatus;

        // UI Controls - Left Queue
        private ListView _lvQueue;
        private Label _lblQueueTitle;
        private Panel _pnlEmptyState;
        private Panel _pnlSmartAdvisor;
        private Label _lblAdvisorText;
        private Button _btnAdvisorApply;
        private Action _advisorApplyAction;
        private Panel _pnlUpdateBanner;
        private Label _lblUpdateText;
        private Button _btnUpdateNow;
        private UpdateInfo _pendingUpdate;
        private ToolTip _toolTip;

        // UI Controls - Right Preview
        private PictureBox _pbPreview;
        private Label _lblPreviewMeta;
        private Button _btnPrevPage;
        private Button _btnNextPage;
        private Button _btnRotate;

        // Adobe Acrobat Style Property Controls
        private NumericUpDown _numCopies;
        private RadioButton _rbPagesAll;
        private RadioButton _rbPagesCurrent;
        private RadioButton _rbPagesCustom;
        private TextBox _txtCustomPages;
        private ComboBox _cbPageSubset;
        private CheckBox _chkReverseOrder;

        private RadioButton _rbScaleFit;
        private RadioButton _rbScaleActual;
        private RadioButton _rbScaleShrink;
        private RadioButton _rbScaleCustom;
        private NumericUpDown _numCustomScale;

        private ComboBox _cbLayoutMode;
        private ComboBox _cbColorMode;
        private CheckBox _chkTonerSaver;
        private ComboBox _cbDuplexMode;
        private ComboBox _cbPaperTray;

        private RadioButton _rbOrientAuto;
        private RadioButton _rbOrientPortrait;
        private RadioButton _rbOrientLandscape;

        // Watermark & Bates Stamping (PMF)
        private ComboBox _cbWatermark;
        private CheckBox _chkBates;
        private TextBox _txtBatesPrefix;
        public double CostPerBwSheet = 0.05;
        public double CostPerColorSheet = 0.20;

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
        private bool _isUpdatingInputs = false;

        public MainForm(string[] initialFiles = null)
        {
            _controller = new BatchPrintController();
            _controller.OnItemStarted += Controller_OnItemStarted;
            _controller.OnPageProgress += Controller_OnPageProgress;
            _controller.OnItemFinished += Controller_OnItemFinished;
            _controller.OnErrorDetected += Controller_OnErrorDetected;
            _controller.OnBatchFinished += Controller_OnBatchFinished;
            _controller.OnRequestManualDuplexFlip += Controller_OnRequestManualDuplexFlip;

            WatchService = new WatchFolderService();
            WatchService.FileDropped += WatchService_FileDropped;

            InitializeUI();
            RefreshPrinterList();
            UpdateSummaryMetrics();

            this.Shown += delegate
            {
                try
                {
                    if (_split != null && this.Width > 200)
                    {
                        _split.SplitterDistance = (int)(this.Width * 0.48);
                    }
                    RestorePreviousSession();
                    if (initialFiles != null && initialFiles.Length > 0)
                    {
                        ProcessInitialArguments(initialFiles);
                    }
                    TelemetryService.TrackSessionStart();
                    if (UpdateManager.CheckOnStartup)
                    {
                        UpdateManager.CheckForUpdatesAsync(this, OnStartupUpdateCheckResult, false);
                    }
                }
                catch { }
            };
        }

        private void InitializeUI()
        {
            this.Text = "PrintPulse v1.1.0 — Universal Batch Print Studio (Commercial Grade)";
            this.Size = new Size(1160, 760);
            this.MinimumSize = new Size(940, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;
            this.Resize += MainForm_Resize;
            this.FormClosing += MainForm_FormClosing;

            // =========================================================================
            // 1. TOP TOOLBAR
            // =========================================================================
            Panel topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(8, 6, 8, 6)
            };

            Panel pnlTopRight = new Panel
            {
                Dock = DockStyle.Right,
                Width = 370,
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
                Text = "Ready",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(95, 6),
                AutoSize = true
            };

            _cbPrinters = new ComboBox
            {
                Location = new Point(5, 24),
                Size = new Size(320, 26),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cbPrinters.SelectedIndexChanged += delegate { UpdateSelectedPrinterStatus(); };

            Button btnRefreshPrinters = CreateModernButton("⟳", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 330, 23, 34, 27);
            btnRefreshPrinters.Click += delegate { RefreshPrinterList(); };

            pnlTopRight.Controls.Add(lblPrinter);
            pnlTopRight.Controls.Add(_lblPrinterStatus);
            pnlTopRight.Controls.Add(_cbPrinters);
            pnlTopRight.Controls.Add(btnRefreshPrinters);

            FlowLayoutPanel flowTopLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = Color.Transparent
            };

            Button btnAddFiles = CreateModernButton("+ Add Files", Color.FromArgb(79, 70, 229), Color.White, 0, 0, 105, 34);
            btnAddFiles.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAddFiles.Click += delegate { AddFilesDialog(); };

            Button btnAddFolder = CreateModernButton("📁 + Folder", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 95, 34);
            btnAddFolder.Click += delegate { AddFolderDialog(); };

            Button btnClear = CreateModernButton("🗑 Clear", Color.FromArgb(241, 245, 249), Color.FromArgb(225, 29, 72), 0, 0, 75, 34);
            btnClear.Click += delegate { ClearQueue(); };

            Button btnMoveUp = CreateModernButton("▲ Up", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 65, 34);
            btnMoveUp.Click += delegate { MoveSelectedItem(-1); };

            Button btnMoveDown = CreateModernButton("▼ Down", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 75, 34);
            btnMoveDown.Click += delegate { MoveSelectedItem(1); };

            Button btnRemove = CreateModernButton("✕ Remove", Color.FromArgb(241, 245, 249), Color.FromArgb(100, 116, 139), 0, 0, 85, 34);
            btnRemove.Click += delegate { RemoveSelectedItem(); };

            flowTopLeft.Controls.Add(btnAddFiles);
            flowTopLeft.Controls.Add(btnAddFolder);
            flowTopLeft.Controls.Add(btnClear);
            flowTopLeft.Controls.Add(btnMoveUp);
            flowTopLeft.Controls.Add(btnMoveDown);
            flowTopLeft.Controls.Add(btnRemove);

            topBar.Controls.Add(flowTopLeft);
            topBar.Controls.Add(pnlTopRight);

            // Tooltips
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 350;
            _toolTip.ReshowDelay = 200;

            _toolTip.SetToolTip(btnAddFiles, "Add files to print (PDF, Word DOCX, text files, or images)");
            _toolTip.SetToolTip(btnAddFolder, "Add all supported documents from a folder into the queue");
            _toolTip.SetToolTip(btnClear, "Clear all documents from the queue");
            _toolTip.SetToolTip(btnMoveUp, "Move selected document earlier in the print sequence");
            _toolTip.SetToolTip(btnMoveDown, "Move selected document later in the print sequence");
            _toolTip.SetToolTip(btnRemove, "Remove selected file from the queue");
            _toolTip.SetToolTip(btnRefreshPrinters, "Scan and refresh installed Windows printers");

            // =========================================================================
            // 2. BOTTOM ACTION BAR
            // =========================================================================
            Panel bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 75,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };

            FlowLayoutPanel flowBottomRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 640,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Color.Transparent
            };

            _btnViewReport = CreateModernButton("📊 View Report", Color.FromArgb(241, 245, 249), Color.FromArgb(79, 70, 229), 0, 0, 110, 42);
            _btnViewReport.Enabled = false;
            _btnViewReport.Click += delegate { ShowReport(); };

            _btnStart = CreateModernButton("▶ START PRINT", Color.FromArgb(16, 185, 129), Color.White, 0, 0, 145, 42);
            _btnStart.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnStart.Click += delegate { StartBatch(); };

            _btnPause = CreateModernButton("❚❚ Pause", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 0, 0, 85, 42);
            _btnPause.Enabled = false;
            _btnPause.Click += delegate { TogglePause(); };

            _btnCancel = CreateModernButton("■ Stop", Color.FromArgb(241, 245, 249), Color.FromArgb(225, 29, 72), 0, 0, 75, 42);
            _btnCancel.Enabled = false;
            _btnCancel.Click += delegate { CancelBatch(); };

            Button btnAdvanced = CreateModernButton("⚙ Advanced", Color.FromArgb(241, 245, 249), Color.FromArgb(79, 70, 229), 0, 0, 105, 42);
            btnAdvanced.Click += delegate { OpenAdvancedOptions(); };

            Button btnFeedback = CreateModernButton("💬 Feedback", Color.FromArgb(241, 245, 249), Color.FromArgb(100, 116, 139), 0, 0, 95, 42);
            btnFeedback.Click += delegate { OpenFeedback(); };

            _toolTip.SetToolTip(_btnStart, "Launch sequential batch printing on the target printer");
            _toolTip.SetToolTip(_btnPause, "Temporarily pause print spooling");
            _toolTip.SetToolTip(_btnCancel, "Stop batch printing immediately");
            _toolTip.SetToolTip(_btnViewReport, "View print audit report and cost receipt");
            _toolTip.SetToolTip(btnAdvanced, "Open Advanced Options (Presets, Separators, Watch Folder, Merged PDF)");
            _toolTip.SetToolTip(btnFeedback, "Send feedback or feature suggestions directly to developers");

            flowBottomRight.Controls.Add(btnFeedback);
            flowBottomRight.Controls.Add(btnAdvanced);
            flowBottomRight.Controls.Add(_btnViewReport);
            flowBottomRight.Controls.Add(_btnStart);
            flowBottomRight.Controls.Add(_btnPause);
            flowBottomRight.Controls.Add(_btnCancel);

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

            // LEFT PANEL: QUEUE + SMART ADVISOR
            Panel pnlQueue = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // Smart Print Advisor Banner
            _pnlSmartAdvisor = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(254, 243, 199),
                Visible = false,
                Padding = new Padding(10, 6, 10, 6)
            };

            _lblAdvisorText = new Label
            {
                Text = "💡 Tip: Smart recommendation available.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(146, 64, 14),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _btnAdvisorApply = CreateModernButton("Apply", Color.FromArgb(217, 119, 6), Color.White, 0, 0, 65, 26);
            _btnAdvisorApply.Dock = DockStyle.Right;
            _btnAdvisorApply.Click += delegate
            {
                if (_advisorApplyAction != null)
                {
                    _advisorApplyAction();
                    _pnlSmartAdvisor.Visible = false;
                }
            };

            Button btnDismissAdvisor = CreateModernButton("✕", Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14), 0, 0, 26, 26);
            btnDismissAdvisor.Dock = DockStyle.Right;
            btnDismissAdvisor.FlatAppearance.BorderSize = 0;
            btnDismissAdvisor.Click += delegate { _pnlSmartAdvisor.Visible = false; };

            _pnlSmartAdvisor.Controls.Add(_lblAdvisorText);
            _pnlSmartAdvisor.Controls.Add(_btnAdvisorApply);
            _pnlSmartAdvisor.Controls.Add(btnDismissAdvisor);

            // Software Update Notification Banner
            _pnlUpdateBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(238, 242, 255),
                Visible = false,
                Padding = new Padding(10, 6, 10, 6)
            };

            _lblUpdateText = new Label
            {
                Text = "✨ A new version of PrintPulse is available!",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(67, 56, 202),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _btnUpdateNow = CreateModernButton("Update Now", Color.FromArgb(79, 70, 229), Color.White, 0, 0, 95, 26);
            _btnUpdateNow.Dock = DockStyle.Right;
            _btnUpdateNow.Click += delegate
            {
                if (_pendingUpdate != null)
                {
                    PromptAndStartUpdate(_pendingUpdate);
                }
            };

            Button btnWhatsNew = CreateModernButton("What's New", Color.FromArgb(224, 231, 255), Color.FromArgb(67, 56, 202), 0, 0, 85, 26);
            btnWhatsNew.Dock = DockStyle.Right;
            btnWhatsNew.Click += delegate
            {
                if (_pendingUpdate != null && !string.IsNullOrEmpty(_pendingUpdate.ReleasePageUrl))
                {
                    try { Process.Start(_pendingUpdate.ReleasePageUrl); } catch { }
                }
            };

            Button btnDismissUpdate = CreateModernButton("✕", Color.FromArgb(238, 242, 255), Color.FromArgb(67, 56, 202), 0, 0, 26, 26);
            btnDismissUpdate.Dock = DockStyle.Right;
            btnDismissUpdate.FlatAppearance.BorderSize = 0;
            btnDismissUpdate.Click += delegate { _pnlUpdateBanner.Visible = false; };

            _pnlUpdateBanner.Controls.Add(_lblUpdateText);
            _pnlUpdateBanner.Controls.Add(_btnUpdateNow);
            _pnlUpdateBanner.Controls.Add(btnWhatsNew);
            _pnlUpdateBanner.Controls.Add(btnDismissUpdate);

            Panel pnlQueueHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(10, 3, 10, 3)
            };

            _lblQueueTitle = new Label
            {
                Text = "PRINT QUEUE (0 Files)",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 230,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Button btnSortMenu = CreateModernButton("🔀 Sort Queue ▾", Color.White, Color.FromArgb(51, 65, 85), 0, 0, 120, 26);
            btnSortMenu.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnSortMenu.Dock = DockStyle.Right;

            ContextMenuStrip cmsSort = new ContextMenuStrip();
            cmsSort.Font = new Font("Segoe UI", 9F);
            cmsSort.Items.Add("🔤 By File Name (A to Z)", null, delegate { SortQueue(SortMode.Name); });
            cmsSort.Items.Add("🔤 By File Name (Z to A)", null, delegate { SortQueue(SortMode.NameDesc); });
            cmsSort.Items.Add(new ToolStripSeparator());
            cmsSort.Items.Add("📄 By Page Count (Smallest First)", null, delegate { SortQueue(SortMode.PageCount); });
            cmsSort.Items.Add("📄 By Page Count (Largest First)", null, delegate { SortQueue(SortMode.PageCountDesc); });
            cmsSort.Items.Add(new ToolStripSeparator());
            cmsSort.Items.Add("🏷️ By File Type (PDF, DOCX, Images)", null, delegate { SortQueue(SortMode.FileType); });
            cmsSort.Items.Add("💾 By File Size", null, delegate { SortQueue(SortMode.FileSize); });
            cmsSort.Items.Add(new ToolStripSeparator());
            cmsSort.Items.Add("🔄 Reset to Original Order", null, delegate { SortQueue(SortMode.OriginalIndex); });

            btnSortMenu.Click += delegate { cmsSort.Show(btnSortMenu, new Point(0, btnSortMenu.Height)); };

            pnlQueueHeader.Controls.Add(btnSortMenu);
            pnlQueueHeader.Controls.Add(_lblQueueTitle);

            _lvQueue = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true,
                HideSelection = false,
                AllowDrop = true,
                ShowItemToolTips = true
            };
            _lvQueue.Columns.Add("", 28);
            _lvQueue.Columns.Add("#", 42);
            _lvQueue.Columns.Add("File Name", 190);
            _lvQueue.Columns.Add("Pages", 50);
            _lvQueue.Columns.Add("Copies", 50);
            _lvQueue.Columns.Add("Color", 60);
            _lvQueue.Columns.Add("Duplex", 75);
            _lvQueue.Columns.Add("Layout", 65);
            _lvQueue.Columns.Add("Status", 85);

            _lvQueue.SelectedIndexChanged += LvQueue_SelectedIndexChanged;
            _lvQueue.ColumnClick += LvQueue_ColumnClick;
            _lvQueue.ItemCheck += LvQueue_ItemCheck;
            _lvQueue.ItemDrag += LvQueue_ItemDrag;
            _lvQueue.MouseMove += LvQueue_MouseMove;
            _lvQueue.MouseLeave += LvQueue_MouseLeave;
            _lvQueue.DragEnter += delegate(object s, DragEventArgs e) { e.Effect = DragDropEffects.Move; };
            _lvQueue.DragOver += LvQueue_DragOver;
            _lvQueue.DragDrop += LvQueue_DragDrop;

            // Empty state overlay for initial launch / empty queue
            _pnlEmptyState = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Visible = true,
                AllowDrop = true
            };
            _pnlEmptyState.DragEnter += MainForm_DragEnter;
            _pnlEmptyState.DragDrop += MainForm_DragDrop;

            Label lblEmptyIcon = new Label
            {
                Text = "📄",
                Font = new Font("Segoe UI", 32F, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                Height = 55,
                TextAlign = ContentAlignment.BottomCenter
            };
            Label lblEmptyMain = new Label
            {
                Text = "No Documents in Print Queue",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Label lblEmptySub = new Label
            {
                Text = "Drag & drop PDF, Word DOCX, text files, or images here\nor click the button below to add documents.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Top,
                Height = 38,
                TextAlign = ContentAlignment.TopCenter
            };
            Button btnEmptyAdd = CreateModernButton("+ Add Files to Print", Color.FromArgb(79, 70, 229), Color.White, 0, 0, 160, 34);
            btnEmptyAdd.Click += delegate { AddFilesDialog(); };

            Panel pnlEmptyBtn = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.Transparent
            };
            pnlEmptyBtn.Controls.Add(btnEmptyAdd);
            pnlEmptyBtn.Resize += delegate
            {
                btnEmptyAdd.Location = new Point((pnlEmptyBtn.Width - btnEmptyAdd.Width) / 2, 4);
            };

            Panel pnlEmptyCenter = new Panel
            {
                Width = 420,
                Height = 180,
                BackColor = Color.Transparent
            };
            pnlEmptyCenter.Controls.Add(pnlEmptyBtn);
            pnlEmptyCenter.Controls.Add(lblEmptySub);
            pnlEmptyCenter.Controls.Add(lblEmptyMain);
            pnlEmptyCenter.Controls.Add(lblEmptyIcon);

            _pnlEmptyState.Controls.Add(pnlEmptyCenter);
            _pnlEmptyState.Resize += delegate
            {
                pnlEmptyCenter.Location = new Point((_pnlEmptyState.Width - pnlEmptyCenter.Width) / 2, Math.Max(20, (_pnlEmptyState.Height - pnlEmptyCenter.Height) / 2));
            };

            pnlQueue.Controls.Add(_pnlEmptyState);
            pnlQueue.Controls.Add(_lvQueue);
            pnlQueue.Controls.Add(_pnlSmartAdvisor);
            pnlQueue.Controls.Add(_pnlUpdateBanner);
            pnlQueue.Controls.Add(pnlQueueHeader);
            _split.Panel1.Controls.Add(pnlQueue);

            // RIGHT PANEL: PREVIEW & ADOBE ACROBAT STYLE SETTINGS
            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            Label lblPreviewHeader = new Label
            {
                Text = "DOCUMENT PREVIEW & PAGE HANDLING",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(10, 6, 0, 0)
            };

            FlowLayoutPanel pnlPreviewBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                WrapContents = false,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(6, 3, 6, 3)
            };

            _btnPrevPage = CreateModernButton("◄ Prev", Color.White, Color.FromArgb(51, 65, 85), 0, 0, 65, 26);
            _btnPrevPage.Click += delegate { ChangePreviewPage(-1); };

            _lblPreviewMeta = new Label
            {
                Text = "No file selected",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                Size = new Size(130, 26),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _btnNextPage = CreateModernButton("Next ►", Color.White, Color.FromArgb(51, 65, 85), 0, 0, 65, 26);
            _btnNextPage.Click += delegate { ChangePreviewPage(1); };

            _btnRotate = CreateModernButton("⟲ Rotate 90°", Color.White, Color.FromArgb(79, 70, 229), 0, 0, 95, 26);
            _btnRotate.Click += delegate { RotateCurrentDocument(); };

            pnlPreviewBar.Controls.Add(_btnPrevPage);
            pnlPreviewBar.Controls.Add(_lblPreviewMeta);
            pnlPreviewBar.Controls.Add(_btnNextPage);
            pnlPreviewBar.Controls.Add(_btnRotate);

            _pbPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(241, 245, 249),
                SizeMode = PictureBoxSizeMode.CenterImage
            };

            // Adobe Acrobat Style Settings Panel
            Panel pnlSettingsWrapper = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 295,
                BackColor = Color.FromArgb(250, 252, 255),
                Padding = new Padding(10, 6, 10, 6),
                AutoScroll = true
            };

            BuildAdobeSettingsControls(pnlSettingsWrapper);

            pnlRight.Controls.Add(_pbPreview);
            pnlRight.Controls.Add(pnlSettingsWrapper);
            pnlRight.Controls.Add(pnlPreviewBar);
            pnlRight.Controls.Add(lblPreviewHeader);
            _split.Panel2.Controls.Add(pnlRight);

            this.Controls.Add(_split);
            this.Controls.Add(bottomBar);
            this.Controls.Add(topBar);
        }

        private void BuildAdobeSettingsControls(Panel container)
        {
            TableLayoutPanel tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 5,
                Height = 250,
                BackColor = Color.Transparent
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // Section 1: Pages to Print (Left Col)
            Panel pnlPages = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            Label lblPTP = new Label { Text = "Pages to Print", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(0, 0), AutoSize = true };
            _rbPagesAll = new RadioButton { Text = "All", Location = new Point(2, 20), AutoSize = true, Checked = true };
            _rbPagesCurrent = new RadioButton { Text = "Current", Location = new Point(50, 20), AutoSize = true };
            _rbPagesCustom = new RadioButton { Text = "Pages:", Location = new Point(120, 20), AutoSize = true };

            _txtCustomPages = new TextBox { Location = new Point(180, 18), Size = new Size(70, 22), Text = "1" };
            _txtCustomPages.TextChanged += delegate { if (!_isUpdatingInputs) { _rbPagesCustom.Checked = true; UpdateSelectedSettings(); } };

            _rbPagesAll.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbPagesAll.Checked) UpdateSelectedSettings(); };
            _rbPagesCurrent.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbPagesCurrent.Checked) UpdateSelectedSettings(); };
            _rbPagesCustom.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbPagesCustom.Checked) UpdateSelectedSettings(); };

            _cbPageSubset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(2, 45), Size = new Size(130, 24), Font = new Font("Segoe UI", 8.5f) };
            _cbPageSubset.Items.AddRange(new object[] { "All in Range", "Odd Pages Only", "Even Pages Only" });
            _cbPageSubset.SelectedIndex = 0;
            _cbPageSubset.SelectedIndexChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };

            _chkReverseOrder = new CheckBox { Text = "Reverse Order", Location = new Point(145, 47), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            _chkReverseOrder.CheckedChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };

            pnlPages.Controls.Add(lblPTP);
            pnlPages.Controls.Add(_rbPagesAll);
            pnlPages.Controls.Add(_rbPagesCurrent);
            pnlPages.Controls.Add(_rbPagesCustom);
            pnlPages.Controls.Add(_txtCustomPages);
            pnlPages.Controls.Add(_cbPageSubset);
            pnlPages.Controls.Add(_chkReverseOrder);
            tlp.Controls.Add(pnlPages, 0, 0);

            // Section 2: Copies & Color (Right Col)
            Panel pnlCopiesColor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            Label lblCopies = new Label { Text = "Copies:", Location = new Point(0, 5), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            _numCopies = new NumericUpDown { Location = new Point(55, 3), Size = new Size(55, 22), Minimum = 1, Maximum = 99, Value = 1 };
            _numCopies.ValueChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };

            _cbColorMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(125, 3), Size = new Size(95, 24), Font = new Font("Segoe UI", 8.5f) };
            _cbColorMode.Items.AddRange(new object[] { "Grayscale (B&W)", "Color" });
            _cbColorMode.SelectedIndex = 0;
            _cbColorMode.SelectedIndexChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };

            _chkTonerSaver = new CheckBox { Text = "Save Ink / Toner", Location = new Point(5, 45), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            _chkTonerSaver.CheckedChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };

            pnlCopiesColor.Controls.Add(lblCopies);
            pnlCopiesColor.Controls.Add(_numCopies);
            pnlCopiesColor.Controls.Add(_cbColorMode);
            pnlCopiesColor.Controls.Add(_chkTonerSaver);
            tlp.Controls.Add(pnlCopiesColor, 1, 0);

            // Section 3: Page Sizing & Handling (Left Col)
            Panel pnlSizing = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            Label lblSize = new Label { Text = "Page Sizing & Handling", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Location = new Point(0, 0), AutoSize = true };
            _rbScaleFit = new RadioButton { Text = "Fit", Location = new Point(2, 20), AutoSize = true, Checked = true };
            _rbScaleActual = new RadioButton { Text = "Actual size", Location = new Point(50, 20), AutoSize = true };
            _rbScaleShrink = new RadioButton { Text = "Shrink oversized", Location = new Point(135, 20), AutoSize = true };

            _rbScaleFit.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbScaleFit.Checked) UpdateSelectedSettings(); };
            _rbScaleActual.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbScaleActual.Checked) UpdateSelectedSettings(); };
            _rbScaleShrink.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbScaleShrink.Checked) UpdateSelectedSettings(); };

            _rbScaleCustom = new RadioButton { Text = "Custom Scale:", Location = new Point(2, 45), AutoSize = true };
            _numCustomScale = new NumericUpDown { Location = new Point(105, 44), Size = new Size(55, 22), Minimum = 25, Maximum = 400, Value = 100 };
            _rbScaleCustom.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbScaleCustom.Checked) UpdateSelectedSettings(); };
            _numCustomScale.ValueChanged += delegate { if (!_isUpdatingInputs) { _rbScaleCustom.Checked = true; UpdateSelectedSettings(); } };

            pnlSizing.Controls.Add(lblSize);
            pnlSizing.Controls.Add(_rbScaleFit);
            pnlSizing.Controls.Add(_rbScaleActual);
            pnlSizing.Controls.Add(_rbScaleShrink);
            pnlSizing.Controls.Add(_rbScaleCustom);
            pnlSizing.Controls.Add(_numCustomScale);
            tlp.Controls.Add(pnlSizing, 0, 1);

            // Section 4: Layout & Duplex & Orientation (Right Col)
            Panel pnlLayout = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            Label lblLay = new Label { Text = "Layout:", Location = new Point(0, 4), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            _cbLayoutMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(55, 2), Size = new Size(170, 24), Font = new Font("Segoe UI", 8.5f) };
            _cbLayoutMode.Items.AddRange(new object[] { "1-Up (Single Page)", "2-Up (2 per Sheet)", "4-Up (4 per Sheet)", "Booklet (Folded Brochure)" });
            _cbLayoutMode.SelectedIndex = 0;
            _cbLayoutMode.SelectedIndexChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };

            Label lblDup = new Label { Text = "Duplex:", Location = new Point(0, 32), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            _cbDuplexMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(55, 30), Size = new Size(170, 24), Font = new Font("Segoe UI", 8.5f) };
            _cbDuplexMode.Items.AddRange(new object[] { "1-Sided (Simplex)", "2-Sided (Flip Long Edge)", "2-Sided (Flip Short Edge)" });
            _cbDuplexMode.SelectedIndex = 0;
            _cbDuplexMode.SelectedIndexChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };

            pnlLayout.Controls.Add(lblLay);
            pnlLayout.Controls.Add(_cbLayoutMode);
            pnlLayout.Controls.Add(lblDup);
            pnlLayout.Controls.Add(_cbDuplexMode);
            tlp.Controls.Add(pnlLayout, 1, 1);

            // Section 5: Orientation & Tray Selection (Row 2)
            Panel pnlOrient = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            Label lblOr = new Label { Text = "Orientation:", Location = new Point(0, 5), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            _rbOrientAuto = new RadioButton { Text = "Auto", Location = new Point(80, 4), AutoSize = true, Checked = true };
            _rbOrientPortrait = new RadioButton { Text = "Portrait", Location = new Point(135, 4), AutoSize = true };
            _rbOrientLandscape = new RadioButton { Text = "Landscape", Location = new Point(200, 4), AutoSize = true };

            _rbOrientAuto.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbOrientAuto.Checked) UpdateSelectedSettings(); };
            _rbOrientPortrait.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbOrientPortrait.Checked) UpdateSelectedSettings(); };
            _rbOrientLandscape.CheckedChanged += delegate { if (!_isUpdatingInputs && _rbOrientLandscape.Checked) UpdateSelectedSettings(); };

            pnlOrient.Controls.Add(lblOr);
            pnlOrient.Controls.Add(_rbOrientAuto);
            pnlOrient.Controls.Add(_rbOrientPortrait);
            pnlOrient.Controls.Add(_rbOrientLandscape);
            tlp.Controls.Add(pnlOrient, 0, 2);

            Panel pnlTray = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            Label lblTr = new Label { Text = "Paper Tray:", Location = new Point(0, 5), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            _cbPaperTray = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(75, 2), Size = new Size(150, 24), Font = new Font("Segoe UI", 8.5f) };
            _cbPaperTray.Items.Add("Auto / Default");
            _cbPaperTray.SelectedIndex = 0;
            _cbPaperTray.SelectedIndexChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };

            pnlTray.Controls.Add(lblTr);
            pnlTray.Controls.Add(_cbPaperTray);
            tlp.Controls.Add(pnlTray, 1, 2);

            // Section 6: Watermark & Bates Numbering (Row 3)
            Panel pnlWm = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            Label lblWm = new Label { Text = "Watermark:", Location = new Point(0, 5), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            _cbWatermark = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Location = new Point(80, 2), Size = new Size(150, 24), Font = new Font("Segoe UI", 8.5f) };
            _cbWatermark.Items.AddRange(new object[] { "None", "CONFIDENTIAL", "DRAFT", "COPY", "SAMPLE", "URGENT", "INTERNAL ONLY" });
            _cbWatermark.SelectedIndex = 0;
            _cbWatermark.TextChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };
            pnlWm.Controls.Add(lblWm);
            pnlWm.Controls.Add(_cbWatermark);
            tlp.Controls.Add(pnlWm, 0, 3);

            Panel pnlBates = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            _chkBates = new CheckBox { Text = "Page Stamp:", Location = new Point(0, 4), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            _txtBatesPrefix = new TextBox { Location = new Point(105, 3), Size = new Size(120, 22), Text = "Page ", Font = new Font("Segoe UI", 8.5f) };
            _chkBates.CheckedChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };
            _txtBatesPrefix.TextChanged += delegate { if (!_isUpdatingInputs) UpdateSelectedSettings(); };
            pnlBates.Controls.Add(_chkBates);
            pnlBates.Controls.Add(_txtBatesPrefix);
            tlp.Controls.Add(pnlBates, 1, 3);

            container.Controls.Add(tlp);

            // Apply All button at bottom of settings
            Button btnApplyAll = CreateModernButton("Apply Selected Settings to All Queued Files", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85), 10, 255, 280, 28);
            btnApplyAll.Click += delegate { ApplySettingsToAll(); };
            container.Controls.Add(btnApplyAll);
        }

        private void RestorePreviousSession()
        {
            List<PrintJobItem> restored = SessionManager.RestoreSession();
            if (restored != null && restored.Count > 0)
            {
                _queue.AddRange(restored);
                RefreshQueueListView();
                UpdateSummaryMetrics();
                _lblStatusText.Text = string.Format("Restored {0} files from previous session.", restored.Count);
                if (_queue.Count > 0)
                {
                    _lvQueue.Items[0].Selected = true;
                }
            }
        }

        private void OnStartupUpdateCheckResult(UpdateInfo info)
        {
            if (info != null && info.IsUpdateAvailable)
            {
                _pendingUpdate = info;
                _lblUpdateText.Text = string.Format("✨ PrintPulse v{0} is available! (Current: v{1})", info.LatestVersion, info.CurrentVersion);
                _pnlUpdateBanner.Visible = true;
            }
        }

        public void PromptAndStartUpdate(UpdateInfo info)
        {
            _pendingUpdate = info;
            _lblUpdateText.Text = string.Format("Downloading PrintPulse v{0}... 0%", info.LatestVersion);
            _btnUpdateNow.Enabled = false;
            _pnlUpdateBanner.Visible = true;

            UpdateManager.DownloadAndApplyUpdate(this, info,
                progress =>
                {
                    _lblUpdateText.Text = string.Format("Downloading PrintPulse v{0}... {1}%", info.LatestVersion, progress);
                },
                completeMsg =>
                {
                    _lblUpdateText.Text = "Update ready. Restarting...";
                },
                errorMsg =>
                {
                    _btnUpdateNow.Enabled = true;
                    _lblUpdateText.Text = "Update failed: " + errorMsg;
                    MessageBox.Show(errorMsg, "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            );
        }

        private void CheckSmartAdvisor(PrintJobItem item)
        {
            if (item == null || _cbPrinters.SelectedItem == null)
            {
                _pnlSmartAdvisor.Visible = false;
                return;
            }

            NativeSpooler.PrinterDevice dev = _cbPrinters.SelectedItem as NativeSpooler.PrinterDevice;
            string printerName = (dev != null) ? dev.Name : string.Empty;

            // 1. Simplex printer check
            if (!string.IsNullOrEmpty(printerName) && !NativeSpooler.CanPrinterDuplex(printerName) && item.TotalPages > 1)
            {
                _lblAdvisorText.Text = "💡 Printer is single-sided: Enable '2-Sided' to use the interactive Manual Duplex Assistant.";
                _advisorApplyAction = delegate
                {
                    _cbDuplexMode.SelectedIndex = 1; // DuplexLong
                    UpdateSelectedSettings();
                };
                _pnlSmartAdvisor.Visible = true;
                return;
            }

            // 2. High page count -> recommend 2-Up
            if (item.TotalPages >= 20 && item.LayoutMode == PageLayoutMode.OneUp)
            {
                _lblAdvisorText.Text = string.Format("💡 {0} pages detected: Enable 2-Up layout to save {1} sheets of paper.", item.TotalPages, (int)Math.Ceiling(item.TotalPages / 2.0));
                _advisorApplyAction = delegate
                {
                    _cbLayoutMode.SelectedIndex = 1; // 2-Up
                    UpdateSelectedSettings();
                };
                _pnlSmartAdvisor.Visible = true;
                return;
            }

            // 3. Document separator recommendation
            if (_queue.Count > 3 && AdvancedOptionsForm.CurrentSlipSheetMode == SlipSheetMode.None)
            {
                _lblAdvisorText.Text = "💡 Multiple files queued: Enable Slip-Sheet Separators so jobs don't mix in output tray.";
                _advisorApplyAction = delegate
                {
                    AdvancedOptionsForm.CurrentSlipSheetMode = SlipSheetMode.DetailedSlip;
                    MessageBox.Show("Document separator slips enabled!", "PrintPulse", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                _pnlSmartAdvisor.Visible = true;
                return;
            }

            _pnlSmartAdvisor.Visible = false;
        }

        private void WatchService_FileDropped(string filePath)
        {
            this.BeginInvoke(new Action(() =>
            {
                AddSingleFile(filePath);
                SessionManager.SaveSession(_queue);
                _lblStatusText.Text = "Watch Folder: Automatically added " + Path.GetFileName(filePath);
            }));
        }

        private void OpenAdvancedOptions()
        {
            AdvancedOptionsForm aof = new AdvancedOptionsForm(this);
            aof.ShowDialog(this);
        }

        public void ApplyPresetToQueue(PageScalingMode scaling, ColorPrintMode color, DuplexPrintMode duplex, PageLayoutMode layout, bool tonerSaver)
        {
            foreach (var item in _queue)
            {
                item.Scaling = scaling;
                item.ColorMode = color;
                item.DuplexMode = duplex;
                item.LayoutMode = layout;
                item.TonerSaver = tonerSaver;
            }
            if (_selectedItem != null) LoadItemPropertiesToInputs(_selectedItem);
            RefreshQueueListView();
            UpdateSummaryMetrics();
            SessionManager.SaveSession(_queue);
        }

        public void ApplyGlobalBlankPageSetting(bool skipBlank)
        {
            foreach (var item in _queue)
            {
                item.SkipBlankPages = skipBlank;
            }
            SessionManager.SaveSession(_queue);
        }

        private int _lastSortedColumn = -1;
        private bool _isAscending = true;

        private void LvQueue_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (_queue.Count <= 1) return;
            if (e.Column == 0) return; // Checkbox column

            if (_lastSortedColumn == e.Column)
            {
                _isAscending = !_isAscending;
            }
            else
            {
                _lastSortedColumn = e.Column;
                _isAscending = true;
            }

            switch (e.Column)
            {
                case 1: // # column
                    if (_isAscending)
                        _queue.Sort((a, b) => a.InitialIndex.CompareTo(b.InitialIndex));
                    else
                        _queue.Sort((a, b) => b.InitialIndex.CompareTo(a.InitialIndex));
                    break;

                case 2: // File Name
                    if (_isAscending)
                        _queue.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
                    else
                        _queue.Sort((a, b) => string.Compare(b.FileName, a.FileName, StringComparison.OrdinalIgnoreCase));
                    break;

                case 3: // Pages
                    if (_isAscending)
                        _queue.Sort((a, b) => a.TotalPages.CompareTo(b.TotalPages));
                    else
                        _queue.Sort((a, b) => b.TotalPages.CompareTo(a.TotalPages));
                    break;

                case 4: // Copies
                    if (_isAscending)
                        _queue.Sort((a, b) => a.Copies.CompareTo(b.Copies));
                    else
                        _queue.Sort((a, b) => b.Copies.CompareTo(a.Copies));
                    break;

                case 5: // Color
                    if (_isAscending)
                        _queue.Sort((a, b) => a.ColorMode.CompareTo(b.ColorMode));
                    else
                        _queue.Sort((a, b) => b.ColorMode.CompareTo(a.ColorMode));
                    break;

                case 6: // Duplex
                    if (_isAscending)
                        _queue.Sort((a, b) => a.DuplexMode.CompareTo(b.DuplexMode));
                    else
                        _queue.Sort((a, b) => b.DuplexMode.CompareTo(a.DuplexMode));
                    break;

                case 7: // Layout
                    if (_isAscending)
                        _queue.Sort((a, b) => a.LayoutMode.CompareTo(b.LayoutMode));
                    else
                        _queue.Sort((a, b) => b.LayoutMode.CompareTo(a.LayoutMode));
                    break;

                case 8: // Status
                    if (_isAscending)
                        _queue.Sort((a, b) => a.Status.CompareTo(b.Status));
                    else
                        _queue.Sort((a, b) => b.Status.CompareTo(a.Status));
                    break;
            }

            UpdateColumnHeaderSortIndicators(e.Column, _isAscending);
            RefreshQueueListView();
            SessionManager.SaveSession(_queue);
        }

        private void UpdateColumnHeaderSortIndicators(int activeCol, bool asc)
        {
            string[] baseTitles = new string[] { "", "#", "File Name", "Pages", "Copies", "Color", "Duplex", "Layout", "Status" };
            for (int i = 0; i < _lvQueue.Columns.Count && i < baseTitles.Length; i++)
            {
                if (i == activeCol)
                {
                    _lvQueue.Columns[i].Text = baseTitles[i] + (asc ? " ▲" : " ▼");
                }
                else
                {
                    _lvQueue.Columns[i].Text = baseTitles[i];
                }
            }
        }

        public void SortQueue(SortMode mode)
        {
            if (_queue.Count <= 1) return;
            switch (mode)
            {
                case SortMode.Name:
                    _queue.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
                    UpdateColumnHeaderSortIndicators(2, true);
                    break;
                case SortMode.NameDesc:
                    _queue.Sort((a, b) => string.Compare(b.FileName, a.FileName, StringComparison.OrdinalIgnoreCase));
                    UpdateColumnHeaderSortIndicators(2, false);
                    break;
                case SortMode.PageCount:
                    _queue.Sort((a, b) => a.TotalPages.CompareTo(b.TotalPages));
                    UpdateColumnHeaderSortIndicators(3, true);
                    break;
                case SortMode.PageCountDesc:
                    _queue.Sort((a, b) => b.TotalPages.CompareTo(a.TotalPages));
                    UpdateColumnHeaderSortIndicators(3, false);
                    break;
                case SortMode.FileType:
                    _queue.Sort((a, b) => string.Compare(Path.GetExtension(a.FilePath), Path.GetExtension(b.FilePath), StringComparison.OrdinalIgnoreCase));
                    UpdateColumnHeaderSortIndicators(-1, true);
                    break;
                case SortMode.FileSize:
                    _queue.Sort((a, b) => a.FileSizeBytes.CompareTo(b.FileSizeBytes));
                    UpdateColumnHeaderSortIndicators(-1, true);
                    break;
                case SortMode.OriginalIndex:
                    _queue.Sort((a, b) => a.InitialIndex.CompareTo(b.InitialIndex));
                    UpdateColumnHeaderSortIndicators(1, true);
                    break;
            }
            RefreshQueueListView();
            SessionManager.SaveSession(_queue);
        }

        public void TriggerMergedPdfExport()
        {
            if (_queue.Count == 0)
            {
                MessageBox.Show("Please add files to the queue before exporting.", "Empty Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = "PrintPulse_Merged_Batch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf",
                Title = "Export Batch as Merged PDF"
            })
            {
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    _lblStatusText.Text = "Generating consolidated PDF document...";
                    string err;
                    bool ok = DocumentRenderer.ExportBatchToPdf(_queue, sfd.FileName, out err);
                    TelemetryService.TrackFunnelStep("MergedPdf", ok ? "ExportSuccess" : "ExportFailed");
                    if (ok)
                    {
                        _lblStatusText.Text = "PDF exported successfully: " + Path.GetFileName(sfd.FileName);
                        if (MessageBox.Show("PDF export completed successfully!\n\nOpen file now?", "PrintPulse", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            try { System.Diagnostics.Process.Start(sfd.FileName); }
                            catch { }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to export PDF: " + err, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    TelemetryService.TrackFunnelStep("MergedPdf", "ExportCancelled");
                }
            }
        }

        private bool Controller_OnRequestManualDuplexFlip(string docName, int step, int totalSheets)
        {
            bool userConfirmed = false;
            this.Invoke(new Action(() =>
            {
                ManualDuplexWizard wizard = new ManualDuplexWizard(docName, step, totalSheets);
                userConfirmed = (wizard.ShowDialog(this) == DialogResult.OK);
                TelemetryService.TrackFunnelStep("ManualDuplex", userConfirmed ? "Resumed" : "Cancelled");
            }));
            return userConfirmed;
        }

        // Printer Management
        private void RefreshPrinterList()
        {
            _cbPrinters.Items.Clear();
            List<NativeSpooler.PrinterDevice> printers = NativeSpooler.GetInstalledPrinters();
            NativeSpooler.PrinterDevice defaultPrinter = null;

            foreach (var p in printers)
            {
                _cbPrinters.Items.Add(p);
                if (p.IsDefault) defaultPrinter = p;
            }

            if (defaultPrinter != null)
                _cbPrinters.SelectedItem = defaultPrinter;
            else if (_cbPrinters.Items.Count > 0)
                _cbPrinters.SelectedIndex = 0;

            UpdateSelectedPrinterStatus();
        }

        private void UpdateSelectedPrinterStatus()
        {
            NativeSpooler.PrinterDevice dev = _cbPrinters.SelectedItem as NativeSpooler.PrinterDevice;
            if (dev == null)
            {
                _lblPrinterStatus.Text = "No printer";
                _lblPrinterStatus.ForeColor = Color.FromArgb(225, 29, 72);
                return;
            }

            uint status = NativeSpooler.GetPrinterStatus(dev.Name);
            if (status == 0)
            {
                _lblPrinterStatus.Text = "● Ready";
                _lblPrinterStatus.ForeColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                _lblPrinterStatus.Text = "● " + NativeSpooler.FormatStatusFlags(status);
                _lblPrinterStatus.ForeColor = Color.FromArgb(245, 158, 11);
            }

            // Refresh available paper trays
            if (_cbPaperTray != null)
            {
                _cbPaperTray.Items.Clear();
                List<string> sources = NativeSpooler.GetPaperSources(dev.Name);
                foreach (string src in sources) _cbPaperTray.Items.Add(src);
                if (_cbPaperTray.Items.Count > 0) _cbPaperTray.SelectedIndex = 0;
            }

            CheckSmartAdvisor(_selectedItem);
        }

        // Drag & Drop & Queue Additions
        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddFilesToQueue(files);
        }

        private void AddFilesDialog()
        {
            using (OpenFileDialog ofd = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Documents to Batch Print",
                Filter = "All Supported Files|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif;*.docx;*.txt;*.csv;*.log;*.xml;*.html|PDF Files (*.pdf)|*.pdf|Image Files (*.jpg;*.png;*.bmp;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif|Office & Text Documents (*.docx;*.txt;*.csv)|*.docx;*.txt;*.csv;*.log;*.xml;*.html|All Files (*.*)|*.*"
            })
            {
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    AddFilesToQueue(ofd.FileNames);
                }
            }
        }

        private void AddFolderDialog()
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog { Description = "Select folder containing documents to batch print:" })
            {
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    string[] files = Directory.GetFiles(fbd.SelectedPath, "*.*", SearchOption.AllDirectories);
                    AddFilesToQueue(files);
                }
            }
        }

        private void AddFilesToQueue(string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0) return;

            foreach (string path in filePaths)
            {
                AddSingleFile(path);
            }

            RefreshQueueListView();
            UpdateSummaryMetrics();
            SessionManager.SaveSession(_queue);

            if (_queue.Count >= 10)
            {
                TelemetryService.TrackHighVolumeTrigger("BatchCount_Over10", _queue.Count);
            }

            if (_selectedItem == null && _queue.Count > 0)
            {
                _lvQueue.Items[_queue.Count - 1].Selected = true;
            }
        }

        private static readonly HashSet<string> SafeDocumentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".doc", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff",
            ".txt", ".csv", ".log", ".rtf", ".xml", ".html", ".htm", ".json"
        };

        private void AddSingleFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext) || !SafeDocumentExtensions.Contains(ext)) return;

            PrintJobItem item = new PrintJobItem(path);
            item.TotalPages = DocumentRenderer.InspectPageCount(item);
            item.InitialIndex = _queue.Count;
            _queue.Add(item);

            if (item.TotalPages >= 50)
            {
                TelemetryService.TrackHighVolumeTrigger("PageCount_Over50", item.TotalPages);
            }
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
                lvi.SubItems.Add("⋮⋮ " + (i + 1));
                lvi.SubItems.Add(item.FileName);
                lvi.SubItems.Add(item.TotalPages.ToString());
                lvi.SubItems.Add(item.Copies.ToString());
                lvi.SubItems.Add(item.ColorMode == ColorPrintMode.Color ? "Color" : "B&W");
                lvi.SubItems.Add(item.DuplexMode == DuplexPrintMode.Simplex ? "1-Sided" : (item.DuplexMode == DuplexPrintMode.DuplexLongEdge ? "2-Sided (Long)" : "2-Sided (Short)"));
                lvi.SubItems.Add(item.LayoutMode == PageLayoutMode.OneUp ? "1-Up" : (item.LayoutMode == PageLayoutMode.TwoUp ? "2-Up" : (item.LayoutMode == PageLayoutMode.FourUp ? "4-Up" : "Booklet")));
                lvi.SubItems.Add(item.Status.ToString());
                lvi.Tag = item;
                lvi.ToolTipText = "Click and drag to reorder print sequence";

                if (item.Status == PrintItemStatus.Printing)
                    lvi.BackColor = Color.FromArgb(238, 242, 255);
                else if (item.Status == PrintItemStatus.Completed)
                    lvi.ForeColor = Color.FromArgb(22, 101, 52);
                else if (item.Status == PrintItemStatus.Error)
                    lvi.ForeColor = Color.FromArgb(225, 29, 72);

                _lvQueue.Items.Add(lvi);
            }

            if (_selectedItem != null)
            {
                for (int i = 0; i < _lvQueue.Items.Count; i++)
                {
                    if (_lvQueue.Items[i].Tag == _selectedItem)
                    {
                        _lvQueue.Items[i].Selected = true;
                        break;
                    }
                }
            }

            _lvQueue.EndUpdate();

            if (_lblQueueTitle != null)
            {
                _lblQueueTitle.Text = string.Format("PRINT QUEUE ({0} {1})", _queue.Count, _queue.Count == 1 ? "File" : "Files");
            }

            if (_pnlEmptyState != null)
            {
                _pnlEmptyState.Visible = (_queue.Count == 0);
                if (_pnlEmptyState.Visible) _pnlEmptyState.BringToFront();
            }
        }

        private void LvQueue_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_lvQueue.SelectedItems.Count > 0)
            {
                _selectedItem = _lvQueue.SelectedItems[0].Tag as PrintJobItem;
                _currentPreviewPage = 1;
                LoadItemPropertiesToInputs(_selectedItem);
                RenderPreview();
                CheckSmartAdvisor(_selectedItem);
            }
        }

        private void LvQueue_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.Index >= 0 && e.Index < _queue.Count)
            {
                _queue[e.Index].IsEnabled = (e.NewValue == CheckState.Checked);
                this.BeginInvoke(new Action(() => {
                    UpdateSummaryMetrics();
                    SessionManager.SaveSession(_queue);
                }));
            }
        }

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
                    SessionManager.SaveSession(_queue);
                }
            }
            _draggedItemIndex = -1;
        }

        private void LvQueue_MouseMove(object sender, MouseEventArgs e)
        {
            ListViewItem item = _lvQueue.GetItemAt(e.X, e.Y);
            if (item != null)
            {
                _lvQueue.Cursor = Cursors.SizeAll;
            }
            else
            {
                _lvQueue.Cursor = Cursors.Default;
            }
        }

        private void LvQueue_MouseLeave(object sender, EventArgs e)
        {
            _lvQueue.Cursor = Cursors.Default;
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
                SessionManager.SaveSession(_queue);
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
            _pnlSmartAdvisor.Visible = false;
            SessionManager.SaveSession(_queue);
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
                _pnlSmartAdvisor.Visible = false;
                SessionManager.ClearSession();
            }
        }

        private void RenderPreview()
        {
            int targetW = _pbPreview.Width > 50 ? _pbPreview.Width - 16 : 380;
            int targetH = _pbPreview.Height > 50 ? _pbPreview.Height - 16 : 420;

            if (_pbPreview.Image != null) _pbPreview.Image.Dispose();

            if (_selectedItem == null)
            {
                Bitmap bmp = new Bitmap(targetW, targetH);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(241, 245, 249));
                    using (Pen p = new Pen(Color.FromArgb(203, 213, 225), 2))
                    {
                        p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        g.DrawRectangle(p, 25, 25, targetW - 50, targetH - 50);
                    }
                    using (Font f1 = new Font("Segoe UI", 11F, FontStyle.Bold))
                    using (Font f2 = new Font("Segoe UI", 8.5F, FontStyle.Regular))
                    using (Brush b1 = new SolidBrush(Color.FromArgb(100, 116, 139)))
                    using (Brush b2 = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    {
                        string line1 = "No Document Selected";
                        string line2 = "Select any file in the queue to preview pages\nand configure its custom print options.";
                        SizeF s1 = g.MeasureString(line1, f1);
                        SizeF s2 = g.MeasureString(line2, f2);
                        g.DrawString(line1, f1, b1, (targetW - s1.Width) / 2, (targetH / 2) - 30);
                        g.DrawString(line2, f2, b2, (targetW - s2.Width) / 2, (targetH / 2) + 5);
                    }
                }
                _pbPreview.Image = bmp;
                _lblPreviewMeta.Text = "No file selected";
                return;
            }

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
                _selectedItem.CurrentPreviewPage = newPage;
                RenderPreview();
                if (_rbPagesCurrent.Checked) UpdateSelectedSettings();
            }
        }

        private void RotateCurrentDocument()
        {
            if (_selectedItem == null) return;
            _selectedItem.Rotate90();
            if (_btnRotate != null)
            {
                _btnRotate.Text = _selectedItem.RotationDegrees > 0 ? string.Format("⟲ {0}°", _selectedItem.RotationDegrees) : "⟲ Rotate 90°";
            }
            RenderPreview();
        }

        private void LoadItemPropertiesToInputs(PrintJobItem item)
        {
            if (item == null) return;
            _isUpdatingInputs = true;

            if (_btnRotate != null)
            {
                _btnRotate.Text = item.RotationDegrees > 0 ? string.Format("⟲ {0}°", item.RotationDegrees) : "⟲ Rotate 90°";
            }

            // Copies
            _numCopies.Value = Math.Max(1, item.Copies);

            // Pages to Print
            if (item.RangeMode == PageRangeMode.CurrentPage)
                _rbPagesCurrent.Checked = true;
            else if (item.RangeMode == PageRangeMode.CustomRange)
            {
                _rbPagesCustom.Checked = true;
                _txtCustomPages.Text = item.PageRange;
            }
            else
            {
                _rbPagesAll.Checked = true;
            }

            _cbPageSubset.SelectedIndex = (int)item.Subset;
            _chkReverseOrder.Checked = item.ReverseOrder;

            // Sizing
            if (item.Scaling == PageScalingMode.ActualSize) _rbScaleActual.Checked = true;
            else if (item.Scaling == PageScalingMode.ShrinkOversized) _rbScaleShrink.Checked = true;
            else if (item.Scaling == PageScalingMode.CustomScale)
            {
                _rbScaleCustom.Checked = true;
                _numCustomScale.Value = item.CustomScalePercent > 0 ? item.CustomScalePercent : 100;
            }
            else _rbScaleFit.Checked = true;

            // Layout
            _cbLayoutMode.SelectedIndex = (int)item.LayoutMode;

            // Duplex
            _cbDuplexMode.SelectedIndex = (int)item.DuplexMode;

            // Color & Toner
            _cbColorMode.SelectedIndex = (item.ColorMode == ColorPrintMode.Color) ? 1 : 0;
            _chkTonerSaver.Checked = item.TonerSaver;

            // Orientation
            if (item.Orientation == PageOrientationMode.Portrait) _rbOrientPortrait.Checked = true;
            else if (item.Orientation == PageOrientationMode.Landscape) _rbOrientLandscape.Checked = true;
            else _rbOrientAuto.Checked = true;

            // Tray
            if (!string.IsNullOrEmpty(item.PaperSourceTray))
            {
                int idx = _cbPaperTray.FindStringExact(item.PaperSourceTray);
                _cbPaperTray.SelectedIndex = (idx >= 0) ? idx : 0;
            }

            // Watermark & Bates
            _cbWatermark.Text = string.IsNullOrEmpty(item.WatermarkText) ? "None" : item.WatermarkText;
            _chkBates.Checked = item.EnableBatesNumbering;
            _txtBatesPrefix.Text = string.IsNullOrEmpty(item.BatesPrefix) ? "Page " : item.BatesPrefix;

            _isUpdatingInputs = false;
        }

        private void UpdateSelectedSettings()
        {
            if (_selectedItem == null || _isUpdatingInputs) return;

            // Copies
            _selectedItem.Copies = (int)_numCopies.Value;

            // Range
            if (_rbPagesCurrent.Checked)
            {
                _selectedItem.RangeMode = PageRangeMode.CurrentPage;
                _selectedItem.CurrentPreviewPage = _currentPreviewPage;
            }
            else if (_rbPagesCustom.Checked)
            {
                _selectedItem.RangeMode = PageRangeMode.CustomRange;
                _selectedItem.PageRange = _txtCustomPages.Text;
            }
            else
            {
                _selectedItem.RangeMode = PageRangeMode.All;
                _selectedItem.PageRange = "All";
            }

            _selectedItem.Subset = (PageSubset)_cbPageSubset.SelectedIndex;
            _selectedItem.ReverseOrder = _chkReverseOrder.Checked;

            // Sizing
            if (_rbScaleActual.Checked) _selectedItem.Scaling = PageScalingMode.ActualSize;
            else if (_rbScaleShrink.Checked) _selectedItem.Scaling = PageScalingMode.ShrinkOversized;
            else if (_rbScaleCustom.Checked)
            {
                _selectedItem.Scaling = PageScalingMode.CustomScale;
                _selectedItem.CustomScalePercent = (int)_numCustomScale.Value;
            }
            else _selectedItem.Scaling = PageScalingMode.Fit;

            // Layout
            _selectedItem.LayoutMode = (PageLayoutMode)_cbLayoutMode.SelectedIndex;
            _selectedItem.NUp = (_selectedItem.LayoutMode == PageLayoutMode.FourUp) ? 4 : (_selectedItem.LayoutMode == PageLayoutMode.TwoUp ? 2 : 1);

            // Duplex
            _selectedItem.DuplexMode = (DuplexPrintMode)_cbDuplexMode.SelectedIndex;

            // Color & Toner
            _selectedItem.ColorMode = (_cbColorMode.SelectedIndex == 1) ? ColorPrintMode.Color : ColorPrintMode.Monochrome;
            _selectedItem.TonerSaver = _chkTonerSaver.Checked;

            // Orientation
            if (_rbOrientPortrait.Checked) _selectedItem.Orientation = PageOrientationMode.Portrait;
            else if (_rbOrientLandscape.Checked) _selectedItem.Orientation = PageOrientationMode.Landscape;
            else _selectedItem.Orientation = PageOrientationMode.Auto;

            // Tray
            if (_cbPaperTray.SelectedItem != null)
                _selectedItem.PaperSourceTray = _cbPaperTray.SelectedItem.ToString();

            // Watermark & Bates
            _selectedItem.WatermarkText = _cbWatermark.Text;
            _selectedItem.EnableBatesNumbering = _chkBates.Checked;
            _selectedItem.BatesPrefix = _txtBatesPrefix.Text;

            // Auto-update selected row in queue
            if (_lvQueue.SelectedItems.Count > 0)
            {
                ListViewItem lvi = _lvQueue.SelectedItems[0];
                lvi.SubItems[4].Text = _selectedItem.Copies.ToString();
                lvi.SubItems[5].Text = (_selectedItem.ColorMode == ColorPrintMode.Color) ? "Color" : "B&W";
                lvi.SubItems[6].Text = (_selectedItem.DuplexMode == DuplexPrintMode.Simplex) ? "1-Sided" : (_selectedItem.DuplexMode == DuplexPrintMode.DuplexLongEdge ? "2-Sided (Long)" : "2-Sided (Short)");
                lvi.SubItems[7].Text = (_selectedItem.LayoutMode == PageLayoutMode.OneUp) ? "1-Up" : (_selectedItem.LayoutMode == PageLayoutMode.TwoUp ? "2-Up" : (_selectedItem.LayoutMode == PageLayoutMode.FourUp ? "4-Up" : "Booklet"));
            }

            UpdateSummaryMetrics();
            RenderPreview();
            SessionManager.SaveSession(_queue);
        }

        private void ApplySettingsToAll()
        {
            if (_selectedItem == null) return;
            foreach (var item in _queue)
            {
                item.Copies = _selectedItem.Copies;
                item.RangeMode = _selectedItem.RangeMode;
                item.PageRange = _selectedItem.PageRange;
                item.Subset = _selectedItem.Subset;
                item.ReverseOrder = _selectedItem.ReverseOrder;
                item.Scaling = _selectedItem.Scaling;
                item.CustomScalePercent = _selectedItem.CustomScalePercent;
                item.LayoutMode = _selectedItem.LayoutMode;
                item.NUp = _selectedItem.NUp;
                item.DuplexMode = _selectedItem.DuplexMode;
                item.ColorMode = _selectedItem.ColorMode;
                item.TonerSaver = _selectedItem.TonerSaver;
                item.Orientation = _selectedItem.Orientation;
                item.PaperSourceTray = _selectedItem.PaperSourceTray;
                item.WatermarkText = _selectedItem.WatermarkText;
                item.EnableBatesNumbering = _selectedItem.EnableBatesNumbering;
                item.BatesPrefix = _selectedItem.BatesPrefix;
            }
            RefreshQueueListView();
            UpdateSummaryMetrics();
            SessionManager.SaveSession(_queue);
            MessageBox.Show("Selected settings applied to all items in queue!", "Settings Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateSummaryMetrics()
        {
            int totalFiles = 0;
            int totalPages = 0;
            int totalSheets = 0;
            int bwSheets = 0;
            int colorSheets = 0;

            foreach (var item in _queue)
            {
                if (!item.IsEnabled) continue;
                totalFiles++;
                int pages = item.ResolvePageIndices().Count * item.Copies;
                totalPages += pages;
                int sheets = item.CalculateSheetCount();
                totalSheets += sheets;
                if (item.ColorMode == ColorPrintMode.Color) colorSheets += sheets;
                else bwSheets += sheets;
            }

            int savedSheets = totalPages - totalSheets;
            string ecoTag = savedSheets > 0 ? string.Format(" • 🌱 {0} Sheets Saved", savedSheets) : "";
            double estCost = (bwSheets * CostPerBwSheet) + (colorSheets * CostPerColorSheet);

            _lblSummaryText.Text = string.Format("Active: {0} {1} | {2} Pages | {3} Sheets | Est. Cost: ${4:0.00}{5}",
                totalFiles, totalFiles == 1 ? "File" : "Files", totalPages, totalSheets, estCost, ecoTag);
        }

        private void ProcessInitialArguments(string[] args)
        {
            if (args == null || args.Length == 0) return;
            foreach (string arg in args)
            {
                if (string.IsNullOrEmpty(arg)) continue;
                try
                {
                    if (Directory.Exists(arg))
                    {
                        string[] files = Directory.GetFiles(arg, "*.*", SearchOption.AllDirectories);
                        AddFilesToQueue(files);
                    }
                    else if (File.Exists(arg))
                    {
                        AddSingleFile(arg);
                    }
                }
                catch { }
            }
            RefreshQueueListView();
            UpdateSummaryMetrics();
            if (_queue.Count > 0 && _selectedItem == null)
            {
                _lvQueue.Items[0].Selected = true;
            }
        }

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

            // Hardware Pre-flight Check
            string hwWarning;
            if (!NativeSpooler.CheckHardwarePreflight(dev.Name, out hwWarning) && !string.IsNullOrEmpty(hwWarning))
            {
                if (MessageBox.Show(hwWarning + "\n\nDo you want to proceed with printing anyway?", "Printer Hardware Pre-Check Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }
            }

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

        private void OpenFeedback()
        {
            FeedbackForm ff = new FeedbackForm();
            ff.ShowDialog(this);
        }

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
                TelemetryService.TrackErrorRecoveryChoice(item.FileName, error, page, dlg.SelectedChoice.ToString());
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

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized && _split != null)
            {
                int targetDistance = (int)(this.Width * 0.48);
                if (targetDistance > _split.Panel1MinSize && targetDistance < (_split.Width - _split.Panel2MinSize))
                {
                    _split.SplitterDistance = targetDistance;
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_controller != null && _controller.IsRunning)
            {
                if (MessageBox.Show("A batch print job is currently active.\nAre you sure you want to exit?", "Exit PrintPulse", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                _controller.RequestCancel();
            }

            if (WatchService != null)
            {
                WatchService.Dispose();
            }

            SessionManager.SaveSession(_queue);

            if (_pbPreview != null && _pbPreview.Image != null)
            {
                _pbPreview.Image.Dispose();
                _pbPreview.Image = null;
            }
        }

        private Button CreateModernButton(string text, Color bg, Color fg, int x, int y, int w, int h)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            if (bg == Color.Transparent || bg.A == 0)
            {
                btn.FlatAppearance.BorderSize = 0;
            }
            else
            {
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = (bg == Color.White || bg == Color.FromArgb(241, 245, 249)) ? Color.FromArgb(203, 213, 225) : bg;
            }
            return btn;
        }
    }
}

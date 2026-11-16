using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading;

namespace PrintPulse
{
    public enum ErrorRecoveryChoice
    {
        ReprintFromPage,
        ReprintEntireFile,
        SkipFile,
        AbortBatch
    }

    public class BatchPrintController
    {
        public event Action<PrintJobItem> OnItemStarted;
        public event Action<PrintJobItem, int, int> OnPageProgress;
        public event Action<PrintJobItem> OnItemFinished;
        public event Action<PrintJobItem, string, int, Action<ErrorRecoveryChoice, int>> OnErrorDetected;
        public event Action<BatchReport> OnBatchFinished;
        public event Action<string> OnLogMessage;

        private Thread _workerThread;
        private volatile bool _isCancelRequested;
        private volatile bool _isPaused;
        private ManualResetEvent _pauseWaitHandle = new ManualResetEvent(true);
        private AutoResetEvent _errorWaitHandle = new AutoResetEvent(false);

        private ErrorRecoveryChoice _chosenRecovery;
        private int _chosenResumePage;

        public bool IsRunning { get; private set; }
        public bool IsPaused { get { return _isPaused; } }
        public string TargetPrinterName { get; set; }
        public double PricePerBwPage { get; set; }
        public double PricePerColorPage { get; set; }

        public BatchPrintController()
        {
            PricePerBwPage = 0.05;
            PricePerColorPage = 0.25;
        }

        public void StartBatch(List<PrintJobItem> items, string printerName)
        {
            if (IsRunning) return;

            TargetPrinterName = printerName;
            _isCancelRequested = false;
            _isPaused = false;
            _pauseWaitHandle.Set();
            IsRunning = true;

            _workerThread = new Thread(() => ExecuteBatchLoop(items));
            _workerThread.IsBackground = true;
            _workerThread.Start();
        }

        public void RequestPause()
        {
            _isPaused = true;
            _pauseWaitHandle.Reset();
            Log("Queue paused by user.");
        }

        public void RequestResume()
        {
            _isPaused = false;
            _pauseWaitHandle.Set();
            Log("Queue resumed.");
        }

        public void RequestCancel()
        {
            _isCancelRequested = true;
            _pauseWaitHandle.Set();
            _errorWaitHandle.Set();
            Log("Cancel batch requested.");
        }

        private void Log(string message)
        {
            if (OnLogMessage != null)
                OnLogMessage(string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, message));
        }

        private void ExecuteBatchLoop(List<PrintJobItem> items)
        {
            DateTime batchStart = DateTime.Now;
            Log("Starting batch print session on printer: " + TargetPrinterName);

            int totalProcessed = 0;
            int totalSucceeded = 0;
            int totalFailed = 0;
            int totalSheetsUsed = 0;
            int totalPagesPrinted = 0;
            int bwPagesCount = 0;
            int colorPagesCount = 0;

            for (int i = 0; i < items.Count; i++)
            {
                if (_isCancelRequested)
                {
                    Log("Batch cancelled by user.");
                    break;
                }

                _pauseWaitHandle.WaitOne();

                PrintJobItem item = items[i];
                if (!item.IsEnabled)
                {
                    item.Status = PrintItemStatus.Skipped;
                    item.StatusMessage = "Excluded from batch";
                    continue;
                }

                totalProcessed++;
                item.Status = PrintItemStatus.Spooling;
                item.StartedAt = DateTime.Now;
                if (OnItemStarted != null) OnItemStarted(item);

                // Check printer hardware status first
                string hardwareError = CheckPrinterHardwareStatus();
                if (!string.IsNullOrEmpty(hardwareError))
                {
                    TelemetryService.TrackError("SpoolerHardwareError", hardwareError, TargetPrinterName, null);
                    Log("Hardware issue detected on " + TargetPrinterName + ": " + hardwareError);
                    HandleErrorPrompt(item, hardwareError, 1);
                    if (_chosenRecovery == ErrorRecoveryChoice.AbortBatch)
                    {
                        break;
                    }
                    else if (_chosenRecovery == ErrorRecoveryChoice.SkipFile)
                    {
                        item.Status = PrintItemStatus.Skipped;
                        item.StatusMessage = "Skipped due to printer error";
                        totalFailed++;
                        continue;
                    }
                }

                // Slip Sheet Separator
                PrintSlipSheet(item, i + 1, items.Count);

                // Execute print
                int startFromPage = 1;
                bool fileDone = false;

                while (!fileDone && !_isCancelRequested)
                {
                    item.Status = PrintItemStatus.Printing;
                    bool success = ExecuteSingleDocumentPrint(item, startFromPage);

                    if (success)
                    {
                        item.Status = PrintItemStatus.Completed;
                        item.StatusMessage = "Printed successfully";
                        item.CompletedAt = DateTime.Now;
                        totalSucceeded++;

                        int sheets = item.CalculateSheetCount();
                        totalSheetsUsed += sheets;
                        totalPagesPrinted += item.ActualPagesPrinted;
                        if (item.ColorMode == ColorPrintMode.Color)
                            colorPagesCount += item.ActualPagesPrinted;
                        else
                            bwPagesCount += item.ActualPagesPrinted;

                        fileDone = true;
                        Log(string.Format("Finished {0} ({1} sheets spooled)", item.FileName, sheets));
                    }
                    else
                    {
                        // Failure during printing
                        TelemetryService.TrackError("DocumentPrintError", item.ErrorDetails, TargetPrinterName, null);
                        Log("Error occurred while printing " + item.FileName);
                        HandleErrorPrompt(item, item.ErrorDetails ?? "Spooler / Paper Error", item.CurrentPageProcessing);

                        if (_chosenRecovery == ErrorRecoveryChoice.ReprintFromPage)
                        {
                            startFromPage = _chosenResumePage;
                            Log("Resuming " + item.FileName + " from page " + startFromPage);
                        }
                        else if (_chosenRecovery == ErrorRecoveryChoice.ReprintEntireFile)
                        {
                            startFromPage = 1;
                            Log("Restarting " + item.FileName + " from beginning.");
                        }
                        else if (_chosenRecovery == ErrorRecoveryChoice.SkipFile)
                        {
                            item.Status = PrintItemStatus.Skipped;
                            item.StatusMessage = "Skipped by user after error";
                            totalFailed++;
                            fileDone = true;
                        }
                        else if (_chosenRecovery == ErrorRecoveryChoice.AbortBatch)
                        {
                            item.Status = PrintItemStatus.Error;
                            totalFailed++;
                            _isCancelRequested = true;
                            break;
                        }
                    }
                }

                if (OnItemFinished != null) OnItemFinished(item);
            }

            IsRunning = false;
            DateTime batchEnd = DateTime.Now;

            BatchReport report = new BatchReport
            {
                SessionDate = batchStart,
                Duration = batchEnd - batchStart,
                PrinterName = TargetPrinterName,
                TotalFilesQueued = items.Count,
                TotalFilesProcessed = totalProcessed,
                SuccessfulFiles = totalSucceeded,
                FailedOrSkippedFiles = totalFailed,
                TotalPagesPrinted = totalPagesPrinted,
                BwPages = bwPagesCount,
                ColorPages = colorPagesCount,
                TotalSheetsUsed = totalSheetsUsed,
                CalculatedBill = (bwPagesCount * PricePerBwPage) + (colorPagesCount * PricePerColorPage),
                ItemsAudit = new List<PrintJobItem>(items)
            };

            Log("Batch completed. Total pages printed: " + totalPagesPrinted + " | Sheets: " + totalSheetsUsed);

            // Collect features and formats used in this batch
            List<string> activeFeatures = new List<string>();
            int pdfCount = 0, docxCount = 0, imgCount = 0, txtCount = 0;

            foreach (var itm in items)
            {
                if (itm.Type == DocumentType.Pdf) pdfCount++;
                else if (itm.Type == DocumentType.Image) imgCount++;
                else if (itm.FilePath != null && itm.FilePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) docxCount++;
                else txtCount++;

                if (itm.LayoutMode == PageLayoutMode.TwoUp && !activeFeatures.Contains("2-Up")) activeFeatures.Add("2-Up");
                if (itm.LayoutMode == PageLayoutMode.FourUp && !activeFeatures.Contains("4-Up")) activeFeatures.Add("4-Up");
                if (itm.LayoutMode == PageLayoutMode.Booklet && !activeFeatures.Contains("Booklet")) activeFeatures.Add("Booklet");
                if (itm.DuplexMode != DuplexPrintMode.Simplex && !activeFeatures.Contains("Duplex")) activeFeatures.Add("Duplex");
                if (itm.TonerSaver && !activeFeatures.Contains("TonerSaver")) activeFeatures.Add("TonerSaver");
                if (itm.RangeMode == PageRangeMode.CustomRange && !activeFeatures.Contains("CustomRange")) activeFeatures.Add("CustomRange");
            }
            if (AdvancedOptionsForm.CurrentSlipSheetMode != SlipSheetMode.None) activeFeatures.Add("SlipSheet");
            string featureSummary = activeFeatures.Count > 0 ? string.Join(", ", activeFeatures.ToArray()) : "Standard";

            TelemetryService.TrackUsageHeartbeat(totalProcessed, totalPagesPrinted, totalSheetsUsed, TargetPrinterName, featureSummary, pdfCount, docxCount, imgCount, txtCount);

            if (OnBatchFinished != null)
                OnBatchFinished(report);
        }

        private void HandleErrorPrompt(PrintJobItem item, string error, int currentFailedPage)
        {
            _chosenRecovery = ErrorRecoveryChoice.SkipFile;
            _chosenResumePage = currentFailedPage;

            if (OnErrorDetected != null)
            {
                OnErrorDetected(item, error, currentFailedPage, (choice, resumePage) =>
                {
                    _chosenRecovery = choice;
                    _chosenResumePage = resumePage;
                    _errorWaitHandle.Set();
                });

                _errorWaitHandle.WaitOne();
            }
        }

        private string CheckPrinterHardwareStatus()
        {
            if (string.IsNullOrEmpty(TargetPrinterName)) return "No printer specified";

            uint status = NativeSpooler.GetPrinterStatus(TargetPrinterName);
            if (status == 0) return null; // Clean / ready

            List<string> issues = new List<string>();
            if ((status & (uint)NativeSpooler.PrinterStatusFlags.PRINTER_STATUS_PAPER_JAM) != 0) issues.Add("Paper Jam");
            if ((status & (uint)NativeSpooler.PrinterStatusFlags.PRINTER_STATUS_PAPER_OUT) != 0) issues.Add("Out of Paper");
            if ((status & (uint)NativeSpooler.PrinterStatusFlags.PRINTER_STATUS_OFFLINE) != 0) issues.Add("Printer is Offline");
            if ((status & (uint)NativeSpooler.PrinterStatusFlags.PRINTER_STATUS_DOOR_OPEN) != 0) issues.Add("Printer Cover/Door Open");
            if ((status & (uint)NativeSpooler.PrinterStatusFlags.PRINTER_STATUS_USER_INTERVENTION) != 0) issues.Add("Manual Intervention Required");

            return issues.Count > 0 ? string.Join(", ", issues.ToArray()) : null;
        }

        public event Func<string, int, int, bool> OnRequestManualDuplexFlip;

        private void PrintSlipSheet(PrintJobItem item, int jobIndex, int totalJobs)
        {
            try
            {
                if (AdvancedOptionsForm.CurrentSlipSheetMode == SlipSheetMode.None) return;

                using (PrintDocument pd = new PrintDocument())
                {
                    pd.PrinterSettings.PrinterName = TargetPrinterName;
                    pd.DocumentName = "PrintPulse - Separator Slip - " + item.FileName;
                    pd.DefaultPageSettings.Color = false;

                    pd.PrintPage += (sender, e) =>
                    {
                        Rectangle bounds = e.MarginBounds;
                        if (AdvancedOptionsForm.CurrentSlipSheetMode == SlipSheetMode.DetailedSlip)
                        {
                            using (Bitmap slip = DocumentRenderer.RenderSlipSheet(item, jobIndex, totalJobs, bounds.Width, bounds.Height))
                            {
                                e.Graphics.DrawImage(slip, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                            }
                        }
                        else
                        {
                            // Blank page
                            e.Graphics.Clear(Color.White);
                        }
                        e.HasMorePages = false;
                    };
                    pd.Print();
                    Log("Printed separator slip for " + item.FileName);
                }
            }
            catch (Exception ex)
            {
                Log("Warning: Could not print separator slip: " + ex.Message);
            }
        }

        private bool ExecuteSingleDocumentPrint(PrintJobItem item, int startFromPage)
        {
            try
            {
                using (PrintDocument pd = new PrintDocument())
                {
                    pd.PrinterSettings.PrinterName = TargetPrinterName;
                    pd.DocumentName = "PrintPulse - " + item.FileName;

                    // Set copies
                    pd.PrinterSettings.Copies = (short)Math.Max(1, item.Copies);

                    // Set Paper Tray / Bin if configured
                    if (!string.IsNullOrEmpty(item.PaperSourceTray) && !item.PaperSourceTray.Equals("Auto / Default", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (PaperSource source in pd.PrinterSettings.PaperSources)
                        {
                            if (string.Equals(source.SourceName, item.PaperSourceTray, StringComparison.OrdinalIgnoreCase))
                            {
                                pd.DefaultPageSettings.PaperSource = source;
                                break;
                            }
                        }
                    }

                    // Set Duplex
                    bool needsManualDuplex = false;
                    if (item.DuplexMode != DuplexPrintMode.Simplex)
                    {
                        if (pd.PrinterSettings.CanDuplex)
                        {
                            if (item.DuplexMode == DuplexPrintMode.DuplexLongEdge)
                                pd.PrinterSettings.Duplex = Duplex.Vertical;
                            else if (item.DuplexMode == DuplexPrintMode.DuplexShortEdge)
                                pd.PrinterSettings.Duplex = Duplex.Horizontal;
                        }
                        else
                        {
                            needsManualDuplex = true;
                            pd.PrinterSettings.Duplex = Duplex.Simplex;
                        }
                    }
                    else
                    {
                        pd.PrinterSettings.Duplex = Duplex.Simplex;
                    }

                    // Set Color
                    if (pd.PrinterSettings.SupportsColor)
                    {
                        pd.DefaultPageSettings.Color = (item.ColorMode == ColorPrintMode.Color);
                    }

                    // Set Orientation
                    if (item.Orientation == PageOrientationMode.Landscape)
                        pd.DefaultPageSettings.Landscape = true;
                    else if (item.Orientation == PageOrientationMode.Portrait)
                        pd.DefaultPageSettings.Landscape = false;

                    List<int> pagesToPrint = item.ResolvePageIndices();
                    List<int> finalPages = new List<int>();
                    foreach (int p in pagesToPrint)
                    {
                        if (p >= startFromPage) finalPages.Add(p);
                    }

                    if (finalPages.Count == 0) return true;

                    if (needsManualDuplex && OnRequestManualDuplexFlip != null)
                    {
                        // Split into Front (Odd index) and Back (Even index) passes
                        List<int> frontPages = new List<int>();
                        List<int> backPages = new List<int>();

                        for (int idx = 0; idx < finalPages.Count; idx++)
                        {
                            if (idx % 2 == 0) frontPages.Add(finalPages[idx]);
                            else backPages.Add(finalPages[idx]);
                        }

                        // Pass 1: Front pages
                        Log(string.Format("Printing front sides ({0} sheets)...", frontPages.Count));
                        bool pass1 = PrintPageList(pd, item, frontPages);
                        if (!pass1 || _isCancelRequested) return false;

                        // Prompt user to flip stack
                        bool resume = OnRequestManualDuplexFlip(item.FileName, 1, frontPages.Count);
                        if (!resume || _isCancelRequested)
                        {
                            Log("Manual duplex cancelled by user after front pass.");
                            return false;
                        }

                        // Pass 2: Back pages
                        Log(string.Format("Printing back sides ({0} sheets)...", backPages.Count));
                        bool pass2 = PrintPageList(pd, item, backPages);
                        return pass2;
                    }
                    else
                    {
                        return PrintPageList(pd, item, finalPages);
                    }
                }
            }
            catch (Exception ex)
            {
                item.ErrorDetails = ex.Message;
                return false;
            }
        }

        private bool PrintPageList(PrintDocument pd, PrintJobItem item, List<int> pages)
        {
            int pageCursor = 0;
            pd.PrintPage += (sender, e) =>
            {
                if (_isCancelRequested)
                {
                    e.Cancel = true;
                    return;
                }

                int nup = (item.LayoutMode == PageLayoutMode.TwoUp) ? 2 : (item.LayoutMode == PageLayoutMode.FourUp ? 4 : 1);
                Rectangle marginBounds = e.MarginBounds;

                List<Bitmap> chunkBmps = new List<Bitmap>();
                for (int c = 0; c < nup && pageCursor < pages.Count; c++)
                {
                    int currentPageNumber = pages[pageCursor];
                    item.CurrentPageProcessing = currentPageNumber;

                    if (OnPageProgress != null)
                        OnPageProgress(item, currentPageNumber, item.TotalPages);

                    Bitmap pageBmp = DocumentRenderer.RenderPageThumbnail(item, currentPageNumber, marginBounds.Width, marginBounds.Height);

                    // Blank page suppression check
                    if ((item.SkipBlankPages || AdvancedOptionsForm.GlobalSkipBlankPages) && DocumentRenderer.IsPageBlank(pageBmp, 0.992f))
                    {
                        Log(string.Format("Skipped blank page {0} in {1}", currentPageNumber, item.FileName));
                        pageBmp.Dispose();
                        pageCursor++;
                        c--; // Retry slot
                        continue;
                    }

                    // Apply toner saver or grayscale if needed
                    Bitmap finalBmp = DocumentRenderer.ApplyRotatedAndFiltered(pageBmp, item.RotationDegrees, item.ColorMode == ColorPrintMode.Monochrome, item.TonerSaver);
                    pageBmp.Dispose();
                    chunkBmps.Add(finalBmp);

                    item.ActualPagesPrinted++;
                    pageCursor++;
                }

                if (chunkBmps.Count > 0)
                {
                    if (item.LayoutMode == PageLayoutMode.TwoUp || item.LayoutMode == PageLayoutMode.FourUp)
                    {
                        DocumentRenderer.DrawNUp(e.Graphics, marginBounds, chunkBmps, item.LayoutMode, true);
                    }
                    else
                    {
                        e.Graphics.DrawImage(chunkBmps[0], marginBounds.Left, marginBounds.Top, marginBounds.Width, marginBounds.Height);
                    }

                    foreach (Bitmap b in chunkBmps) b.Dispose();
                }

                e.HasMorePages = (pageCursor < pages.Count);
            };

            pd.Print();
            return true;
        }
    }
}

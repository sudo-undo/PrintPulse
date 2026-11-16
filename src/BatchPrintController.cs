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

                    // Set Duplex
                    if (pd.PrinterSettings.CanDuplex)
                    {
                        if (item.DuplexMode == DuplexPrintMode.DuplexLongEdge)
                            pd.PrinterSettings.Duplex = Duplex.Vertical;
                        else if (item.DuplexMode == DuplexPrintMode.DuplexShortEdge)
                            pd.PrinterSettings.Duplex = Duplex.Horizontal;
                        else
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
                    // Filter to startFromPage onwards
                    List<int> finalPages = new List<int>();
                    foreach (int p in pagesToPrint)
                    {
                        if (p >= startFromPage) finalPages.Add(p);
                    }

                    if (finalPages.Count == 0) return true;

                    int pageCursor = 0;
                    item.ActualPagesPrinted = 0;

                    pd.PrintPage += (sender, e) =>
                    {
                        if (_isCancelRequested)
                        {
                            e.Cancel = true;
                            return;
                        }

                        int currentPageNumber = finalPages[pageCursor];
                        item.CurrentPageProcessing = currentPageNumber;

                        if (OnPageProgress != null)
                            OnPageProgress(item, currentPageNumber, item.TotalPages);

                        // Render the page onto the printer graphics context
                        Rectangle marginBounds = e.MarginBounds;
                        using (Bitmap pageBmp = DocumentRenderer.RenderPageThumbnail(item, currentPageNumber, marginBounds.Width, marginBounds.Height))
                        {
                            e.Graphics.DrawImage(pageBmp, marginBounds.Left, marginBounds.Top, marginBounds.Width, marginBounds.Height);
                        }

                        item.ActualPagesPrinted++;
                        pageCursor++;

                        e.HasMorePages = (pageCursor < finalPages.Count);
                    };

                    pd.Print();
                    return true;
                }
            }
            catch (Exception ex)
            {
                item.ErrorDetails = ex.Message;
                return false;
            }
        }
    }
}

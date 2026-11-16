using System;
using System.Collections.Generic;
using System.IO;

namespace PrintPulse
{
    public enum DocumentType
    {
        Pdf,
        Image,
        Text,
        Unknown
    }

    public enum PrintItemStatus
    {
        Queued,
        Spooling,
        Printing,
        Completed,
        Error,
        Paused,
        Skipped
    }

    public enum ColorPrintMode
    {
        Monochrome,
        Color
    }

    public enum DuplexPrintMode
    {
        Simplex,
        DuplexLongEdge,
        DuplexShortEdge
    }

    public enum PageOrientationMode
    {
        Auto,
        Portrait,
        Landscape
    }

    public class PrintJobItem
    {
        public string Id { get; private set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DocumentType Type { get; set; }
        public long FileSizeBytes { get; set; }
        public int TotalPages { get; set; }
        public bool IsEnabled { get; set; }

        // Print settings
        public string PageRange { get; set; } // "All" or e.g. "1-4, 7"
        public int Copies { get; set; }
        public ColorPrintMode ColorMode { get; set; }
        public DuplexPrintMode DuplexMode { get; set; }
        public PageOrientationMode Orientation { get; set; }
        public int NUp { get; set; } // 1, 2, 4
        public int RotationDegrees { get; set; } // 0, 90, 180, 270

        // Live execution status
        public PrintItemStatus Status { get; set; }
        public int CurrentPageProcessing { get; set; }
        public string StatusMessage { get; set; }
        public string ErrorDetails { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int ActualPagesPrinted { get; set; }

        public PrintJobItem(string filePath)
        {
            Id = Guid.NewGuid().ToString("N");
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            IsEnabled = true;
            Status = PrintItemStatus.Queued;
            StatusMessage = "Ready in queue";
            PageRange = "All";
            Copies = 1;
            ColorMode = ColorPrintMode.Monochrome;
            DuplexMode = DuplexPrintMode.Simplex;
            Orientation = PageOrientationMode.Auto;
            NUp = 1;
            RotationDegrees = 0;
            TotalPages = 1;

            if (File.Exists(filePath))
            {
                FileInfo fi = new FileInfo(filePath);
                FileSizeBytes = fi.Length;
                string ext = fi.Extension.ToLowerInvariant();

                if (ext == ".pdf")
                    Type = DocumentType.Pdf;
                else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".tiff" || ext == ".tif" || ext == ".gif")
                    Type = DocumentType.Image;
                else if (ext == ".txt" || ext == ".csv" || ext == ".log" || ext == ".rtf" || ext == ".ini")
                    Type = DocumentType.Text;
                else
                    Type = DocumentType.Unknown;
            }
        }

        public string GetFileSizeFormatted()
        {
            if (FileSizeBytes < 1024) return FileSizeBytes + " B";
            if (FileSizeBytes < 1024 * 1024) return (FileSizeBytes / 1024.0).ToString("0.0") + " KB";
            return (FileSizeBytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
        }

        public List<int> ResolvePageIndices()
        {
            List<int> pages = new List<int>();
            if (string.IsNullOrEmpty(PageRange) || PageRange.Trim().Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i <= Math.Max(1, TotalPages); i++)
                    pages.Add(i);
                return pages;
            }

            string[] tokens = PageRange.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string t in tokens)
            {
                string token = t.Trim();
                if (token.Contains("-"))
                {
                    string[] parts = token.Split('-');
                    int start, end;
                    if (int.TryParse(parts[0].Trim(), out start) && int.TryParse(parts[1].Trim(), out end))
                    {
                        for (int p = Math.Max(1, start); p <= Math.Min(TotalPages, end); p++)
                        {
                            if (!pages.Contains(p)) pages.Add(p);
                        }
                    }
                }
                else
                {
                    int single;
                    if (int.TryParse(token, out single))
                    {
                        if (single >= 1 && single <= TotalPages && !pages.Contains(single))
                            pages.Add(single);
                    }
                }
            }

            if (pages.Count == 0)
            {
                for (int i = 1; i <= Math.Max(1, TotalPages); i++)
                    pages.Add(i);
            }
            return pages;
        }

        public int CalculateSheetCount()
        {
            List<int> pages = ResolvePageIndices();
            int pagesPerCopy = pages.Count;
            int effectivePages = (int)Math.Ceiling(pagesPerCopy / (double)Math.Max(1, NUp));

            int sheetsPerCopy = effectivePages;
            if (DuplexMode != DuplexPrintMode.Simplex)
            {
                sheetsPerCopy = (int)Math.Ceiling(effectivePages / 2.0);
            }

            return sheetsPerCopy * Math.Max(1, Copies);
        }

        public void Rotate90()
        {
            RotationDegrees = (RotationDegrees + 90) % 360;
        }
    }
}

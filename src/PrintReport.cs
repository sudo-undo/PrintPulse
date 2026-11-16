using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PrintPulse
{
    public class BatchReport
    {
        public DateTime SessionDate { get; set; }
        public TimeSpan Duration { get; set; }
        public string PrinterName { get; set; }
        public int TotalFilesQueued { get; set; }
        public int TotalFilesProcessed { get; set; }
        public int SuccessfulFiles { get; set; }
        public int FailedOrSkippedFiles { get; set; }
        public int TotalPagesPrinted { get; set; }
        public int BwPages { get; set; }
        public int ColorPages { get; set; }
        public int TotalSheetsUsed { get; set; }
        public double CalculatedBill { get; set; }
        public List<PrintJobItem> ItemsAudit { get; set; }

        public BatchReport()
        {
            ItemsAudit = new List<PrintJobItem>();
        }

        public string GenerateTextSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("========================================================================");
            sb.AppendLine("                   PRINTPULSE BATCH PRINT REPORT                        ");
            sb.AppendLine("========================================================================");
            sb.AppendLine("Date & Time:      " + SessionDate.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Duration:         " + Duration.ToString(@"mm\:ss"));
            sb.AppendLine("Target Printer:   " + PrinterName);
            sb.AppendLine("------------------------------------------------------------------------");
            sb.AppendLine(string.Format("Files Processed:  {0} of {1} (Success: {2}, Issues/Skipped: {3})",
                TotalFilesProcessed, TotalFilesQueued, SuccessfulFiles, FailedOrSkippedFiles));
            sb.AppendLine("Total Pages:      " + TotalPagesPrinted + " (B&W: " + BwPages + ", Color: " + ColorPages + ")");
            sb.AppendLine("Sheets of Paper:  " + TotalSheetsUsed + " sheets");
            sb.AppendLine("Calculated Total: $" + CalculatedBill.ToString("0.00"));
            sb.AppendLine("========================================================================");
            sb.AppendLine("ITEM AUDIT TRAIL:");
            sb.AppendLine(string.Format("{0,-4} | {1,-28} | {2,-6} | {3,-10} | {4,-12}", "#", "File Name", "Pages", "Status", "Notes"));
            sb.AppendLine("------------------------------------------------------------------------");

            int idx = 1;
            foreach (var item in ItemsAudit)
            {
                string name = item.FileName.Length > 28 ? item.FileName.Substring(0, 25) + "..." : item.FileName;
                string notes = !string.IsNullOrEmpty(item.ErrorDetails) ? item.ErrorDetails : (item.StatusMessage ?? "-");
                if (notes.Length > 18) notes = notes.Substring(0, 15) + "...";
                sb.AppendLine(string.Format("{0,-4} | {1,-28} | {2,-6} | {3,-10} | {4,-12}",
                    idx++, name, item.ActualPagesPrinted, item.Status, notes));
            }
            sb.AppendLine("========================================================================");
            return sb.ToString();
        }

        public void ExportCsv(string destinationPath)
        {
            using (StreamWriter sw = new StreamWriter(destinationPath, false, Encoding.UTF8))
            {
                sw.WriteLine("Index,FileName,FileType,TotalPages,PagesPrinted,Copies,ColorMode,DuplexMode,Status,Notes");
                int idx = 1;
                foreach (var item in ItemsAudit)
                {
                    string notes = !string.IsNullOrEmpty(item.ErrorDetails) ? item.ErrorDetails : item.StatusMessage;
                    sw.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\"",
                        idx++,
                        item.FileName.Replace("\"", "\"\""),
                        item.Type,
                        item.TotalPages,
                        item.ActualPagesPrinted,
                        item.Copies,
                        item.ColorMode,
                        item.DuplexMode,
                        item.Status,
                        (notes ?? "").Replace("\"", "\"\"")));
                }
            }
        }
    }
}

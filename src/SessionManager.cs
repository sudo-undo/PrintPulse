using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PrintPulse
{
    public static class SessionManager
    {
        private static string GetSessionFilePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrintPulse");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "session_restore.dat");
        }

        public static void SaveSession(List<PrintJobItem> items)
        {
            try
            {
                string path = GetSessionFilePath();
                if (items == null || items.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }

                StringBuilder sb = new StringBuilder();
                foreach (PrintJobItem item in items)
                {
                    if (File.Exists(item.FilePath))
                    {
                        // FilePath|Copies|ColorMode|DuplexMode|RangeMode|PageRange|Subset|ReverseOrder|Scaling|LayoutMode|PaperSourceTray|SkipBlankPages|TonerSaver
                        sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}",
                            item.FilePath,
                            item.Copies,
                            (int)item.ColorMode,
                            (int)item.DuplexMode,
                            (int)item.RangeMode,
                            item.PageRange,
                            (int)item.Subset,
                            item.ReverseOrder ? 1 : 0,
                            (int)item.Scaling,
                            (int)item.LayoutMode,
                            item.PaperSourceTray ?? "Auto",
                            item.SkipBlankPages ? 1 : 0,
                            item.TonerSaver ? 1 : 0
                        ));
                    }
                }
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static List<PrintJobItem> RestoreSession()
        {
            List<PrintJobItem> items = new List<PrintJobItem>();
            try
            {
                string path = GetSessionFilePath();
                if (!File.Exists(path)) return items;

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line.Trim())) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length > 0 && File.Exists(parts[0]))
                    {
                        PrintJobItem item = new PrintJobItem(parts[0]);
                        if (parts.Length > 1) { int c; if (int.TryParse(parts[1], out c)) item.Copies = c; }
                        if (parts.Length > 2) { int cm; if (int.TryParse(parts[2], out cm)) item.ColorMode = (ColorPrintMode)cm; }
                        if (parts.Length > 3) { int dm; if (int.TryParse(parts[3], out dm)) item.DuplexMode = (DuplexPrintMode)dm; }
                        if (parts.Length > 4) { int rm; if (int.TryParse(parts[4], out rm)) item.RangeMode = (PageRangeMode)rm; }
                        if (parts.Length > 5) item.PageRange = parts[5];
                        if (parts.Length > 6) { int sm; if (int.TryParse(parts[6], out sm)) item.Subset = (PageSubset)sm; }
                        if (parts.Length > 7) item.ReverseOrder = parts[7] == "1";
                        if (parts.Length > 8) { int sc; if (int.TryParse(parts[8], out sc)) item.Scaling = (PageScalingMode)sc; }
                        if (parts.Length > 9) { int lm; if (int.TryParse(parts[9], out lm)) item.LayoutMode = (PageLayoutMode)lm; }
                        if (parts.Length > 10) item.PaperSourceTray = parts[10];
                        if (parts.Length > 11) item.SkipBlankPages = parts[11] == "1";
                        if (parts.Length > 12) item.TonerSaver = parts[12] == "1";

                        items.Add(item);
                    }
                }
            }
            catch { }
            return items;
        }

        public static void ClearSession()
        {
            try
            {
                string path = GetSessionFilePath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace PrintPulse
{
    public static class DocumentRenderer
    {
        public static int InspectPageCount(PrintJobItem item)
        {
            if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath)) return 1;

            try
            {
                if (item.Type == DocumentType.Image)
                {
                    using (FileStream fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (Image img = Image.FromStream(fs, false, false))
                    {
                        FrameDimension dimension = new FrameDimension(img.FrameDimensionsList[0]);
                        return Math.Max(1, img.GetFrameCount(dimension));
                    }
                }
                else if (item.Type == DocumentType.Pdf)
                {
                    return ParsePdfPageCount(item.FilePath);
                }
                else
                {
                    int lineCount = 0;
                    string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
                    if (ext == ".docx")
                    {
                        List<string> docxLines = ExtractDocxLines(item.FilePath, 2000);
                        lineCount = docxLines.Count;
                    }
                    else
                    {
                        using (FileStream fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader sr = new StreamReader(fs, Encoding.Default))
                        {
                            while (sr.ReadLine() != null && lineCount < 100000)
                            {
                                lineCount++;
                            }
                        }
                    }
                    int linesPerPage = 50;
                    int pages = (int)Math.Ceiling(Math.Max(1, lineCount) / (double)linesPerPage);
                    return Math.Max(1, pages);
                }
            }
            catch
            {
                return 1;
            }
        }

        public static int ParsePdfPageCount(string pdfPath)
        {
            try
            {
                using (FileStream fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs, Encoding.ASCII))
                {
                    string content = sr.ReadToEnd();
                    Match match = Regex.Match(content, @"/Type\s*/Pages\b[^>]*?/Count\s+(\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        int count;
                        if (int.TryParse(match.Groups[1].Value, out count) && count > 0)
                            return Math.Min(10000, count);
                    }

                    MatchCollection matches = Regex.Matches(content, @"/Type\s*/Page\b", RegexOptions.IgnoreCase);
                    if (matches.Count > 0)
                        return Math.Min(10000, matches.Count);
                }
            }
            catch { }
            return 1;
        }

        public static Bitmap RenderPageThumbnail(PrintJobItem item, int pageNumber, int targetWidth, int targetHeight)
        {
            int w = Math.Max(120, targetWidth);
            int h = Math.Max(140, targetHeight);
            Bitmap bmp = new Bitmap(w, h);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(241, 245, 249));
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
                {
                    DrawPlaceholder(g, "File not found", w, h);
                    return bmp;
                }

                try
                {
                    if (item.Type == DocumentType.Image)
                    {
                        using (FileStream fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (Image src = Image.FromStream(fs, false, false))
                        {
                            FrameDimension dimension = new FrameDimension(src.FrameDimensionsList[0]);
                            int frameCount = src.GetFrameCount(dimension);
                            int frameIndex = Math.Min(Math.Max(0, pageNumber - 1), frameCount - 1);
                            if (frameIndex > 0)
                                src.SelectActiveFrame(dimension, frameIndex);

                            using (Bitmap transformed = ApplyRotatedAndFiltered(src, item.RotationDegrees, item.ColorMode == ColorPrintMode.Monochrome))
                            {
                                DrawImageFitted(g, transformed, w, h);
                            }
                        }
                    }
                    else if (item.Type == DocumentType.Pdf)
                    {
                        DrawRealPdfPreview(g, item, pageNumber, w, h);
                    }
                    else
                    {
                        DrawDocumentTextPreview(g, item, pageNumber, w, h);
                    }
                }
                catch (Exception ex)
                {
                    DrawPlaceholder(g, "Preview error:\n" + ex.Message, w, h);
                }
            }
            return bmp;
        }

        private static void DrawRealPdfPreview(Graphics g, PrintJobItem item, int pageNumber, int w, int h)
        {
            int paperMargin = 12;
            Rectangle paperRect = new Rectangle(paperMargin, paperMargin, w - (paperMargin * 2), h - (paperMargin * 2));

            // Drop shadow
            using (Brush shadowBrush = new SolidBrush(Color.FromArgb(215, 222, 232)))
            {
                g.FillRectangle(shadowBrush, paperRect.X + 3, paperRect.Y + 3, paperRect.Width, paperRect.Height);
            }

            using (Brush paperBrush = new SolidBrush(Color.White))
            using (Pen borderPen = new Pen(Color.FromArgb(203, 213, 225), 1))
            {
                g.FillRectangle(paperBrush, paperRect);
                g.DrawRectangle(borderPen, paperRect);
            }

            // Check if PDF has embedded scanned image for this page
            Bitmap embeddedImage = ExtractPdfEmbeddedImage(item.FilePath, pageNumber);
            if (embeddedImage != null)
            {
                using (embeddedImage)
                using (Bitmap filtered = ApplyRotatedAndFiltered(embeddedImage, item.RotationDegrees, item.ColorMode == ColorPrintMode.Monochrome))
                {
                    DrawImageFittedInRect(g, filtered, paperRect);
                }
                return;
            }

            // Otherwise, extract real text, title and structure
            string pdfTitle = "";
            string pdfAuthor = "";
            List<string> extractedLines = ExtractPdfTextLines(item.FilePath, out pdfTitle, out pdfAuthor);

            if (string.IsNullOrEmpty(pdfTitle))
                pdfTitle = Path.GetFileNameWithoutExtension(item.FileName);

            // Header Banner on paper
            using (Brush headerBg = new SolidBrush(Color.FromArgb(248, 250, 252)))
            using (Pen headerLine = new Pen(Color.FromArgb(226, 232, 240), 1))
            {
                g.FillRectangle(headerBg, paperRect.X + 1, paperRect.Y + 1, paperRect.Width - 2, 42);
                g.DrawLine(headerLine, paperRect.X, paperRect.Y + 43, paperRect.Right, paperRect.Y + 43);
            }

            using (Font titleFont = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (Font metaFont = new Font("Segoe UI", 7.5F, FontStyle.Regular))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
            using (Brush metaBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            using (Brush badgeBg = new SolidBrush(Color.FromArgb(238, 242, 255)))
            using (Brush badgeFg = new SolidBrush(Color.FromArgb(79, 70, 229)))
            {
                g.FillRectangle(badgeBg, paperRect.X + 8, paperRect.Y + 8, 32, 16);
                g.DrawString("PDF", new Font("Segoe UI", 7F, FontStyle.Bold), badgeFg, paperRect.X + 12, paperRect.Y + 9);

                string displayTitle = pdfTitle.Length > 35 ? pdfTitle.Substring(0, 32) + "..." : pdfTitle;
                g.DrawString(displayTitle, titleFont, titleBrush, paperRect.X + 46, paperRect.Y + 7);

                string metaInfo = string.Format("Page {0} of {1}   |   {2}", pageNumber, item.TotalPages, item.GetFileSizeFormatted());
                g.DrawString(metaInfo, metaFont, metaBrush, paperRect.X + 46, paperRect.Y + 24);
            }

            // Body text rendering from actual extracted document content
            int textX = paperRect.X + 16;
            int textY = paperRect.Y + 54;
            int maxBottom = paperRect.Bottom - 30;

            using (Font bodyFont = new Font("Segoe UI", 8.25F, FontStyle.Regular))
            using (Brush textBrush = new SolidBrush(item.ColorMode == ColorPrintMode.Monochrome ? Color.Black : Color.FromArgb(51, 65, 85)))
            {
                if (extractedLines.Count > 0)
                {
                    int linesPerPage = 28;
                    int startIdx = Math.Max(0, (pageNumber - 1) * linesPerPage);

                    for (int i = startIdx; i < Math.Min(extractedLines.Count, startIdx + linesPerPage); i++)
                    {
                        string line = extractedLines[i];
                        if (line.Length > 65) line = line.Substring(0, 62) + "...";
                        g.DrawString(line, bodyFont, textBrush, textX, textY);
                        textY += 15;
                        if (textY > maxBottom) break;
                    }
                }
                else
                {
                    g.DrawString("Document Preview", new Font("Segoe UI", 10F, FontStyle.Bold), textBrush, textX, textY);
                    textY += 24;
                    g.DrawString("File: " + item.FileName, bodyFont, textBrush, textX, textY);
                    textY += 18;
                    g.DrawString("Pages: " + item.TotalPages + " pages", bodyFont, textBrush, textX, textY);
                    textY += 28;

                    using (Brush barBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
                    {
                        for (int r = 0; r < 14; r++)
                        {
                            int lineW = (r % 3 == 0) ? (paperRect.Width - 90) : (paperRect.Width - 45);
                            g.FillRectangle(barBrush, textX, textY, lineW, 6);
                            textY += 14;
                            if (textY > maxBottom) break;
                        }
                    }
                }
            }

            // Footer
            using (Font footerFont = new Font("Segoe UI", 7.5F, FontStyle.Regular))
            using (Brush footerBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            using (Pen footerLine = new Pen(Color.FromArgb(241, 245, 249), 1))
            {
                g.DrawLine(footerLine, paperRect.X + 8, paperRect.Bottom - 22, paperRect.Right - 8, paperRect.Bottom - 22);
                string footerText = string.Format("- Page {0} -", pageNumber);
                SizeF sz = g.MeasureString(footerText, footerFont);
                g.DrawString(footerText, footerFont, footerBrush, paperRect.X + (paperRect.Width - sz.Width) / 2, paperRect.Bottom - 18);
            }
        }

        private static void DrawDocumentTextPreview(Graphics g, PrintJobItem item, int pageNumber, int w, int h)
        {
            int paperMargin = 12;
            Rectangle paperRect = new Rectangle(paperMargin, paperMargin, w - (paperMargin * 2), h - (paperMargin * 2));

            using (Brush shadowBrush = new SolidBrush(Color.FromArgb(215, 222, 232)))
            using (Brush paperBrush = new SolidBrush(Color.White))
            using (Pen borderPen = new Pen(Color.FromArgb(203, 213, 225), 1))
            {
                g.FillRectangle(shadowBrush, paperRect.X + 3, paperRect.Y + 3, paperRect.Width, paperRect.Height);
                g.FillRectangle(paperBrush, paperRect);
                g.DrawRectangle(borderPen, paperRect);
            }

            string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
            List<string> lines;
            if (ext == ".docx")
            {
                lines = ExtractDocxLines(item.FilePath, 1000);
            }
            else
            {
                lines = new List<string>();
                using (FileStream fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs, Encoding.Default))
                {
                    string l;
                    int count = 0;
                    while ((l = sr.ReadLine()) != null && count < 1000)
                    {
                        lines.Add(l);
                        count++;
                    }
                }
            }

            // Top Header
            using (Brush headerBg = new SolidBrush(Color.FromArgb(248, 250, 252)))
            using (Pen headerLine = new Pen(Color.FromArgb(226, 232, 240), 1))
            {
                g.FillRectangle(headerBg, paperRect.X + 1, paperRect.Y + 1, paperRect.Width - 2, 38);
                g.DrawLine(headerLine, paperRect.X, paperRect.Y + 39, paperRect.Right, paperRect.Y + 39);
            }

            using (Font titleFont = new Font("Segoe UI", 8.5F, FontStyle.Bold))
            using (Font metaFont = new Font("Segoe UI", 7.5F, FontStyle.Regular))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (Brush metaBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                string headerTitle = item.FileName.Length > 30 ? item.FileName.Substring(0, 28) + "..." : item.FileName;
                g.DrawString(headerTitle, titleFont, titleBrush, paperRect.X + 12, paperRect.Y + 6);
                g.DrawString("Document Preview - Page " + pageNumber + " of " + item.TotalPages, metaFont, metaBrush, paperRect.X + 12, paperRect.Y + 22);
            }

            // Body Lines with line numbers
            int startLine = Math.Max(0, (pageNumber - 1) * 35);
            int endLine = Math.Min(lines.Count, startLine + 35);
            float y = paperRect.Y + 48;

            using (Font monoFont = new Font("Consolas", 8F, FontStyle.Regular))
            using (Font numFont = new Font("Consolas", 7.5F, FontStyle.Regular))
            using (Brush numBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (Pen numDivider = new Pen(Color.FromArgb(241, 245, 249), 1))
            {
                g.DrawLine(numDivider, paperRect.X + 38, paperRect.Y + 40, paperRect.X + 38, paperRect.Bottom - 10);

                for (int i = startLine; i < endLine; i++)
                {
                    string numStr = (i + 1).ToString().PadLeft(3);
                    g.DrawString(numStr, numFont, numBrush, paperRect.X + 8, y);

                    string content = lines[i];
                    if (content.Length > 70) content = content.Substring(0, 67) + "...";
                    g.DrawString(content, monoFont, textBrush, paperRect.X + 44, y);

                    y += 14;
                    if (y > paperRect.Bottom - 20) break;
                }
            }
        }

        private static List<string> ExtractDocxLines(string filePath, int maxLines)
        {
            List<string> lines = new List<string>();
            try
            {
                byte[] xmlBytes = ReadZipEntry(filePath, "word/document.xml");
                if (xmlBytes != null && xmlBytes.Length > 0)
                {
                    string xml = Encoding.UTF8.GetString(xmlBytes);
                    MatchCollection pMatches = Regex.Matches(xml, @"<w:p\b[^>]*>(.*?)</w:p>", RegexOptions.Singleline);
                    foreach (Match p in pMatches)
                    {
                        MatchCollection tMatches = Regex.Matches(p.Value, @"<w:t\b[^>]*>(.*?)</w:t>");
                        StringBuilder sb = new StringBuilder();
                        foreach (Match t in tMatches)
                        {
                            sb.Append(t.Groups[1].Value);
                        }
                        string clean = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(clean))
                        {
                            lines.Add(clean);
                            if (lines.Count >= maxLines) break;
                        }
                    }
                }
            }
            catch { }
            return lines;
        }

        private static byte[] ReadZipEntry(string zipPath, string entryName)
        {
            using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader br = new BinaryReader(fs))
            {
                long length = fs.Length;
                if (length < 22) return null;

                long maxSearch = Math.Min(length, 65536 + 22);
                long searchStart = length - maxSearch;
                fs.Seek(searchStart, SeekOrigin.Begin);
                byte[] buf = br.ReadBytes((int)maxSearch);

                int eocdOffset = -1;
                for (int i = buf.Length - 22; i >= 0; i--)
                {
                    if (buf[i] == 0x50 && buf[i + 1] == 0x4B && buf[i + 2] == 0x05 && buf[i + 3] == 0x06)
                    {
                        eocdOffset = i;
                        break;
                    }
                }

                if (eocdOffset < 0) return null;

                int cdCount = BitConverter.ToUInt16(buf, eocdOffset + 8);
                int cdStart = (int)BitConverter.ToUInt32(buf, eocdOffset + 16);

                fs.Seek(cdStart, SeekOrigin.Begin);

                for (int entryIdx = 0; entryIdx < cdCount; entryIdx++)
                {
                    if (fs.Position + 46 > length) break;
                    uint sig = br.ReadUInt32();
                    if (sig != 0x02014b50) break;

                    fs.Seek(6, SeekOrigin.Current);
                    ushort method = br.ReadUInt16();
                    fs.Seek(8, SeekOrigin.Current);
                    uint compSize = br.ReadUInt32();
                    uint uncompSize = br.ReadUInt32();
                    ushort nameLen = br.ReadUInt16();
                    ushort extraLen = br.ReadUInt16();
                    ushort commentLen = br.ReadUInt16();
                    fs.Seek(8, SeekOrigin.Current);
                    uint localHeaderOffset = br.ReadUInt32();

                    byte[] nameBytes = br.ReadBytes(nameLen);
                    string name = Encoding.UTF8.GetString(nameBytes);
                    fs.Seek(extraLen + commentLen, SeekOrigin.Current);

                    if (string.Equals(name, entryName, StringComparison.OrdinalIgnoreCase))
                    {
                        fs.Seek(localHeaderOffset, SeekOrigin.Begin);
                        uint localSig = br.ReadUInt32();
                        if (localSig != 0x04034b50) return null;

                        fs.Seek(22, SeekOrigin.Current);
                        ushort localNameLen = br.ReadUInt16();
                        ushort localExtraLen = br.ReadUInt16();
                        fs.Seek(localNameLen + localExtraLen, SeekOrigin.Current);

                        if (method == 0)
                        {
                            return br.ReadBytes((int)compSize);
                        }
                        else if (method == 8)
                        {
                            using (DeflateStream ds = new DeflateStream(fs, CompressionMode.Decompress, true))
                            {
                                MemoryStream ms = new MemoryStream((int)uncompSize);
                                byte[] temp = new byte[8192];
                                int read;
                                long totalRead = 0;
                                while (totalRead < uncompSize && (read = ds.Read(temp, 0, (int)Math.Min(temp.Length, uncompSize - totalRead))) > 0)
                                {
                                    ms.Write(temp, 0, read);
                                    totalRead += read;
                                }
                                return ms.ToArray();
                            }
                        }
                        return null;
                    }
                }
            }
            return null;
        }

        private static List<string> ExtractPdfTextLines(string pdfPath, out string title, out string author)
        {
            title = "";
            author = "";
            List<string> lines = new List<string>();

            try
            {
                byte[] bytes = File.ReadAllBytes(pdfPath);
                string ascii = Encoding.ASCII.GetString(bytes);

                Match mTitle = Regex.Match(ascii, @"/Title\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
                if (mTitle.Success) title = mTitle.Groups[1].Value;

                Match mAuthor = Regex.Match(ascii, @"/Author\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
                if (mAuthor.Success) author = mAuthor.Groups[1].Value;

                int searchStart = 0;
                while (searchStart < bytes.Length)
                {
                    int streamIdx = ascii.IndexOf("stream", searchStart, StringComparison.Ordinal);
                    if (streamIdx < 0) break;

                    int dataStart = streamIdx + 6;
                    if (dataStart < bytes.Length && bytes[dataStart] == 13 && dataStart + 1 < bytes.Length && bytes[dataStart + 1] == 10)
                        dataStart += 2;
                    else if (dataStart < bytes.Length && bytes[dataStart] == 10)
                        dataStart += 1;

                    int endIdx = ascii.IndexOf("endstream", dataStart, StringComparison.Ordinal);
                    if (endIdx < 0) break;

                    int len = endIdx - dataStart;
                    if (len > 10)
                    {
                        try
                        {
                            using (MemoryStream ms = new MemoryStream(bytes, dataStart + 2, len - 2))
                            using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                            using (MemoryStream outMs = new MemoryStream())
                            {
                                ds.CopyTo(outMs);
                                string decompressed = Encoding.UTF8.GetString(outMs.ToArray());

                                MatchCollection textMatches = Regex.Matches(decompressed, @"\(([^\\)]{2,})\)");
                                foreach (Match tm in textMatches)
                                {
                                    string val = tm.Groups[1].Value.Trim();
                                    if (val.Length > 1 && !val.StartsWith("/") && !lines.Contains(val))
                                    {
                                        lines.Add(val);
                                        if (lines.Count > 200) break;
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (lines.Count > 200) break;
                    searchStart = endIdx + 9;
                }
            }
            catch { }

            return lines;
        }

        private static Bitmap ExtractPdfEmbeddedImage(string pdfPath, int pageNumber)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(pdfPath);
                int imageCount = 0;
                for (int i = 0; i < bytes.Length - 4; i++)
                {
                    if (bytes[i] == 0xFF && bytes[i + 1] == 0xD8 && bytes[i + 2] == 0xFF)
                    {
                        imageCount++;
                        if (imageCount == pageNumber || (pageNumber == 1 && imageCount == 1))
                        {
                            int end = -1;
                            for (int j = i + 3; j < bytes.Length - 1; j++)
                            {
                                if (bytes[j] == 0xFF && bytes[j + 1] == 0xD9)
                                {
                                    end = j + 2;
                                    break;
                                }
                            }

                            if (end > i)
                            {
                                int imgLen = end - i;
                                byte[] imgData = new byte[imgLen];
                                Array.Copy(bytes, i, imgData, 0, imgLen);
                                using (MemoryStream ms = new MemoryStream(imgData))
                                {
                                    return new Bitmap(Image.FromStream(ms));
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static void DrawImageFittedInRect(Graphics g, Image img, Rectangle targetRect)
        {
            float ratioX = (float)(targetRect.Width - 16) / Math.Max(1, img.Width);
            float ratioY = (float)(targetRect.Height - 16) / Math.Max(1, img.Height);
            float ratio = Math.Min(ratioX, ratioY);

            int drawW = Math.Max(1, (int)(img.Width * ratio));
            int drawH = Math.Max(1, (int)(img.Height * ratio));
            int posX = targetRect.X + (targetRect.Width - drawW) / 2;
            int posY = targetRect.Y + (targetRect.Height - drawH) / 2;

            g.DrawImage(img, posX, posY, drawW, drawH);
        }

        private static void DrawImageFitted(Graphics g, Image img, int canvasW, int canvasH)
        {
            float ratioX = (float)(canvasW - 16) / Math.Max(1, img.Width);
            float ratioY = (float)(canvasH - 16) / Math.Max(1, img.Height);
            float ratio = Math.Min(ratioX, ratioY);

            int drawW = Math.Max(1, (int)(img.Width * ratio));
            int drawH = Math.Max(1, (int)(img.Height * ratio));
            int posX = (canvasW - drawW) / 2;
            int posY = (canvasH - drawH) / 2;

            g.DrawImage(img, posX, posY, drawW, drawH);
        }

        private static void DrawPlaceholder(Graphics g, string text, int w, int h)
        {
            using (Font font = new Font("Segoe UI", 9, FontStyle.Regular))
            using (Brush brush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, font, brush, new RectangleF(10, 10, w - 20, h - 20), sf);
            }
        }

        public static Bitmap ApplyRotatedAndFiltered(Image source, int rotationDegrees, bool grayscale)
        {
            Bitmap bmp = new Bitmap(source);
            if (rotationDegrees == 90)
                bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
            else if (rotationDegrees == 180)
                bmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
            else if (rotationDegrees == 270)
                bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);

            if (grayscale)
            {
                Bitmap gray = new Bitmap(bmp.Width, bmp.Height);
                using (Graphics g = Graphics.FromImage(gray))
                {
                    ColorMatrix colorMatrix = new ColorMatrix(
                        new float[][]
                        {
                            new float[] {.3f, .3f, .3f, 0, 0},
                            new float[] {.59f, .59f, .59f, 0, 0},
                            new float[] {.11f, .11f, .11f, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1}
                        });
                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        attributes.SetColorMatrix(colorMatrix);
                        g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
                    }
                }
                bmp.Dispose();
                return gray;
            }

            return bmp;
        }
    }
}

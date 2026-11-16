using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PrintPulse
{
    public static class DocumentRenderer
    {
        public static int InspectPageCount(PrintJobItem item)
        {
            if (!File.Exists(item.FilePath)) return 1;

            try
            {
                if (item.Type == DocumentType.Image)
                {
                    using (Image img = Image.FromFile(item.FilePath))
                    {
                        FrameDimension dimension = new FrameDimension(img.FrameDimensionsList[0]);
                        return img.GetFrameCount(dimension);
                    }
                }
                else if (item.Type == DocumentType.Text)
                {
                    string[] lines = File.ReadAllLines(item.FilePath);
                    int linesPerPage = 55; // standard letter/A4 printable lines
                    int pages = (int)Math.Ceiling(Math.Max(1, lines.Length) / (double)linesPerPage);
                    return Math.Max(1, pages);
                }
                else if (item.Type == DocumentType.Pdf)
                {
                    return ParsePdfPageCount(item.FilePath);
                }
            }
            catch
            {
                return 1;
            }
            return 1;
        }

        public static int ParsePdfPageCount(string pdfPath)
        {
            try
            {
                using (FileStream fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs, Encoding.ASCII))
                {
                    string content = sr.ReadToEnd();
                    // Match /Type\s*/Pages\b.*?/Count\s+(\d+)
                    Match match = Regex.Match(content, @"/Type\s*/Pages\b[^>]*?/Count\s+(\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        int count;
                        if (int.TryParse(match.Groups[1].Value, out count) && count > 0)
                            return count;
                    }

                    // Fallback: count individual /Type\s*/Page\b matches
                    MatchCollection matches = Regex.Matches(content, @"/Type\s*/Page\b", RegexOptions.IgnoreCase);
                    if (matches.Count > 0)
                        return matches.Count;
                }
            }
            catch { }
            return 1;
        }

        public static Bitmap RenderPageThumbnail(PrintJobItem item, int pageNumber, int targetWidth, int targetHeight)
        {
            int w = Math.Max(100, targetWidth);
            int h = Math.Max(100, targetHeight);
            Bitmap bmp = new Bitmap(w, h);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Border & shadow simulation
                using (Pen borderPen = new Pen(Color.FromArgb(210, 215, 225), 1))
                {
                    g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
                }

                if (!File.Exists(item.FilePath))
                {
                    DrawPlaceholder(g, "File not found", w, h);
                    return bmp;
                }

                try
                {
                    if (item.Type == DocumentType.Image)
                    {
                        using (Image src = Image.FromFile(item.FilePath))
                        {
                            FrameDimension dimension = new FrameDimension(src.FrameDimensionsList[0]);
                            int frameIndex = Math.Min(pageNumber - 1, src.GetFrameCount(dimension) - 1);
                            if (frameIndex > 0)
                                src.SelectActiveFrame(dimension, frameIndex);

                            Bitmap transformed = ApplyRotatedAndFiltered(src, item.RotationDegrees, item.ColorMode == ColorPrintMode.Monochrome);
                            DrawImageFitted(g, transformed, w, h);
                            transformed.Dispose();
                        }
                    }
                    else if (item.Type == DocumentType.Text)
                    {
                        DrawTextPage(g, item.FilePath, pageNumber, w, h);
                    }
                    else if (item.Type == DocumentType.Pdf)
                    {
                        DrawPdfVisualPreview(g, item, pageNumber, w, h);
                    }
                }
                catch (Exception ex)
                {
                    DrawPlaceholder(g, "Preview unavailable: " + ex.Message, w, h);
                }
            }
            return bmp;
        }

        private static void DrawImageFitted(Graphics g, Image img, int canvasW, int canvasH)
        {
            float ratioX = (float)(canvasW - 16) / img.Width;
            float ratioY = (float)(canvasH - 16) / img.Height;
            float ratio = Math.Min(ratioX, ratioY);

            int drawW = (int)(img.Width * ratio);
            int drawH = (int)(img.Height * ratio);
            int posX = (canvasW - drawW) / 2;
            int posY = (canvasH - drawH) / 2;

            g.DrawImage(img, posX, posY, drawW, drawH);
        }

        private static void DrawTextPage(Graphics g, string filePath, int pageNumber, int w, int h)
        {
            string[] allLines = File.ReadAllLines(filePath);
            int linesPerPage = 45;
            int startLine = (pageNumber - 1) * linesPerPage;

            using (Font titleFont = new Font("Segoe UI", 9, FontStyle.Bold))
            using (Font contentFont = new Font("Consolas", 8, FontStyle.Regular))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (Brush headerBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                g.DrawString(Path.GetFileName(filePath) + " - Page " + pageNumber, titleFont, headerBrush, 12, 10);
                g.DrawLine(Pens.LightGray, 12, 26, w - 12, 26);

                float y = 32;
                for (int i = startLine; i < Math.Min(allLines.Length, startLine + linesPerPage); i++)
                {
                    string line = allLines[i];
                    if (line.Length > 80) line = line.Substring(0, 80) + "...";
                    g.DrawString(line, contentFont, textBrush, 12, y);
                    y += 14;
                    if (y > h - 20) break;
                }
            }
        }

        private static void DrawPdfVisualPreview(Graphics g, PrintJobItem item, int pageNumber, int w, int h)
        {
            // Lightweight visual schematic representation of PDF page
            using (Brush paperBg = new SolidBrush(Color.FromArgb(250, 252, 255)))
            {
                g.FillRectangle(paperBg, 8, 8, w - 16, h - 16);
            }

            using (Pen framePen = new Pen(Color.FromArgb(226, 232, 240), 1))
            {
                g.DrawRectangle(framePen, 8, 8, w - 16, h - 16);
            }

            // Top Header Bar
            using (Brush barBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
            {
                g.FillRectangle(barBrush, 16, 16, w - 32, 24);
            }

            using (Font badgeFont = new Font("Segoe UI", 8, FontStyle.Bold))
            using (Font textFont = new Font("Segoe UI", 8, FontStyle.Regular))
            using (Brush badgeBrush = new SolidBrush(Color.FromArgb(79, 70, 229)))
            using (Brush darkText = new SolidBrush(Color.FromArgb(51, 65, 85)))
            {
                g.DrawString("PDF DOCUMENT", badgeFont, badgeBrush, 22, 21);
                g.DrawString("Page " + pageNumber + " of " + item.TotalPages, textFont, darkText, w - 110, 21);
            }

            // Simulated content layout lines
            using (Brush lineBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
            {
                float y = 55;
                int lineCount = 0;
                while (y < h - 40 && lineCount < 20)
                {
                    int lineW = (lineCount % 4 == 0) ? (w - 120) : (w - 60);
                    g.FillRectangle(lineBrush, 24, y, lineW, 5);
                    y += 13;
                    lineCount++;
                }
            }

            // File metadata info in bottom banner
            using (Font metaFont = new Font("Segoe UI", 7, FontStyle.Italic))
            using (Brush metaBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                string info = string.Format("{0} ({1}) | Rotation: {2}°", item.FileName, item.GetFileSizeFormatted(), item.RotationDegrees);
                g.DrawString(info, metaFont, metaBrush, 20, h - 25);
            }
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

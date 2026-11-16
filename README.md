# PrintPulse - Batch Print Studio (v1.0)

> Universal, dependable batch printing solution with real-time spooler monitoring, page-level error recovery, interactive document preview, and zero-dependency legacy-to-modern Windows compatibility.

Compatible with **Windows XP SP3, Vista, 7, 8, 8.1, 10, and 11** (32-bit & 64-bit).

---

## 🌟 Key Features

1. **Universal Drag-and-Drop Queue:**
   - Drop PDF documents, images (`.jpg`, `.png`, `.bmp`, `.tiff`), and text files (`.txt`, `.csv`).
   - Drag to re-order print priorities with fluid mouse control.
   - Quick 1-click sorting and Up/Down movement buttons.
   - Checkbox to temporarily include/exclude documents without removing them.

2. **Built-in Document Previewer & Inspector:**
   - Zero-dependency document preview (no Adobe Acrobat, Chrome, or Foxit required).
   - Page navigation (`Prev`, `Next`, `Page X of Y`).
   - **Quick Rotate 90° / 180° / 270°:** 1-click rotation to fix upside-down or sideways scans before printing.

3. **Per-File & Global Print Controls (Responsive Layout):**
   - Custom Page Range (e.g., `All`, `1-4, 7`).
   - Number of copies.
   - Color Mode: Monochrome (B&W) vs. Full Color.
   - Duplex: 1-Sided (Simplex), 2-Sided Long Edge (Booklet), 2-Sided Short Edge (Calendar).
   - N-Up: 1, 2, or 4 pages per sheet.
   - "Apply to All" button to propagate settings across hundreds of files in one click.

4. **Live Spooler Telemetry & Jam Interception:**
   - Detects all USB, Network LAN, Wi-Fi, and Local printers using Windows native `winspool.drv`.
   - Real-time status: `Ready`, `Spooling`, `Printing Page X of Y`, `Paper Jam`, `Out of Paper`, `Offline`.
   - **Automatic Queue Freeze:** When a printer error or paper jam occurs, the queue halts immediately.

5. **Interactive Reprint Wizard:**
   - Prompts the user with clear recovery options:
     - `Resume / Reprint from Page [X]` (reprints only the failed/damaged page onwards).
     - `Reprint Entire Document`.
     - `Skip File & Continue Next`.
     - `Abort Batch`.

6. **Print Session Summary & Audit Report:**
   - Breakdown of Total Files, Pages Printed, Sheets Used, B&W vs. Color count.
   - Configurable Cost / Page billing calculator for print shops and offices.
   - Export report to **CSV**, **Text File**, or **Print Receipt**.

7. **In-App Feedback & Suggestions:**
   - Built-in feedback form routed directly to `hs123253@gmail.com` with auto-email launch and clipboard copy.

---

## 📦 Packages & Distribution

Both editions are located in the `dist/` folder:

1. **Portable Edition (Zero Install):**
   * **Location:** [`dist/Portable/PrintPulse.exe`](file:///C:/Users/pc/.gemini/antigravity/scratch/PrintPulse/dist/Portable/PrintPulse.exe)
   * **Usage:** Single standalone `.exe` (74 KB). No installation required. Runs directly from USB pen drives or any folder on Windows XP through Windows 11.

2. **Installable Edition (Setup Wizard):**
   * **Location:** [`dist/Installer/PrintPulse_Setup_v1.0.exe`](file:///C:/Users/pc/.gemini/antigravity/scratch/PrintPulse/dist/Installer/PrintPulse_Setup_v1.0.exe)
   * **Usage:** Complete setup wizard (87 KB). Prompts for destination folder, creates Desktop & Start Menu shortcuts, and registers in Windows Control Panel (Add/Remove Programs). Also includes the Inno Setup script [`installer/PrintPulse_Installer.iss`](file:///C:/Users/pc/.gemini/antigravity/scratch/PrintPulse/installer/PrintPulse_Installer.iss).

---

## 📚 Knowledge Transfer & Developer Documentation

* **[`HANDOVER.md`](file:///C:/Users/pc/.gemini/antigravity/scratch/PrintPulse/HANDOVER.md):** Complete technical handover and KT guide for incoming AI assistants (Claude, Codex, Antigravity) and developers.
* **[`CHAT_HISTORY_AND_DECISIONS.md`](file:///C:/Users/pc/.gemini/antigravity/scratch/PrintPulse/CHAT_HISTORY_AND_DECISIONS.md):** Complete chronological log of all user requests, decisions, and feature implementations.

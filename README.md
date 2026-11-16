# PrintPulse - Batch Print Studio (v1.0)

> Universal, dependable batch printing solution with real-time spooler monitoring, page-level error recovery, and interactive document preview.

Compatible with **Windows XP SP3, Vista, 7, 8, 8.1, 10, and 11** (32-bit & 64-bit).

---

## 🌟 Key Features

1. **Universal Drag-and-Drop Queue:**
   - Drop PDF documents, images (`.jpg`, `.png`, `.bmp`, `.tiff`), and text files (`.txt`, `.csv`).
   - Drag to re-order print priorities with fluid mouse control.
   - Quick 1-click sorting (A-Z, Date, Page count) and Up/Down movement buttons.
   - Checkbox to temporarily include/exclude documents without removing them.

2. **Built-in Document Previewer & Inspector:**
   - Zero-dependency document preview (no Adobe Acrobat, Chrome, or Foxit required).
   - Page navigation (`Prev`, `Next`, `Page X of Y`).
   - **Quick Rotate 90° / 180° / 270°:** 1-click rotation to fix upside-down or sideways scans before printing.

3. **Per-File & Global Print Controls:**
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

---

## 🏗️ Architecture & Dependability

* **Runtime Target:** .NET Framework 4.0 Client Profile.
  * Native on Windows 8, 8.1, 10, 11 (pre-installed by Microsoft).
  * Runs on Windows XP SP3, Vista, and Windows 7 SP1.
* **Low-Level Spooler Control:** P/Invoke bindings to `winspool.drv` (`OpenPrinter`, `EnumPrinters`, `EnumJobs`, `GetPrinter`, `SetJob`).
* **Binary Size:** **~67 KB** (single standalone portable `.exe`). Zero bloat, zero heavy web runtimes.

---

## 📦 Distribution Packages

* **Portable Edition:** Located at `dist/Portable/PrintPulse.exe`. Runs directly from USB thumb drives without installation.
* **Setup Installer:** Script located at `installer/PrintPulse_Installer.iss`. Checks for .NET 4.0 presence before installation and creates Start Menu / Desktop shortcuts.

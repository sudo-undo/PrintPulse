# PrintPulse — Universal Batch Print Studio (v1.2)

> Universal, dependable batch printing solution with real-time spooler monitoring, page-level error recovery, interactive document preview, legal Bates stamping, watermarks, pre-print cost calculation, and zero-dependency legacy-to-modern Windows compatibility.

Compatible with **Windows XP SP3, Vista, 7, 8, 8.1, 10, and 11** (32-bit & 64-bit).

🌐 **Official Website:** [https://sudo-undo.github.io/PrintPulse](https://sudo-undo.github.io/PrintPulse)

```powershell
# Install via Windows Package Manager
winget install sudo-undo.PrintPulse
```

---

## 🌟 Key Features

1. **Universal Drag-and-Drop Queue & Windows Explorer Integration:**
   - Drop PDF documents, Word documents (`.docx`), images (`.jpg`, `.png`, `.bmp`, `.tiff`), and text files (`.txt`, `.csv`, `.log`).
   - **Windows Explorer Right-Click:** Select multiple documents in Windows Explorer, right-click, and choose **"Print with PrintPulse"** to instantly populate the queue.
   - Drag to re-order print priorities with fluid 4-way move cursor and grip handles (`⋮⋮ 1`).
   - Quick 1-click column header sorting (Explorer style) and top dropdown sort menu.

2. **Legal Bates Stamping & Watermarking (PMF Feature):**
   - **Bates Page Numbering:** Sequential page stamps (e.g. `CASE-001 - Page 1 of 50`, custom prefix, customizable start number, and date/time).
   - **Diagonal Watermarks:** Semi-transparent diagonal text (`CONFIDENTIAL`, `DRAFT`, `COPY`, `SAMPLE`, `URGENT`, `INTERNAL ONLY`, or custom text) rendered onto every page.

3. **Pre-Print Cost & Ink Savings Estimator:**
   - Configurable B&W and Color rates per sheet.
   - Dynamic real-time readout before spooling: *"Active: 12 Files | 48 Pages | 24 Sheets | Est. Cost: $1.80 • 🌱 24 Sheets Saved (Duplex)"*.

4. **Built-in Document Previewer & Inspector:**
   - Zero-dependency document preview (no Adobe Acrobat, Chrome, MS Office, or Foxit required).
   - Page navigation (`Prev`, `Next`, `Page X of Y`).
   - **Quick Rotate 90° / 180° / 270°:** 1-click rotation to fix upside-down or sideways scans before printing.

5. **Per-File & Global Print Controls (Responsive Layout):**
   - Custom Page Range (e.g., `All`, `Current Page`, `1-4, 7`), Subsets (Odd / Even).
   - Number of copies, Scaling (Fit, Actual Size, Shrink, Custom Scale %).
   - Duplex: 1-Sided (Simplex), 2-Sided Long Edge, 2-Sided Short Edge, and **Interactive Manual Duplex Assistant** for single-sided printers.
   - Imposition: 1-Up, 2-Up, 4-Up, and **Booklet / Saddle-Stitch** brochure folding.
   - "Apply to All" button to propagate settings across hundreds of files in one click.

6. **Live Spooler Telemetry & Jam Interception:**
   - Detects all USB, Network LAN, Wi-Fi, and Local printers using Windows native `winspool.drv`.
   - Real-time status: `Ready`, `Spooling`, `Printing Page X of Y`, `Paper Jam`, `Out of Paper`, `Offline`.
   - **Automatic Queue Freeze:** When a printer error or paper jam occurs, the queue halts immediately.

7. **Interactive Reprint Wizard:**
   - Prompts the user with clear recovery options:
     - `Resume / Reprint from Page [X]` (reprints only the failed/damaged page onwards).
     - `Reprint Entire Document`.
     - `Skip File & Continue Next`.
     - `Abort Batch`.

8. **Advanced Automation & Export:**
   - **Drop Watch Folders:** Background daemon automatically queuing files dropped into any designated folder.
   - **Consolidated PDF Export:** 1-click merge of all queued files into a single consolidated PDF document.
   - **Session Crash & Power-Outage Recovery:** Real-time state persistence and automatic restoration on startup.
   - **Self-Upgradable Releases:** Automated background GitHub Releases checking with 1-click seamless in-place upgrade.

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

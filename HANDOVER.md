# PrintPulse — Developer Handover & Knowledge Transfer (KT)

> **NOTICE FOR INCOMING AI AGENTS & DEVELOPERS:**
> This document is designed to give any AI assistant (such as Antigravity, Claude, Codex, Cursor, GPT) or human engineer complete context, architectural background, build procedures, and operational rules so development can continue seamlessly without lost context.

---

## 1. Project Identity & Purpose
* **Project Name:** PrintPulse (Batch Print Studio)
* **Author / Feedback Contact:** `hs123253@gmail.com`
* **GitHub Repository:** https://github.com/sudo-undo/PrintPulse
* **Repository Path:** `C:\Users\pc\.gemini\antigravity\scratch\PrintPulse`
* **Core Function:** High-speed batch printing tool allowing users to queue multiple documents (PDF, JPG, PNG, BMP, TIFF, TXT, CSV), drag to reorder, preview pages, adjust per-file print options, monitor Windows Print Spooler status in real time, auto-freeze on paper jams/errors with page-level reprint prompts, and generate a final print audit report.
* **Licensing Mode:** 100% Unlocked, fully functional (all paid barriers removed for direct use).

---

## 2. Operating System & Compatibility Directives

| Requirement | Choice | Why This Choice Was Made |
| :--- | :--- | :--- |
| **Target OS Range** | Windows XP SP3, Vista, 7, 8, 8.1, 10, 11 (32-bit & 64-bit) | Print shops and cybercafes often use legacy Windows PCs (XP / 7) alongside modern Win 10/11 machines. |
| **Framework Target** | **.NET Framework 4.0 Client Profile** | Electron/Tauri/Node/Modern .NET 6+ do **not** run on Windows XP/Vista. .NET 4.0 is pre-installed on Windows 8–11 and supported on XP SP3/Win 7. |
| **Dependencies** | Zero external runtime installers | Pure Win32 P/Invoke to `winspool.drv` and native GDI+ (`System.Drawing`). |
| **Single Portable Size** | **~72 KB** | Lightweight, instantaneous launch, runs directly from USB drives. |

---

## 3. Codebase File Index & Roles

All source code resides in `src/`:

| File | Purpose & Responsibilities |
| :--- | :--- |
| `src/NativeSpooler.cs` | Win32 P/Invoke interop with `winspool.drv`. Implements `EnumPrinters`, `OpenPrinter`, `GetPrinter`, `EnumJobs`, `SetJob`, and reads status flags (`PRINTER_STATUS_PAPER_JAM`, `PRINTER_STATUS_PAPER_OUT`, `PRINTER_STATUS_OFFLINE`, `PRINTER_STATUS_DOOR_OPEN`, `JOB_STATUS_ERROR`). |
| `src/PrintJobItem.cs` | Data model for queue items. Holds path, filename, page count, range parsing (`ResolvePageIndices`), duplex mode, copies, color mode, rotation angle, current progress, error notes, and sheet count calculation. |
| `src/DocumentRenderer.cs` | Renders pages to GDI+ bitmaps for UI preview and printer canvas. Parses PDF page count via `/Count` regex, renders images and text files, handles rotation (90°, 180°, 270°), and grayscale matrix conversions. |
| `src/BatchPrintController.cs` | Background worker thread that drives sequential printing. Checks printer hardware health before each job, drives `System.Drawing.Printing.PrintDocument`, emits live progress events, catches jams, triggers reprint wizard, and generates `BatchReport`. |
| `src/PrintReport.cs` | Model and business logic for the final print session. Aggregates sheets used, pages printed, color vs. B&W, computes cost/bill, and exports summary to `.csv` and `.txt`. |
| `src/ReprintDialog.cs` | Modal dialog triggered when a spooler error/jam is detected. Gives user 4 choices: Resume from Page X, Reprint Entire File, Skip File, or Abort Batch. |
| `src/ReportForm.cs` | UI form displaying the final audit summary with metric cards, item audit table, and CSV/text/receipt export buttons. |
| `src/FeedbackForm.cs` | UI form allowing users to submit feature requests and bug reports directly to `hs123253@gmail.com` via mailto or clipboard copy. |
| `src/MainForm.cs` | Main application window. Fluid split layout: Left queue with drag-and-drop file drop & row reordering; Right previewer with page scroller, rotate button, and responsive TableLayout settings; Bottom action bar with progress, status, and control buttons. |
| `src/Program.cs` | Application entry point with DPI awareness enablement and global unhandled exception catcher. |
| `src/app.manifest` | Windows manifest declaring compatibility with Vista, 7, 8, 8.1, 10, and 11. |
| `src/PrintPulse.csproj` | MSBuild project configuration targeting `.NETFramework,Version=v4.0,Profile=Client`. |

---

## 4. Build, Compile & Release Procedures

### Compiling via Built-in MSBuild (No Visual Studio Required):
Run the following PowerShell command:
```powershell
& "C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" "C:\Users\pc\.gemini\antigravity\scratch\PrintPulse\src\PrintPulse.csproj" /p:Configuration=Release
```

### Updating the Portable Executable:
After a successful build, copy the binary to the portable distribution folder:
```powershell
Copy-Item "C:\Users\pc\.gemini\antigravity\scratch\PrintPulse\bin\Release\PrintPulse.exe" "C:\Users\pc\.gemini\antigravity\scratch\PrintPulse\dist\Portable\PrintPulse.exe" -Force
```

### Building the Setup Installer:
Inno Setup script is located at:
`installer/PrintPulse_Installer.iss`
It includes a pre-flight registry check for `.NET 4.0 Client Profile` and prompts the user if missing on unpatched XP/7 systems.

---

## 5. Developer & AI Assistant Protocol

When making modifications or adding new features:
1. **Always edit C# 4/5 compatible code** (avoid C# 7+ pattern matching like `x is Type varName`, use `as Type` instead) to maintain compatibility with the built-in v4.0.30319 CSC compiler.
2. **Compile and verify** with MSBuild before finalizing any turn.
3. **Update `dist/Portable/PrintPulse.exe`** so the user always has a ready-to-test executable.
4. **Git Commit Rule:** Commit every change to the local Git repository with a descriptive message (`git add .`, `git commit -m "..."`).
5. **Keep KT and History Synced:** Update `HANDOVER.md` and `CHAT_HISTORY_AND_DECISIONS.md` whenever new features or architectural changes are introduced.

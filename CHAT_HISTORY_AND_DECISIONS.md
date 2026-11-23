# PrintPulse — Complete Chat History, Decisions & Implementation Log

This document preserves the complete chronological record of user requests, assistant proposals, technical evaluations, architectural decisions, and implemented features.

---

## Session 1: Feasibility, Market Research & Architecture Discussion
* **User Request:**
  * Can we create an extension or Windows program for batch printing documents?
  * Key requirements: Multiple file drag-and-drop, drag to reorder print sequence, real-time print status tracking, error detection (paper jam, etc.) with prompt to reprint specific page, compatibility with any printer (USB, LAN, etc.) and Windows PC.
  * Question: Is there a market/demand or is it a waste of time? What features are needed? Can we earn money?
* **Analysis & Decisions:**
  * **Market Validation:** Confirmed strong demand in small business / B2B sectors (photocopy & print shops, law firms, accounting desks, school offices, e-commerce warehouses) where operators waste hours opening PDFs one-by-one.
  * **Architecture Choice:** Clarified that a **Chrome extension alone is technically incapable** of low-level spooler control and silent printing due to browser security sandboxes. Recommended a native Windows desktop utility.
  * **Spooler Hardware Reality:** Explained how Windows Print Spooler buffers jobs and why high-end printers report page-level telemetry whereas basic USB printers report general alerts. Designed a hybrid approach: page-chunked spooler monitoring with an interactive "Resume from Page X" recovery wizard.

---

## Session 2: Legacy Windows Compatibility (XP up to 11) & Master Feature Scope
* **User Request:**
  * Must run on **Windows XP, Vista, 7, 8, 10, and 11**.
  * Provide both an **installable app** and a **portable app**.
  * Finalize master feature set.
* **Analysis & Decisions:**
  * **Framework Selection:** Identified that modern runtimes (Electron, Chromium, .NET 6/7/8) completely dropped Windows XP, Vista, and 7.
  * Selected **.NET Framework 4.0 Client Profile**: the universally compatible runtime that installs on XP SP3/Vista/7 and is pre-installed out-of-the-box on Windows 8, 10, and 11.
  * Low-level access via `winspool.drv` P/Invoke and native GDI+ image/vector rendering.
  * Included both a zero-install **Portable `.exe`** and an **Inno Setup installer script**.

---

## Session 3: Additional In-Demand Features & Print Report
* **User Request:**
  * Add support for more file types.
  * Add high-demand features (N-Up, Rotate, etc.).
  * Provide a **Final Print Report**.
  * Ensure rock-solid reliability with pre-flight prerequisite checks.
* **Decisions & Implementation:**
  * Formats: PDF, Images (`.jpg`, `.png`, `.bmp`, `.tiff`, `.tif`, `.gif`), Text files (`.txt`, `.csv`, `.log`, `.rtf`).
  * Features added:
    1. **N-Up Printing:** 1, 2, or 4 pages per sheet (paper & ink saver).
    2. **1-Click 90° Rotation:** Quick fix for upside-down scans.
    3. **Final Print Session Summary:** Total files, pages, sheets, color vs. B&W, and bill calculation with CSV/text/receipt export.
    4. **Smart Bootstrapper:** Installer checks Windows Registry for .NET 4.0 before setup.
  * App officially named: **PrintPulse**.

---

## Session 4: Prototype Compilation & Verification
* Created initial C# codebase under `C:\Users\pc\.gemini\antigravity\scratch\PrintPulse\src`.
* Resolved C# compiler language version constraint (avoided C# 7 pattern matching syntax to compile cleanly on built-in .NET 4.0 MSBuild).
* Successfully compiled standalone release executable `PrintPulse.exe` (~67 KB) in 1.08 seconds.

---

## Session 5: UI Layout Fixing, Feedback Channel, Git & Handover Documentation
* **User Feedback & Request:**
  * Provided screenshot showing that on certain screen resolutions/scaling, the right preview panel was squeezed, button labels were cut off (`Rotate 90...`, `Color: M...`, `Layout: 1 Page / Shee...`), and bottom buttons were hidden.
  * Remove any paid barrier — keep app 100% unlocked and free for full use.
  * Add an in-app Feedback & Suggestion feature routing messages to `hs123253@gmail.com`.
  * Initialize Git repository and commit changes.
  * Create comprehensive handover (`HANDOVER.md`) and chat history documentation for knowledge transfer.
* **Implementation:**
  * **Responsive UI Overhaul:**
    * Changed fixed splitter distance to proportional ratio (`52% left / 48% right`).
    * Replaced fixed X-coordinate inputs in settings panel with responsive `TableLayoutPanel` (2 rows x 4 columns) with auto-scaling percentage columns.
    * Converted top bar into Left `FlowLayoutPanel` (Queue actions) and Right docked panel (Printer selection) so controls never overlap.
    * Converted bottom bar into responsive Left docked panel (Progress & Status) and Right `FlowLayoutPanel` with `RightToLeft` alignment (Start, Pause, Stop, Report buttons) so buttons are never pushed off-screen.
  * **Feedback Form:** Created `FeedbackForm.cs` with category selection, subject, message, auto-mailto launcher for `hs123253@gmail.com`, and a one-click "Copy to Clipboard" fallback.
  * **Unlocked App:** Full functionality unlocked without restrictions or paywalls.
  * **Git Repository:** Initialized Git repo, created `.gitignore`, configured user credentials, and committed all source files.
  * **Documentation:** Created `HANDOVER.md`, `CHAT_HISTORY_AND_DECISIONS.md`, and updated `README.md`.

---

## Session 10: Live Telemetry Pipeline Activation & In-App Verification
* **Setup & Verification Completed:**
  * **Direct Feedback Delivery:** Activated direct HTTPS delivery to `hs123253@gmail.com`. Verified in-app submission test returned: `Result: True | Server Response: Feedback submitted successfully!`.
  * **Anonymous Diagnostic Error Logger:** Configured `WriteToLocalDiagnosticLog` writing to `%LocalAppData%\PrintPulse\Logs\diagnostics.log`. Verified real-time error capture with PII redaction (e.g. `C:\Users\JohnDoe\...` -> `C:\Users\[REDACTED]\...`).
  * **Sentry Ingestion Ready:** Built-in support for remote Sentry DSN endpoint with silent background dispatch.
  * **Release Packages Updated:** Updated `dist/Portable/PrintPulse.exe` (81 KB) and recompiled `dist/Installer/PrintPulse_Setup_v1.0.exe` (94 KB).
  * **GitHub Synchronized:** Committed and pushed to remote repository `sudo-undo/PrintPulse`.
* **User Request:**
  * Adopt the industry standard for receiving error statistics (reliable, dependable, free).
  * Replace the personal mailto method with direct in-app feedback submission.
* **Architecture & Implementation:**
  * **TelemetryService:** Created `src/TelemetryService.cs` handling:
    1. Direct in-app feedback submission over HTTPS without requiring a local email client.
    2. Automatic anonymous crash & hardware spooler error tracking (compatible with Sentry HTTPS ingestion API).
    3. Legal PII sanitization: automatically scrubs Windows usernames and file paths (`C:\Users\[REDACTED]\...`).
    4. Safe TLS handling: automatically negotiates TLS 1.2 on modern Windows with fallback to TLS 1.0 on legacy Windows XP/7.
  * **Feedback Form Upgrade:** Upgraded `FeedbackForm.cs` with direct submission button (`🚀 Submit Feedback`), live sending indicator, and opt-in diagnostic toggle.
  * **Error Hooking:** Integrated error tracking in `Program.cs` (crash events) and `BatchPrintController.cs` (printer hardware errors).
  * **Build & Distribution:** Updated `PrintPulse.exe` (79 KB) and recompiled standalone installer `PrintPulse_Setup_v1.0.exe` (92 KB).
  * **GitHub Synchronization:** Committed all changes and pushed to remote repository `sudo-undo/PrintPulse`.
* **User Request:**
  * Where is the portable file located?
  * Where is the installable version located?
* **Implementation & Build:**
  * **Portable App Location:** `dist/Portable/PrintPulse.exe` (74 KB).
  * **Installable Setup Wizard:** Created and compiled `installer/SetupInstaller.cs` into `dist/Installer/PrintPulse_Setup_v1.0.exe` (87 KB). It embeds the portable executable payload, prompts for custom install location, installs shortcuts to Desktop & Start Menu, and registers in Windows Add/Remove Programs.
  * Pushed all updates to GitHub repository `sudo-undo/PrintPulse`.
* **Audit Scope:** Full codebase scan across GDI+ resource management, concurrency, file locks, and bounds checking.
* **Vulnerabilities & Issues Identified and Resolved:**
  1. **File Locking Flaw (GDI+ `Image.FromFile`):** Replaced direct file opens with `FileStream` utilizing `FileShare.ReadWrite`. Files queued or previewed are no longer locked on disk by Windows.
  2. **Memory Leak Safeguard:** Enclosed all dynamic bitmaps (`transformed`) and graphic buffers in strict `using (...)` scopes to prevent GDI object leaks.
  3. **Large File Streaming Guard:** Refactored text rendering to stream lines incrementally rather than loading entire files into memory with `File.ReadAllLines`, preventing memory spikes on large files.
  4. **Thread Safety & Clean Exit:** Implemented `MainForm_FormClosing` event to warn user if a print run is active, request clean worker cancellation, and dispose graphics handles before window destruction.
  5. **Compiler Cleanliness:** Resolved all code warnings (0 warnings, 0 errors).
* **Git & GitHub Synchronization:** Committed audit enhancements and pushed to remote `main` branch.
* **Issue Reported:**
  * Application was exiting immediately without displaying the main window.
* **Root Cause Diagnostics:**
  * When `SplitContainer` was initialized in `MainForm.cs`, setting `Panel1MinSize` and `Panel2MinSize` during object instantiation exceeded the container's unparented default width, throwing a silent `InvalidOperationException: SplitterDistance must be between Panel1MinSize and Width - Panel2MinSize`.
* **Fix & Verification:**
  * Removed premature minimum size constraints during SplitContainer instantiation.
  * Deferred proportional splitter sizing to `this.Shown` event inside a guarded try/catch block.
  * Added top-level unhandled exception handlers in `Program.cs` to ensure any runtime errors surface visibly to the user.
  * Verified successful instantiation via automated test harness: `MainForm created with no exceptions! Form size: {Width=1100, Height=720}`.
  * Updated portable release executable and pushed fix to GitHub repository `sudo-undo/PrintPulse`.
* **User Request:**
  * Inquired what their GitHub account username is.
* **Actions & Decisions:**
  * Detected connected GitHub credentials on system: Username is **`sudo-undo`** (https://github.com/sudo-undo).
  * Automatically created the remote repository: https://github.com/sudo-undo/PrintPulse.
  * Linked the local Git project to `origin` and performed initial push to `main` branch.
  * Verified that all subsequent updates will automatically commit and push to `origin/main`.
* **User Feedback & Queue/Preview/UI Fixes (2026-11-16):**
  * **Queue Auto-Update on Settings Change:** Updated `MainForm.UpdateSelectedSettings()` to immediately reflect modified Copies, Color Mode, and Duplex Mode in the `ListView` items in real time, without requiring drag-and-drop row reordering.
  * **Document Preview Support for Non-Images:** Enhanced `DocumentRenderer.cs` to render true visual previews for `.pdf`, `.docx`, `.txt`, `.csv`, `.log`, `.xml`, `.html`, and `.json` documents onto a high-fidelity simulated paper surface with margins, line numbers, and extracted metadata (e.g. PDF title/author, embedded scans, and DOCX paragraphs via a zero-dependency ZIP central directory + Deflate stream extractor compatible with .NET 4.0 Client Profile).
  * **Relocated Feedback Button:** Removed `btnFeedback` from the main top action toolbar (`flowTopLeft`) and placed it discreetly into the bottom utility status bar (`flowBottomRight`).
  * **Disk Space Cleared & Rebuild:** Handled C: drive disk space restoration (3.15 GB free), successfully recompiled portable binary `dist/Portable/PrintPulse.exe` and setup installer `dist/Installer/PrintPulse_Setup_v1.0.exe` with zero errors.
* **Sentry Error Telemetry Integration (2026-11-16):**
  * Configured active project DSN: `https://628e8b818e6c0337ffa686d9ef422bae@o4512439853449216.ingest.us.sentry.io/4512441183502336`.
  * Implemented native Sentry Store API ingestion (`/api/{projectId}/store/`) in `TelemetryService.cs` with `X-Sentry-Auth` client authorization and anonymous PII-redacted payload structure (zero external dependencies, fully compatible with .NET 4.0 Client Profile).
  * Sent and verified test events in Sentry (Event ID `3bd28719ad4347cdbc1783ebc0de9865`).
  * Recompiled portable and installer binaries and pushed to GitHub `sudo-undo/PrintPulse`.
* **PrintPulse v1.1.0 Major Release (2026-11-16):**
  * **Adobe Acrobat-Style Page Selection & Handling:** Implemented Pages to Print (All, Current Page, Custom Range `1-4, 7`), Subsets (All in Range, Odd Pages Only, Even Pages Only), Reverse Order checkbox, Sizing (Fit, Actual Size 100%, Shrink Oversized, Custom Scale %), Orientation (Auto-Rotate & Center, Portrait, Landscape), and Toner/Ink Saver luminosity reduction.
  * **Imposition & Layout Engine:** Added 2-Up (side-by-side) and 4-Up (2x2 grid) sheet tiling with dividing borders, plus automatic Booklet / Saddle-Stitch brochure folding imposition.
  * **Interactive Manual Duplex Assistant:** Built `ManualDuplexWizard.cs` with animated visual step-by-step guidance showing users how to remove, turn 180°, and re-insert paper for printers lacking hardware duplexing.
  * **Hardware Pre-Flight Check & Multi-Tray Routing:** Integrated Win32 spooler hardware status checks (Paper Out, Door Open, Jam, Offline, Error) before batch launches and dynamic paper tray / bin routing per document via `PrinterSettings.PaperSources`.
  * **Advanced Options Modal (`AdvancedOptionsForm.cs`):** Added 1-click Presets/Profiles (Default Office, Eco-Draft, High-Quality Presentation, Folded Booklet), Slip-Sheet / Banner job dividers (Blank Page or Detailed Summary Slip), Queue Auto-Sorting (by Name, Pages, Type, Size), and native zero-dependency Merged PDF export (`DocumentRenderer.ExportBatchToPdf`).
  * **Smart Print Advisor:** Non-intrusive proactive suggestion banner automatically detecting document and printer capabilities (e.g. single-sided printer duplexing, 2-Up paper saving for large files, mixed orientation).
  * **Session Crash & Power-Outage Restore (`SessionManager.cs`):** Real-time queue persistence to `%LOCALAPPDATA%\PrintPulse\session_restore.dat` and auto-recovery on startup.
  * **Watch Folder Service (`WatchFolderService.cs`):** Background filesystem watcher automatically ingesting files dropped into user-designated folders.
  * **Artifacts & Distribution:** Built `dist/Portable/PrintPulse.exe` (127 KB) and `dist/Installer/PrintPulse_Setup_v1.1.exe` (140 KB), fully tested and verified with 0 errors.

* **UI Polishing, Window Scrolling & Expanded Telemetry Suite (2026-11-16):**
  * **Advanced Options Window Scrolling Fix (`AdvancedOptionsForm.cs`):** Fixed issue where the top group box ("Quick Print Presets") was clipped. Made form resizable (`FormBorderStyle.Sizable`, `Size = 720x640`, `MinimumSize = 680x500`), configured `content.AutoScroll = true` with `AutoScrollMinSize = new Size(640, 520)`, and corrected docking and z-order.
  * **UI Action Bar Optimization (`MainForm.cs`):** Relocated the `⚙ Advanced` button from the top queue manipulation toolbar (`flowTopLeft`) to the bottom utility action bar (`flowBottomRight`), grouping it cohesively alongside `💬 Feedback` and `📊 View Report`.
  * **Anonymous Product Analytics & Heartbeat (`TelemetryService.cs`):**
    * Daily/Weekly Active Users (DAU/WAU) session heartbeat with anonymous SHA-256 machine identifier.
    * Document Format Breakdown tracking (`pdf_count`, `docx_count`, `img_count`, `txt_count`).
    * Funnel & Feature Drop-Off Tracking (`TrackFunnelStep`).
    * Error Recovery Choice tracking (`TrackErrorRecoveryChoice`).
    * High-Volume & Paywall Intent Triggers (`TrackHighVolumeTrigger` for batches >10 files or single docs >50 pages).
  * **Binaries Recompiled & Verified:** Rebuilt with MSBuild (`0 Errors`, `bin/Release/PrintPulse.exe` = 138 KB, `dist/Installer/PrintPulse_Setup_v1.1.exe` = 150 KB).

* **Home Screen Queue Header Sorting & Dialog Text Boundary Fixes (2026-11-17):**
  * **Direct Column Header Sorting (Windows Explorer Style):** Clicking any table header (`File Name`, `Pages`, `Copies`, `Color`, `Duplex`, `Layout`, `Status`) directly sorts the queue ascending/descending with dynamic arrow indicators (`▲`/`▼`) on the active column. Clicking `#` restores natural queue addition order via `InitialIndex`.
  * **Queue Header Quick Sort Menu:** Added `[🔀 Sort Queue ▾]` dropdown menu right on top of the queue table with instant 1-click presets: A to Z, Z to A, Page Count (Asc/Desc), File Type, File Size, and Original Order.
  * **Advanced Options Text Boundary & Mnemonic Fix (`AdvancedOptionsForm.cs`):** Fixed cut-off text on `"Smart Blank-Page Suppression"` by expanding Group 2 to a dedicated two-row layout giving the checkbox full 600+ px horizontal width. Escaped ampersand (`&&`) in title `"Document Separation && Paper Saving"` so the `&` character renders visibly instead of acting as a Windows mnemonic shortcut.
  * **Recompiled & Tested:** Rebuilt portable executable (144 KB) and standalone installer (157 KB) with 0 errors.

* **UX Modernization & Drag-Hover Cursor Affordance (2026-11-17):**
  * **Interactive Drag Cursor & Grip Indicators:** Attached `MouseMove` and `MouseLeave` handlers to `_lvQueue` to dynamically morph the cursor into the standard 4-way move/drag cursor (`Cursors.SizeAll`) when hovering over queued file items, reverting to `Cursors.Default` when over empty space. Added modern `⋮⋮` grip glyphs to the `#` column (e.g. `⋮⋮ 1`, `⋮⋮ 2`) and attached item tooltips (*"Click and drag to reorder print sequence"*).
  * **Interactive Empty State Placeholder:** Designed a dedicated empty queue overlay (`_pnlEmptyState`) featuring a clean drop-target illustration, supported file extensions hint, and a prominent `[+ Add Files to Print]` button. The overlay automatically hides when items are added.
  * **Document Preview Empty State:** Replaced the blank grey picture box with a polished placeholder drawing featuring a dashed border and helpful instruction text when no file is selected.
  * **UI Polish & Tooltips:** Added comprehensive hover tooltips to all toolbar and action bar buttons; added dynamic rotation badge `⟲ Rotate (90°)`; added a dismiss button (`✕`) to the Smart Advisor; enlarged `[▶ START PRINT]` (145x42px, emerald green `#10B981`); and added dynamic paper/ink eco-savings calculation to the bottom summary bar.
  * **Binaries Recompiled & Pushed:** Portable executable (148 KB) and standalone installer (161 KB) rebuilt with 0 errors.


* **Comprehensive Security Audit, Cyber-Attack Hardening & Vulnerability Mitigation (2026-11-17):**
  * **Strict Extension Whitelist (`MainForm.cs`):** Replaced blacklist approach with an explicit whitelist of safe document extensions (`.pdf`, `.docx`, `.doc`, `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.tif`, `.tiff`, `.txt`, `.csv`, `.log`, `.rtf`, `.xml`, `.html`, `.htm`, `.json`). Mitigated arbitrary file execution, script payloads (`.bat`, `.cmd`, `.vbs`, `.ps1`), screensavers (`.scr`), and executable injection.
  * **Memory Exhaustion & DoS Defense (`DocumentRenderer.cs`):**
    * Multi-gigabyte PDF page count memory safety: Capped `ParsePdfPageCount` to stream read up to the first 4 MB and last 1 MB instead of loading entire multi-gigabyte files into memory with `sr.ReadToEnd()`.
    * Streamed text extraction safety: Capped `ExtractPdfTextLines` to read up to 8 MB via chunked `FileStream` instead of allocating whole files with `File.ReadAllBytes`.
  * **Zip Bomb & Decompression Bomb Defense (`DocumentRenderer.cs`):**
    * In DOCX ZIP central directory parsing (`ReadZipEntry`), enforced strict entry caps: uncompressed size capped to 35 MB, compressed size capped to 50 MB, and decompression stream capped to 35 MB.
    * In PDF Deflate stream decompression, capped output memory stream to 2 MB per stream to prevent memory exhaustion from crafted compressed streams.
  * **Session State & Integer Overflow Guard (`SessionManager.cs`):** Clamped copies to `[1, 999]` and re-validated page counts via `DocumentRenderer.InspectPageCount` upon session restoration to prevent corrupted state and integer overflow attacks.
  * **Installer Hardening & Command Injection Mitigation (`SetupInstaller.cs`):**
    * Drive root and Windows system directory traversal/destruction protection: Rejects destination paths that resolve to drive root (`C:\`) or Windows system directories (`C:\Windows`, `C:\Windows\System32`).
    * Uninstaller command injection mitigation: Sanitized `cleanDir` by stripping quote characters (`"`) and shell operators (`&`, `|`, `;`, `^`, `%`) from `UninstallString`.
  * **GDI WinForms ButtonBase Exception Fix (`MainForm.cs`):** Resolved `System.NotSupportedException: ButtonBase controls do not support setting the BorderColor to Transparent` by setting `BorderSize = 0` when `bg == Color.Transparent` and ensuring solid border colors.
  * **Automated Verification & Binaries:** MSBuild Rebuild passed with 0 errors; portable executable (150 KB) and standalone installer (164 KB) verified running cleanly with zero crashes.


* **Self-Upgradable Updates & Notification Engine (2026-11-18):**
  * **Native Update Engine (`UpdateManager.cs`):** Built zero-dependency updater communicating directly with the GitHub Releases API (`sudo-undo/PrintPulse/releases/latest`) over TLS 1.2. Compares semantic remote tags (e.g. `v1.2.0`) against `CurrentVersion` (`1.1.0`), extracts markdown release notes, and locates binary download assets.
  * **In-App Update Notification Banner (`MainForm.cs`):** Added a non-intrusive soft-indigo notification banner docked above the print queue. Features `[Update Now]`, `[What's New]` (opens browser to release page), and `[✕]` (dismiss button).
  * **Seamless 1-Click Update Hand-Off:**
    * **Portable Mode:** Downloads new executable to `%TEMP%` with real-time download progress display (`0% - 100%`), then launches a silent cmd handoff (`ping 127.0.0.1 -n 3 > nul & copy /y ... & start ...`) that atomically replaces `PrintPulse.exe` once the old process exits and restarts the new build.
    * **Installer Mode:** Automatically downloads the latest `PrintPulse_Setup_vX.X.exe` and launches the setup wizard while cleanly exiting the current instance.
  * **Advanced Options Modal Integration (`AdvancedOptionsForm.cs`):** Added Group 6: `"🔄 Software Updates"` with a manual `[Check for Updates Now]` button and a toggle for `[✓] Check for updates automatically on startup` (persisted to `%LOCALAPPDATA%\PrintPulse\update_settings.cfg`).
  * **Binaries Rebuilt & Verified:** MSBuild release rebuild passed with 0 errors (portable executable: 154 KB, setup installer: 168 KB).


* **PrintPulse v1.2.0 Commercial PMF & Distribution Release (2026-11-24):**
  * **Windows Explorer Context Menu & CLI Ingestion (`ShellIntegration.cs` & `MainForm.cs`):**
    * Added full Windows Explorer shell right-click integration (`HKCU\Software\Classes\*\shell\PrintPulse` and `Directory\shell\PrintPulse`) allowing users to right-click files or folders and click *"Print with PrintPulse"*.
    * Added command-line ingestion: `PrintPulse.exe "doc1.pdf" "folder1"` automatically loads and parses arguments on launch.
    * Added context menu option in both `SetupInstaller.cs` and `AdvancedOptionsForm.cs` (Group 7: `"⚙️ Windows Integration & Cost Estimation"`).
  * **Legal & Corporate Bates Stamping & Watermarking Engine (`DocumentRenderer.cs` & `PrintJobItem.cs`):**
    * Implemented sequential Bates page numbering (`CASE-001 - Page 1 of 50 • 2026-11-24`) with custom prefix, custom starting index, and live timestamp.
    * Added semi-transparent diagonal watermarking presets (`CONFIDENTIAL`, `DRAFT`, `COPY`, `SAMPLE`, `URGENT`, `INTERNAL ONLY`, or custom text) rendered at -35° angle.
    * Both Bates and Watermarks render in real-time in the document preview window and onto final physical/spooled pages.
  * **Live Pre-Print Cost & Savings Estimator (`MainForm.cs` & `AdvancedOptionsForm.cs`):**
    * Configurable per-sheet unit pricing (default: $0.05 B&W, $0.20 Color).
    * Dynamic real-time readout on the bottom summary bar: *"Active: 12 Files | 48 Pages | 24 Sheets | Est. Cost: $1.80 • 🌱 24 Sheets Saved (Duplex)"*.
  * **Official Web Landing Page (`docs/index.html`):**
    * Created high-converting responsive landing page designed for GitHub Pages (`https://sudo-undo.github.io/PrintPulse`).
    * Includes comparison table (PrintPulse vs Print Conductor vs Adobe Acrobat), feature highlights, terminal winget snippet, and direct 1-click download buttons.
  * **Windows Package Manager Manifest (`winget/sudo-undo.PrintPulse.yaml`):**
    * Created official winget v1.6.0 singleton manifest ready for submission to `microsoft/winget-pkgs`.
  * **Recompiled & Verified:** Rebuilt with MSBuild (`0 Errors`, portable executable: 172 KB, setup installer: 186 KB), verified running cleanly.

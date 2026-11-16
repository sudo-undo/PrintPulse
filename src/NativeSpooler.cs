using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PrintPulse
{
    public static class NativeSpooler
    {
        [Flags]
        public enum PrinterEnumFlags : uint
        {
            PRINTER_ENUM_DEFAULT = 0x00000001,
            PRINTER_ENUM_LOCAL = 0x00000002,
            PRINTER_ENUM_CONNECTIONS = 0x00000004,
            PRINTER_ENUM_FAVORITE = 0x00000008,
            PRINTER_ENUM_NAME = 0x00000010,
            PRINTER_ENUM_REMOTE = 0x00000020,
            PRINTER_ENUM_SHARED = 0x00000040,
            PRINTER_ENUM_NETWORK = 0x00000080
        }

        [Flags]
        public enum JobStatusFlags : uint
        {
            JOB_STATUS_PAUSED = 0x00000001,
            JOB_STATUS_ERROR = 0x00000002,
            JOB_STATUS_DELETING = 0x00000004,
            JOB_STATUS_SPOOLING = 0x00000008,
            JOB_STATUS_PRINTING = 0x00000010,
            JOB_STATUS_OFFLINE = 0x00000020,
            JOB_STATUS_PAPEROUT = 0x00000040,
            JOB_STATUS_PRINTED = 0x00000080,
            JOB_STATUS_DELETED = 0x00000100,
            JOB_STATUS_BLOCKED_DEVQ = 0x00000200,
            JOB_STATUS_USER_INTERVENTION = 0x00000400,
            JOB_STATUS_RESTART = 0x00000800,
            JOB_STATUS_COMPLETE = 0x00001000,
            JOB_STATUS_RETAINED = 0x00002000,
            JOB_STATUS_RENDERING_LOCALLY = 0x00004000
        }

        [Flags]
        public enum PrinterStatusFlags : uint
        {
            PRINTER_STATUS_PAUSED = 0x00000001,
            PRINTER_STATUS_ERROR = 0x00000002,
            PRINTER_STATUS_PENDING_DELETION = 0x00000004,
            PRINTER_STATUS_PAPER_JAM = 0x00000008,
            PRINTER_STATUS_PAPER_OUT = 0x00000010,
            PRINTER_STATUS_MANUAL_FEED = 0x00000020,
            PRINTER_STATUS_PAPER_PROBLEM = 0x00000040,
            PRINTER_STATUS_OFFLINE = 0x00000080,
            PRINTER_STATUS_IO_ACTIVE = 0x00000100,
            PRINTER_STATUS_BUSY = 0x00000200,
            PRINTER_STATUS_PRINTING = 0x00000400,
            PRINTER_STATUS_OUTPUT_BIN_FULL = 0x00000800,
            PRINTER_STATUS_NOT_AVAILABLE = 0x00001000,
            PRINTER_STATUS_WAITING = 0x00002000,
            PRINTER_STATUS_PROCESSING = 0x00004000,
            PRINTER_STATUS_INITIALIZING = 0x00008000,
            PRINTER_STATUS_WARMING_UP = 0x00010000,
            PRINTER_STATUS_TONER_LOW = 0x00020000,
            PRINTER_STATUS_NO_TONER = 0x00040000,
            PRINTER_STATUS_PAGE_PUNT = 0x00080000,
            PRINTER_STATUS_USER_INTERVENTION = 0x00100000,
            PRINTER_STATUS_OUT_OF_MEMORY = 0x00200000,
            PRINTER_STATUS_DOOR_OPEN = 0x00400000,
            PRINTER_STATUS_SERVER_UNKNOWN = 0x00800000,
            PRINTER_STATUS_POWER_SAVE = 0x01000000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PRINTER_INFO_2
        {
            public string pServerName;
            public string pPrinterName;
            public string pShareName;
            public string pPortName;
            public string pDriverName;
            public string pComment;
            public string pLocation;
            public IntPtr pDevMode;
            public string pSepFile;
            public string pPrintProcessor;
            public string pDatatype;
            public string pParameters;
            public IntPtr pSecurityDescriptor;
            public uint Attributes;
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint cJobs;
            public uint AveragePPM;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct JOB_INFO_2
        {
            public uint JobId;
            public string pPrinterName;
            public string pMachineName;
            public string pUserName;
            public string pDocument;
            public string pNotifyName;
            public string pDatatype;
            public string pPrintProcessor;
            public string pParameters;
            public string pDriverName;
            public IntPtr pDevMode;
            public uint Status;
            public uint Priority;
            public uint Position;
            public uint StartTime;
            public uint UntilTime;
            public uint TotalPages;
            public uint Size;
            public IntPtr Submitted;
            public uint Time;
            public uint PagesPrinted;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool EnumPrinters(
            uint Flags,
            string Name,
            uint Level,
            IntPtr pPrinterEnum,
            uint cbBuf,
            out uint pcbNeeded,
            out uint pcReturned);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GetPrinter(
            IntPtr hPrinter,
            uint Level,
            IntPtr pPrinter,
            uint cbBuf,
            out uint pcbNeeded);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool EnumJobs(
            IntPtr hPrinter,
            uint FirstJob,
            uint NoJobs,
            uint Level,
            IntPtr pJob,
            uint cbBuf,
            out uint pcbNeeded,
            out uint pcReturned);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetJob(
            IntPtr hPrinter,
            uint JobId,
            uint Level,
            IntPtr pJob,
            uint Command);

        public const uint JOB_CONTROL_PAUSE = 1;
        public const uint JOB_CONTROL_RESUME = 2;
        public const uint JOB_CONTROL_CANCEL = 3;
        public const uint JOB_CONTROL_RESTART = 4;
        public const uint JOB_CONTROL_DELETE = 5;

        public class PrinterDevice
        {
            public string Name { get; set; }
            public string Port { get; set; }
            public string Driver { get; set; }
            public bool IsDefault { get; set; }
            public bool IsNetwork { get; set; }
            public uint Status { get; set; }
            public uint JobCount { get; set; }

            public string StatusDescription
            {
                get
                {
                    return FormatStatusFlags(Status);
                }
            }

            public override string ToString()
            {
                return Name + (IsDefault ? " (Default)" : "") + " [" + StatusDescription + "]";
            }
        }

        public static List<PrinterDevice> GetInstalledPrinters()
        {
            List<PrinterDevice> list = new List<PrinterDevice>();
            uint flags = (uint)(PrinterEnumFlags.PRINTER_ENUM_LOCAL | PrinterEnumFlags.PRINTER_ENUM_CONNECTIONS);
            uint cbNeeded = 0;
            uint cReturned = 0;

            EnumPrinters(flags, null, 2, IntPtr.Zero, 0, out cbNeeded, out cReturned);
            if (cbNeeded == 0) return list;

            IntPtr pAddr = Marshal.AllocHGlobal((int)cbNeeded);
            try
            {
                if (EnumPrinters(flags, null, 2, pAddr, cbNeeded, out cbNeeded, out cReturned))
                {
                    int structSize = Marshal.SizeOf(typeof(PRINTER_INFO_2));
                    for (int i = 0; i < cReturned; i++)
                    {
                        IntPtr current = new IntPtr(pAddr.ToInt64() + (i * structSize));
                        PRINTER_INFO_2 info = (PRINTER_INFO_2)Marshal.PtrToStructure(current, typeof(PRINTER_INFO_2));
                        
                        PrinterDevice dev = new PrinterDevice
                        {
                            Name = info.pPrinterName,
                            Port = info.pPortName,
                            Driver = info.pDriverName,
                            Status = info.Status,
                            JobCount = info.cJobs,
                            IsNetwork = (info.Attributes & (uint)PrinterEnumFlags.PRINTER_ENUM_NETWORK) != 0 || (info.pPortName != null && info.pPortName.StartsWith("IP_"))
                        };
                        list.Add(dev);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pAddr);
            }

            // Detect default printer via System.Drawing.Printing
            try
            {
                System.Drawing.Printing.PrinterSettings ps = new System.Drawing.Printing.PrinterSettings();
                string defaultName = ps.PrinterName;
                foreach (var p in list)
                {
                    if (string.Equals(p.Name, defaultName, StringComparison.OrdinalIgnoreCase))
                    {
                        p.IsDefault = true;
                    }
                }
            }
            catch { }

            return list;
        }

        public static string FormatStatusFlags(uint status)
        {
            if (status == 0) return "Ready";
            List<string> issues = new List<string>();
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_PAPER_JAM) != 0) issues.Add("Paper Jam");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_PAPER_OUT) != 0) issues.Add("Out of Paper");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_OFFLINE) != 0) issues.Add("Offline");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_DOOR_OPEN) != 0) issues.Add("Door Open");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_NO_TONER) != 0) issues.Add("No Toner");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_TONER_LOW) != 0) issues.Add("Low Toner");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_USER_INTERVENTION) != 0) issues.Add("User Action Needed");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_PAUSED) != 0) issues.Add("Paused");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_BUSY) != 0) issues.Add("Busy");
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_PRINTING) != 0) issues.Add("Printing");
            return issues.Count > 0 ? string.Join(", ", issues.ToArray()) : "Active / Status: " + status;
        }

        public static uint GetPrinterStatus(string printerName)
        {
            IntPtr hPrinter = IntPtr.Zero;
            try
            {
                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                    return (uint)PrinterStatusFlags.PRINTER_STATUS_NOT_AVAILABLE;

                uint cbNeeded = 0;
                GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out cbNeeded);
                if (cbNeeded == 0) return 0;

                IntPtr pBuf = Marshal.AllocHGlobal((int)cbNeeded);
                try
                {
                    if (GetPrinter(hPrinter, 2, pBuf, cbNeeded, out cbNeeded))
                    {
                        PRINTER_INFO_2 info = (PRINTER_INFO_2)Marshal.PtrToStructure(pBuf, typeof(PRINTER_INFO_2));
                        return info.Status;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pBuf);
                }
            }
            finally
            {
                if (hPrinter != IntPtr.Zero)
                    ClosePrinter(hPrinter);
            }
            return 0;
        }

        public static bool CheckHardwarePreflight(string printerName, out string warning)
        {
            warning = string.Empty;
            uint status = GetPrinterStatus(printerName);

            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_OFFLINE) != 0)
            {
                warning = "Printer is currently Offline. Check power and USB/network cables.";
                return false;
            }
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_DOOR_OPEN) != 0)
            {
                warning = "Printer cover/door is open. Please close it before printing.";
                return false;
            }
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_PAPER_OUT) != 0)
            {
                warning = "Printer is out of paper. Please load paper into the tray.";
                return false;
            }
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_PAPER_JAM) != 0)
            {
                warning = "Printer reports a paper jam. Please clear the paper path.";
                return false;
            }
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_NOT_AVAILABLE) != 0)
            {
                warning = "Printer is not available or not responding.";
                return false;
            }
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_ERROR) != 0)
            {
                warning = "Printer reports a hardware error state.";
                return false;
            }
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_NO_TONER) != 0)
            {
                warning = "Printer is out of ink/toner. Replace cartridge to proceed.";
                return false;
            }
            if ((status & (uint)PrinterStatusFlags.PRINTER_STATUS_TONER_LOW) != 0)
            {
                warning = "Warning: Printer toner is low.";
                // Warning only, can still proceed
                return true;
            }

            return true;
        }

        public static bool CanPrinterDuplex(string printerName)
        {
            try
            {
                System.Drawing.Printing.PrinterSettings ps = new System.Drawing.Printing.PrinterSettings();
                ps.PrinterName = printerName;
                return ps.CanDuplex;
            }
            catch { return false; }
        }

        public static List<string> GetPaperSources(string printerName)
        {
            List<string> sources = new List<string>();
            sources.Add("Auto / Default");
            try
            {
                System.Drawing.Printing.PrinterSettings ps = new System.Drawing.Printing.PrinterSettings();
                ps.PrinterName = printerName;
                foreach (System.Drawing.Printing.PaperSource src in ps.PaperSources)
                {
                    if (!sources.Contains(src.SourceName))
                        sources.Add(src.SourceName);
                }
            }
            catch { }
            return sources;
        }

        public static List<JOB_INFO_2> GetActiveJobs(string printerName)
        {
            List<JOB_INFO_2> jobs = new List<JOB_INFO_2>();
            IntPtr hPrinter = IntPtr.Zero;
            try
            {
                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                    return jobs;

                uint cbNeeded = 0;
                uint cReturned = 0;
                EnumJobs(hPrinter, 0, 100, 2, IntPtr.Zero, 0, out cbNeeded, out cReturned);
                if (cbNeeded == 0) return jobs;

                IntPtr pBuf = Marshal.AllocHGlobal((int)cbNeeded);
                try
                {
                    if (EnumJobs(hPrinter, 0, 100, 2, pBuf, cbNeeded, out cbNeeded, out cReturned))
                    {
                        int structSize = Marshal.SizeOf(typeof(JOB_INFO_2));
                        for (int i = 0; i < cReturned; i++)
                        {
                            IntPtr current = new IntPtr(pBuf.ToInt64() + (i * structSize));
                            JOB_INFO_2 job = (JOB_INFO_2)Marshal.PtrToStructure(current, typeof(JOB_INFO_2));
                            jobs.Add(job);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pBuf);
                }
            }
            finally
            {
                if (hPrinter != IntPtr.Zero)
                    ClosePrinter(hPrinter);
            }
            return jobs;
        }
    }
}

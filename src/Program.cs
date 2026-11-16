using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PrintPulse
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    SetProcessDPIAware();
                }
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) =>
            {
                TelemetryService.TrackError("ThreadException", e.Exception.Message, null, e.Exception.StackTrace);
                MessageBox.Show("An unexpected error occurred:\n" + e.Exception.Message, "PrintPulse", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                TelemetryService.TrackError("DomainCrash", e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Unknown crash", null, null);
                MessageBox.Show("Fatal unhandled domain error:\n" + e.ExceptionObject, "PrintPulse Fatal", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                TelemetryService.TrackError("StartupCrash", ex.Message, null, ex.StackTrace);
                MessageBox.Show("Fatal startup error:\n" + ex.ToString(), "PrintPulse Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrintPulse
{
    public static class ShellIntegration
    {
        private const string ShellKeyPath = @"Software\Classes\*\shell\PrintPulse";
        private const string DirectoryShellKeyPath = @"Software\Classes\Directory\shell\PrintPulse";

        public static bool IsContextMenuRegistered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ShellKeyPath))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool RegisterContextMenu()
        {
            try
            {
                string exePath = Application.ExecutablePath;

                // 1. Register for individual/multiple files
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ShellKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("", "Print with PrintPulse");
                        key.SetValue("Icon", "\"" + exePath + "\",0");
                        using (RegistryKey cmdKey = key.CreateSubKey("command"))
                        {
                            if (cmdKey != null)
                            {
                                cmdKey.SetValue("", "\"" + exePath + "\" \"%1\"");
                            }
                        }
                    }
                }

                // 2. Register for directories / folders
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(DirectoryShellKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("", "Batch Print Folder with PrintPulse");
                        key.SetValue("Icon", "\"" + exePath + "\",0");
                        using (RegistryKey cmdKey = key.CreateSubKey("command"))
                        {
                            if (cmdKey != null)
                            {
                                cmdKey.SetValue("", "\"" + exePath + "\" \"%1\"");
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                TelemetryService.TrackError("ShellRegisterError", ex.Message);
                return false;
            }
        }

        public static bool UnregisterContextMenu()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(ShellKeyPath, false);
                Registry.CurrentUser.DeleteSubKeyTree(DirectoryShellKeyPath, false);
                return true;
            }
            catch (Exception ex)
            {
                TelemetryService.TrackError("ShellUnregisterError", ex.Message);
                return false;
            }
        }
    }
}

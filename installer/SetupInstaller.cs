using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrintPulseInstaller
{
    public class SetupForm : Form
    {
        private TextBox _txtInstallDir;
        private CheckBox _chkDesktop;
        private CheckBox _chkStartMenu;
        private CheckBox _chkLaunch;
        private Button _btnInstall;
        private ProgressBar _prog;
        private Label _lblStatus;

        public SetupForm()
        {
            InitUI();
        }

        private void InitUI()
        {
            this.Text = "PrintPulse Setup Wizard";
            this.Size = new Size(520, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Banner
            Panel banner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 75,
                BackColor = Color.FromArgb(79, 70, 229)
            };

            Label lblTitle = new Label
            {
                Text = "Install PrintPulse v1.0",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 15),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = "Universal Batch Print Studio (Windows XP to 11)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(224, 231, 255),
                Location = new Point(22, 42),
                AutoSize = true
            };

            banner.Controls.Add(lblTitle);
            banner.Controls.Add(lblSub);

            // Installation Path
            Label lblDir = new Label
            {
                Text = "Destination Folder:",
                Location = new Point(25, 95),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\PrintPulse");
            _txtInstallDir = new TextBox
            {
                Text = defaultPath,
                Location = new Point(25, 120),
                Size = new Size(360, 25)
            };

            Button btnBrowse = new Button
            {
                Text = "Browse...",
                Location = new Point(395, 118),
                Size = new Size(85, 27),
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat
            };
            btnBrowse.Click += (s, e) =>
            {
                FolderBrowserDialog fbd = new FolderBrowserDialog { SelectedPath = _txtInstallDir.Text };
                if (fbd.ShowDialog() == DialogResult.OK)
                    _txtInstallDir.Text = fbd.SelectedPath;
            };

            // Options
            GroupBox grp = new GroupBox
            {
                Text = " Shortcuts & Preferences ",
                Location = new Point(25, 160),
                Size = new Size(455, 110),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            _chkDesktop = new CheckBox
            {
                Text = "Create a Desktop shortcut",
                Location = new Point(15, 25),
                Size = new Size(300, 24),
                Checked = true
            };

            _chkStartMenu = new CheckBox
            {
                Text = "Create a Start Menu shortcut",
                Location = new Point(15, 50),
                Size = new Size(300, 24),
                Checked = true
            };

            _chkLaunch = new CheckBox
            {
                Text = "Launch PrintPulse after installation completes",
                Location = new Point(15, 75),
                Size = new Size(320, 24),
                Checked = true
            };

            grp.Controls.Add(_chkDesktop);
            grp.Controls.Add(_chkStartMenu);
            grp.Controls.Add(_chkLaunch);

            // Progress & Status
            _lblStatus = new Label
            {
                Text = "Click Install to begin setup.",
                Location = new Point(25, 280),
                Size = new Size(455, 20),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            _prog = new ProgressBar
            {
                Location = new Point(25, 305),
                Size = new Size(455, 18),
                Style = ProgressBarStyle.Continuous
            };

            // Buttons
            _btnInstall = new Button
            {
                Text = "Install Now",
                Location = new Point(275, 335),
                Size = new Size(110, 32),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Click += (s, e) => PerformInstall();

            Button btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(395, 335),
                Size = new Size(85, 32),
                BackColor = Color.FromArgb(241, 245, 249),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(banner);
            this.Controls.Add(lblDir);
            this.Controls.Add(_txtInstallDir);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(grp);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(_prog);
            this.Controls.Add(_btnInstall);
            this.Controls.Add(btnCancel);
        }

        private void PerformInstall()
        {
            string targetDir = _txtInstallDir.Text.Trim();
            if (string.IsNullOrEmpty(targetDir)) return;

            _btnInstall.Enabled = false;
            _prog.Value = 20;
            _lblStatus.Text = "Creating installation directory...";

            try
            {
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                _prog.Value = 50;
                _lblStatus.Text = "Installing application files...";

                string targetExe = Path.Combine(targetDir, "PrintPulse.exe");
                
                // Copy payload from bundled resource or adjacent portable
                byte[] payload = ExtractPayloadBytes();
                File.WriteAllBytes(targetExe, payload);

                _prog.Value = 75;
                _lblStatus.Text = "Creating shortcuts...";

                if (_chkDesktop.Checked)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktop, "PrintPulse.lnk"), targetExe, "PrintPulse Batch Print Studio");
                }

                if (_chkStartMenu.Checked)
                {
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                    CreateShortcut(Path.Combine(startMenu, "PrintPulse.lnk"), targetExe, "PrintPulse Batch Print Studio");
                }

                // Register Uninstall entry in Windows Registry
                RegisterUninstall(targetDir, targetExe);

                _prog.Value = 100;
                _lblStatus.Text = "Installation complete!";

                if (_chkLaunch.Checked && File.Exists(targetExe))
                {
                    Process.Start(targetExe);
                }

                MessageBox.Show("PrintPulse has been installed successfully!", "Installation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Installation failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnInstall.Enabled = true;
            }
        }

        private byte[] ExtractPayloadBytes()
        {
            // First check embedded resource
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("PrintPulse.exe", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream s = asm.GetManifestResourceStream(name))
                    {
                        byte[] data = new byte[s.Length];
                        s.Read(data, 0, data.Length);
                        return data;
                    }
                }
            }

            // Fallback: look adjacent in portable folder or current directory
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PrintPulse.exe");
            if (File.Exists(localPath)) return File.ReadAllBytes(localPath);

            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\Portable\\PrintPulse.exe");
            if (File.Exists(portablePath)) return File.ReadAllBytes(portablePath);

            throw new FileNotFoundException("Could not locate PrintPulse.exe payload.");
        }

        private void CreateShortcut(string shortcutPath, string targetExe, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetExe;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
                    shortcut.Description = description;
                    shortcut.Save();
                }
            }
            catch { }
        }

        private void RegisterUninstall(string installDir, string exePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\PrintPulse"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "PrintPulse (Batch Print Studio)");
                        key.SetValue("DisplayVersion", "1.0.0");
                        key.SetValue("Publisher", "PrintPulse Technologies");
                        key.SetValue("DisplayIcon", exePath);
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("UninstallString", "cmd.exe /c rd /s /q \"" + installDir + "\"");
                    }
                }
            }
            catch { }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}

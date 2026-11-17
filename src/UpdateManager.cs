using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace PrintPulse
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; }
        public string CurrentVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string ReleasePageUrl { get; set; }
        public string DownloadUrl { get; set; }
        public long FileSizeBytes { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class UpdateManager
    {
        public const string CurrentVersion = "1.1.0";
        public const string GitHubRepo = "sudo-undo/PrintPulse";
        public const string GitHubReleasesApiUrl = "https://api.github.com/repos/sudo-undo/PrintPulse/releases/latest";
        public const string GitHubReleasesHtmlUrl = "https://github.com/sudo-undo/PrintPulse/releases/latest";

        private static string SettingsFilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrintPulse");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "update_settings.cfg");
            }
        }

        public static bool CheckOnStartup
        {
            get
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        string txt = File.ReadAllText(SettingsFilePath).Trim();
                        bool val;
                        if (bool.TryParse(txt, out val)) return val;
                    }
                }
                catch { }
                return true; // Enabled by default
            }
            set
            {
                try
                {
                    File.WriteAllText(SettingsFilePath, value.ToString());
                }
                catch { }
            }
        }

        public static void CheckForUpdatesAsync(Control syncContext, Action<UpdateInfo> callback, bool isUserInitiated)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                UpdateInfo info = new UpdateInfo
                {
                    CurrentVersion = CurrentVersion,
                    LatestVersion = CurrentVersion,
                    IsUpdateAvailable = false,
                    ReleasePageUrl = GitHubReleasesHtmlUrl
                };

                try
                {
                    // Enable TLS 1.2
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | SecurityProtocolType.Tls;

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(GitHubReleasesApiUrl);
                    req.Method = "GET";
                    req.UserAgent = "PrintPulse-Updater/" + CurrentVersion;
                    req.Accept = "application/vnd.github.v3+json";
                    req.Timeout = 10000;

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();

                        // Parse tag_name
                        Match mTag = Regex.Match(json, @"""tag_name""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                        if (mTag.Success)
                        {
                            string remoteTag = mTag.Groups[1].Value.Trim().TrimStart('v', 'V');
                            info.LatestVersion = remoteTag;

                            if (IsNewerVersion(remoteTag, CurrentVersion))
                            {
                                info.IsUpdateAvailable = true;
                            }
                        }

                        // Parse release page html_url
                        Match mHtml = Regex.Match(json, @"""html_url""\s*:\s*""(https:[^""]+)""", RegexOptions.IgnoreCase);
                        if (mHtml.Success)
                        {
                            info.ReleasePageUrl = mHtml.Groups[1].Value;
                        }

                        // Parse body (release notes)
                        Match mBody = Regex.Match(json, @"""body""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.IgnoreCase);
                        if (mBody.Success)
                        {
                            string rawBody = mBody.Groups[1].Value;
                            info.ReleaseNotes = UnescapeJson(rawBody);
                        }

                        // Parse binary download asset (prefer installer or portable .exe)
                        MatchCollection mAssets = Regex.Matches(json, @"""browser_download_url""\s*:\s*""(https:[^""]+\.exe)""", RegexOptions.IgnoreCase);
                        if (mAssets.Count > 0)
                        {
                            // If running from an installer directory, prefer setup; else prefer portable or first exe
                            string bestUrl = mAssets[0].Groups[1].Value;
                            foreach (Match assetMatch in mAssets)
                            {
                                string url = assetMatch.Groups[1].Value;
                                if (url.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    bestUrl = url;
                                    break;
                                }
                            }
                            info.DownloadUrl = bestUrl;
                        }
                    }
                }
                catch (WebException wex)
                {
                    info.ErrorMessage = "Unable to connect to update server: " + wex.Message;
                }
                catch (Exception ex)
                {
                    info.ErrorMessage = ex.Message;
                }

                if (syncContext != null && syncContext.IsHandleCreated && !syncContext.IsDisposed)
                {
                    syncContext.BeginInvoke(new Action(() => callback(info)));
                }
                else
                {
                    callback(info);
                }
            });
        }

        public static void DownloadAndApplyUpdate(Control syncContext, UpdateInfo info, Action<int> progressCallback, Action<string> completeCallback, Action<string> errorCallback)
        {
            if (string.IsNullOrEmpty(info.DownloadUrl))
            {
                // Fallback to opening GitHub release page if no direct asset URL found
                try
                {
                    Process.Start(info.ReleasePageUrl);
                    if (completeCallback != null) completeCallback("Opened browser to release download page.");
                }
                catch (Exception ex)
                {
                    if (errorCallback != null) errorCallback(ex.Message);
                }
                return;
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | SecurityProtocolType.Tls;

                    string ext = Path.GetExtension(info.DownloadUrl);
                    if (string.IsNullOrEmpty(ext)) ext = ".exe";

                    string tempUpdateFile = Path.Combine(Path.GetTempPath(), "PrintPulse_Update_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext);

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(info.DownloadUrl);
                    req.Method = "GET";
                    req.UserAgent = "PrintPulse-Updater/" + CurrentVersion;
                    req.Timeout = 60000;

                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        long totalBytes = resp.ContentLength;
                        using (Stream input = resp.GetResponseStream())
                        using (FileStream output = new FileStream(tempUpdateFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[16384];
                            int read;
                            long totalRead = 0;
                            int lastPercent = -1;

                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                output.Write(buffer, 0, read);
                                totalRead += read;

                                if (totalBytes > 0)
                                {
                                    int pct = (int)((totalRead * 100) / totalBytes);
                                    if (pct != lastPercent)
                                    {
                                        lastPercent = pct;
                                        if (syncContext != null && syncContext.IsHandleCreated)
                                        {
                                            syncContext.BeginInvoke(new Action(() => progressCallback(pct)));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Download complete, now launch the update
                    if (syncContext != null && syncContext.IsHandleCreated)
                    {
                        syncContext.BeginInvoke(new Action(() =>
                        {
                            ApplyDownloadedUpdate(tempUpdateFile);
                        }));
                    }
                    else
                    {
                        ApplyDownloadedUpdate(tempUpdateFile);
                    }
                }
                catch (Exception ex)
                {
                    if (syncContext != null && syncContext.IsHandleCreated)
                    {
                        syncContext.BeginInvoke(new Action(() => errorCallback("Download failed: " + ex.Message)));
                    }
                }
            });
        }

        private static void ApplyDownloadedUpdate(string tempFilePath)
        {
            try
            {
                string currentExe = Application.ExecutablePath;
                string currentDir = Application.StartupPath;

                // Check if the downloaded file is an installer
                if (tempFilePath.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempFilePath,
                        UseShellExecute = true
                    });
                    Application.Exit();
                    return;
                }

                // If portable, replace the executable using a clean asynchronous cmd handoff
                string batchCommand = string.Format(
                    "ping 127.0.0.1 -n 3 > nul & copy /y \"{0}\" \"{1}\" & start \"\" \"{1}\"",
                    tempFilePath,
                    currentExe
                );

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + batchCommand,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = currentDir
                };

                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to automatically launch update: " + ex.Message + "\n\nYou can manually run: " + tempFilePath, "Update Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static bool IsNewerVersion(string remoteVer, string localVer)
        {
            try
            {
                Version remote = new Version(NormalizeVersion(remoteVer));
                Version local = new Version(NormalizeVersion(localVer));
                return remote > local;
            }
            catch
            {
                return string.Compare(remoteVer, localVer, StringComparison.OrdinalIgnoreCase) > 0;
            }
        }

        private static string NormalizeVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return "0.0.0.0";
            v = v.Trim().TrimStart('v', 'V');
            string[] parts = v.Split('.');
            if (parts.Length == 1) return v + ".0.0.0";
            if (parts.Length == 2) return v + ".0.0";
            if (parts.Length == 3) return v + ".0";
            return v;
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
        }
    }
}

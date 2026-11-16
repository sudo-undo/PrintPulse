using System;
using System.IO;
using System.Threading;

namespace PrintPulse
{
    public class WatchFolderService : IDisposable
    {
        private FileSystemWatcher _watcher;
        private string _watchPath;
        public bool IsRunning { get; private set; }

        public event Action<string> FileDropped;

        public string WatchPath
        {
            get { return _watchPath; }
        }

        public void Start(string path)
        {
            Stop();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            _watchPath = path;
            _watcher = new FileSystemWatcher(_watchPath);
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            _watcher.Created += OnFileCreated;
            _watcher.EnableRaisingEvents = true;
            IsRunning = true;
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileCreated;
                _watcher.Dispose();
                _watcher = null;
            }
            IsRunning = false;
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext == ".pdf" || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" ||
                ext == ".tif" || ext == ".tiff" || ext == ".docx" || ext == ".txt" || ext == ".csv" ||
                ext == ".log" || ext == ".xml" || ext == ".html")
            {
                // Give file lock a second to release (Windows file copy latency)
                ThreadPool.QueueUserWorkItem(delegate
                {
                    int retries = 10;
                    while (retries-- > 0)
                    {
                        try
                        {
                            using (FileStream fs = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                if (fs.Length > 0) break;
                            }
                        }
                        catch { Thread.Sleep(500); }
                    }

                    Action<string> handler = FileDropped;
                    if (handler != null && File.Exists(e.FullPath))
                    {
                        handler(e.FullPath);
                    }
                });
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PrintPulse
{
    public static class TelemetryService
    {
        // Production Direct Ingestion Endpoint (routes directly to developer inbox)
        public const string TargetFeedbackEmail = "hs123253@gmail.com";
        public static string FeedbackEndpointUrl = "https://formsubmit.co/ajax/" + TargetFeedbackEmail;
        
        // Sentry DSN endpoint
        public static string SentryDsnEndpoint = "https://628e8b818e6c0337ffa686d9ef422bae@o4512439853449216.ingest.us.sentry.io/4512441183502336"; 
        
        public static bool IsTelemetryEnabled = true;

        /// <summary>
        /// Submits user feedback directly over HTTPS to the developer without needing an email app.
        /// </summary>
        public static bool SubmitFeedback(string category, string userEmail, string subject, string message, out string responseMessage)
        {
            responseMessage = string.Empty;
            try
            {
                EnableSecureTls();

                string payload = string.Format(
                    "{{\"_subject\":\"[PrintPulse Feedback - {0}] {1}\",\"category\":\"{0}\",\"email\":\"{2}\",\"message\":\"{3}\",\"os_version\":\"{4}\",\"app_version\":\"1.0.0\"}}",
                    EscapeJson(category),
                    EscapeJson(subject),
                    EscapeJson(string.IsNullOrEmpty(userEmail) ? "anonymous@user.com" : userEmail),
                    EscapeJson(message),
                    EscapeJson(Environment.OSVersion.VersionString)
                );

                byte[] data = Encoding.UTF8.GetBytes(payload);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(FeedbackEndpointUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Referer = "https://printpulse.app";
                request.UserAgent = "PrintPulse/1.0";
                request.ContentLength = data.Length;
                request.Timeout = 12000;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string jsonResponse = reader.ReadToEnd();
                    if (response.StatusCode == HttpStatusCode.OK || jsonResponse.Contains("\"success\":\"true\"") || jsonResponse.Contains("\"success\":true"))
                    {
                        responseMessage = "Feedback submitted successfully!";
                        return true;
                    }
                    else if (jsonResponse.Contains("Activate Form"))
                    {
                        responseMessage = "Check your email inbox (" + TargetFeedbackEmail + ") to click the one-time activation link.";
                        return true;
                    }
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse webRes = wex.Response as HttpWebResponse;
                if (webRes != null)
                {
                    using (StreamReader sr = new StreamReader(webRes.GetResponseStream()))
                    {
                        string body = sr.ReadToEnd();
                        if (body.Contains("Activate Form"))
                        {
                            responseMessage = "Check your email (" + TargetFeedbackEmail + ") for the one-time activation link.";
                            return true;
                        }
                    }
                }
                responseMessage = wex.Message;
                return false;
            }
            catch (Exception ex)
            {
                responseMessage = ex.Message;
                return false;
            }

            return false;
        }

        /// <summary>
        /// Silently logs an anonymous error/crash report to the diagnostic backend and local log file.
        /// Guaranteed not to throw or block the main thread.
        /// </summary>
        public static void TrackError(string errorCategory, string errorMessage, string printerDriver = null, string stackTrace = null)
        {
            if (!IsTelemetryEnabled) return;

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    EnableSecureTls();

                    string scrubbedError = AnonymizePathData(errorMessage);
                    string scrubbedStack = AnonymizePathData(stackTrace ?? string.Empty);

                    string logEntry = string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss UTC}] EVENT: {1} | PRINTER: {2} | OS: {3}\nERROR: {4}\nSTACK: {5}\n------------------------------------------------------------\n",
                        DateTime.UtcNow,
                        errorCategory,
                        printerDriver ?? "Unknown",
                        Environment.OSVersion.VersionString,
                        scrubbedError,
                        scrubbedStack
                    );

                    // 1. Always record to local diagnostic file for immediate developer auditing
                    WriteToLocalDiagnosticLog(logEntry);

                    // 2. Transmit to remote Sentry endpoint if configured
                    if (!string.IsNullOrEmpty(SentryDsnEndpoint))
                    {
                        try
                        {
                            Uri uri = new Uri(SentryDsnEndpoint);
                            string publicKey = uri.UserInfo;
                            string hostName = uri.Host;
                            string projectId = uri.AbsolutePath.Trim('/');
                            string storeUrl = "https://" + hostName + "/api/" + projectId + "/store/";

                            string eventId = Guid.NewGuid().ToString("N");
                            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
                            string authHeader = "Sentry sentry_version=7, sentry_client=PrintPulse/1.1, sentry_key=" + publicKey;

                            string jsonPayload = string.Format(
                                "{{\"event_id\":\"{0}\",\"timestamp\":\"{1}\",\"platform\":\"csharp\",\"level\":\"error\",\"logger\":\"PrintPulse\",\"message\":{{\"formatted\":\"[{2}] {3}\"}},\"tags\":{{\"app\":\"PrintPulse\",\"version\":\"1.1.0\",\"category\":\"{2}\",\"printer\":\"{4}\",\"os\":\"{5}\"}},\"extra\":{{\"stack\":\"{6}\"}}}}",
                                eventId,
                                timestamp,
                                EscapeJson(errorCategory),
                                EscapeJson(scrubbedError),
                                EscapeJson(printerDriver ?? "Unknown"),
                                EscapeJson(Environment.OSVersion.VersionString),
                                EscapeJson(scrubbedStack)
                            );

                            byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(storeUrl);
                            req.Method = "POST";
                            req.ContentType = "application/json";
                            req.Headers.Add("X-Sentry-Auth", authHeader);
                            req.UserAgent = "PrintPulse/1.1";
                            req.ContentLength = data.Length;
                            req.Timeout = 8000;
                            using (Stream s = req.GetRequestStream())
                            {
                                s.Write(data, 0, data.Length);
                            }
                            using (req.GetResponse()) { }
                        }
                        catch { }
                    }
                }
                catch { } // Fail completely silent
            });
        }

        public static string DetectPrinterBrand(string printerName)
        {
            if (string.IsNullOrEmpty(printerName)) return "Generic / Unknown";
            string p = printerName.ToLowerInvariant();
            if (p.Contains("hp") || p.Contains("hewlett")) return "HP";
            if (p.Contains("canon")) return "Canon";
            if (p.Contains("epson")) return "Epson";
            if (p.Contains("brother")) return "Brother";
            if (p.Contains("xerox")) return "Xerox";
            if (p.Contains("samsung")) return "Samsung";
            if (p.Contains("ricoh")) return "Ricoh";
            if (p.Contains("kyocera")) return "Kyocera";
            if (p.Contains("konica") || p.Contains("minolta")) return "Konica Minolta";
            if (p.Contains("toshiba")) return "Toshiba";
            if (p.Contains("panasonic")) return "Panasonic";
            if (p.Contains("pdf") || p.Contains("xps") || p.Contains("onenote")) return "Virtual / Software";
            return "Other";
        }

        public static void TrackUsageHeartbeat(int filesCount, int totalPages, int totalSheets, string printerName, string featuresUsed)
        {
            if (!IsTelemetryEnabled) return;

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    EnableSecureTls();
                    string brand = DetectPrinterBrand(printerName);

                    string logEntry = string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss UTC}] USAGE_HEARTBEAT | Files: {1} | Pages: {2} | Sheets: {3} | Brand: {4} | Features: {5}\n------------------------------------------------------------\n",
                        DateTime.UtcNow,
                        filesCount,
                        totalPages,
                        totalSheets,
                        brand,
                        featuresUsed ?? "Standard"
                    );
                    WriteToLocalDiagnosticLog(logEntry);

                    if (!string.IsNullOrEmpty(SentryDsnEndpoint))
                    {
                        try
                        {
                            Uri uri = new Uri(SentryDsnEndpoint);
                            string publicKey = uri.UserInfo;
                            string hostName = uri.Host;
                            string projectId = uri.AbsolutePath.Trim('/');
                            string storeUrl = "https://" + hostName + "/api/" + projectId + "/store/";

                            string eventId = Guid.NewGuid().ToString("N");
                            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
                            string authHeader = "Sentry sentry_version=7, sentry_client=PrintPulse/1.1, sentry_key=" + publicKey;

                            string jsonPayload = string.Format(
                                "{{\"event_id\":\"{0}\",\"timestamp\":\"{1}\",\"platform\":\"csharp\",\"level\":\"info\",\"logger\":\"PrintPulse.Usage\",\"message\":{{\"formatted\":\"Batch Complete: {2} files, {3} pages spooled on {4}\"}},\"tags\":{{\"app\":\"PrintPulse\",\"version\":\"1.1.0\",\"event_type\":\"usage_heartbeat\",\"printer_brand\":\"{4}\",\"os\":\"{5}\",\"features\":\"{6}\"}},\"extra\":{{\"files_count\":{2},\"pages_printed\":{3},\"sheets_used\":{7}}}}}",
                                eventId,
                                timestamp,
                                filesCount,
                                totalPages,
                                EscapeJson(brand),
                                EscapeJson(Environment.OSVersion.VersionString),
                                EscapeJson(featuresUsed ?? "Standard"),
                                totalSheets
                            );

                            byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(storeUrl);
                            req.Method = "POST";
                            req.ContentType = "application/json";
                            req.Headers.Add("X-Sentry-Auth", authHeader);
                            req.UserAgent = "PrintPulse/1.1";
                            req.ContentLength = data.Length;
                            req.Timeout = 8000;
                            using (Stream s = req.GetRequestStream())
                            {
                                s.Write(data, 0, data.Length);
                            }
                            using (req.GetResponse()) { }
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }

        private static void WriteToLocalDiagnosticLog(string text)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrintPulse\\Logs");
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                string logPath = Path.Combine(logDir, "diagnostics.log");
                File.AppendAllText(logPath, text, Encoding.UTF8);
            }
            catch { }
        }

        private static void EnableSecureTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | SecurityProtocolType.Tls;
            }
            catch { }
        }

        private static string AnonymizePathData(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string scrubbed = Regex.Replace(text, @"([a-zA-Z]:\\Users\\)[^\\]+(\\?)", "$1[REDACTED]$2", RegexOptions.IgnoreCase);
            return scrubbed;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }
    }
}

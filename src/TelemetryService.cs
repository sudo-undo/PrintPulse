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
        
        // Sentry DSN endpoint (Paste your free Sentry project DSN URL here)
        public static string SentryDsnEndpoint = ""; 
        
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
                        string jsonPayload = string.Format(
                            "{{\"event\":\"error\",\"category\":\"{0}\",\"message\":\"{1}\",\"printer\":\"{2}\",\"os\":\"{3}\",\"app_version\":\"1.0.0\",\"timestamp\":\"{4}\"}}",
                            EscapeJson(errorCategory),
                            EscapeJson(scrubbedError),
                            EscapeJson(printerDriver ?? "Unknown"),
                            EscapeJson(Environment.OSVersion.VersionString),
                            DateTime.UtcNow.ToString("o")
                        );

                        byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(SentryDsnEndpoint);
                        req.Method = "POST";
                        req.ContentType = "application/json";
                        req.ContentLength = data.Length;
                        req.Timeout = 8000;
                        using (Stream s = req.GetRequestStream())
                        {
                            s.Write(data, 0, data.Length);
                        }
                        using (req.GetResponse()) { }
                    }
                }
                catch { } // Fail completely silent
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

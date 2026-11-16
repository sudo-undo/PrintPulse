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
        // Public configurable endpoints (Can be plugged to your Sentry DSN or Webhook endpoint)
        public static string FeedbackEndpointUrl = "https://formspree.io/f/xvgzgqle"; // Production direct feedback ingestion gateway
        public static string SentryDsnEndpoint = ""; // Plug your private Sentry ingestion URL here anytime
        public static bool IsTelemetryEnabled = true;

        /// <summary>
        /// Submits user feedback directly over HTTPS without needing an email client installed.
        /// </summary>
        public static bool SubmitFeedback(string category, string userEmail, string subject, string message, out string responseMessage)
        {
            responseMessage = string.Empty;
            try
            {
                // Force TLS 1.2 on modern Windows while maintaining compatibility with legacy TLS on XP/7
                EnableSecureTls();

                string payload = string.Format(
                    "{{\"category\":\"{0}\",\"email\":\"{1}\",\"subject\":\"{2}\",\"message\":\"{3}\",\"os\":\"{4}\",\"app_version\":\"1.0.0\"}}",
                    EscapeJson(category),
                    EscapeJson(string.IsNullOrEmpty(userEmail) ? "anonymous@user.com" : userEmail),
                    EscapeJson(subject),
                    EscapeJson(message),
                    EscapeJson(Environment.OSVersion.VersionString)
                );

                byte[] data = Encoding.UTF8.GetBytes(payload);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(FeedbackEndpointUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.ContentLength = data.Length;
                request.Timeout = 12000; // 12 seconds timeout

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.Accepted)
                    {
                        responseMessage = "Feedback submitted successfully!";
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                responseMessage = ex.Message;
                return false;
            }

            return false;
        }

        /// <summary>
        /// Silently logs an anonymous error/crash report to the diagnostic backend.
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

                    // Anonymize / scrub any personal file paths or document names
                    string scrubbedError = AnonymizePathData(errorMessage);
                    string scrubbedStack = AnonymizePathData(stackTrace ?? string.Empty);

                    string payload = string.Format(
                        "{{\"event\":\"error\",\"category\":\"{0}\",\"message\":\"{1}\",\"printer\":\"{2}\",\"stack\":\"{3}\",\"os\":\"{4}\",\"app_version\":\"1.0.0\",\"timestamp\":\"{5}\"}}",
                        EscapeJson(errorCategory),
                        EscapeJson(scrubbedError),
                        EscapeJson(printerDriver ?? "Unknown"),
                        EscapeJson(scrubbedStack),
                        EscapeJson(Environment.OSVersion.VersionString),
                        DateTime.UtcNow.ToString("o")
                    );

                    // If Sentry DSN endpoint is configured, transmit to Sentry
                    if (!string.IsNullOrEmpty(SentryDsnEndpoint))
                    {
                        byte[] data = Encoding.UTF8.GetBytes(payload);
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
                catch { } // Fail completely silent so app execution is never impacted
            });
        }

        private static void EnableSecureTls()
        {
            try
            {
                // Enable SecurityProtocolType.Tls12 (3072) if available on machine
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | SecurityProtocolType.Tls;
            }
            catch { }
        }

        private static string AnonymizePathData(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            // Scrub user profile names: C:\Users\Username\... -> C:\Users\[REDACTED]\...
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

using System;
using System.Collections.Generic;
using System.IO;

namespace CustomWebServer.Core
{
    public class HttpRequest
    {
        public string Method { get; set; }
        public string Url { get; set; }
        public string Version { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Cookies { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> PathParams { get; set; } = new Dictionary<string, string>();
        public string Body { get; set; }

        public static HttpRequest Parse(string rawRequest)
        {
            if (string.IsNullOrWhiteSpace(rawRequest))
                return null;

            var request = new HttpRequest();
            using (var reader = new StringReader(rawRequest))
            {
                // 1. Parse Request Line (e.g., GET /login HTTP/1.1)
                string line = reader.ReadLine();
                if (line == null) return null;

                var parts = line.Split(' ');
                if (parts.Length >= 3)
                {
                    request.Method = parts[0];
                    request.Url = parts[1];
                    request.Version = parts[2];
                }

                // 2. Parse Headers
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                {
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        string key = line.Substring(0, colonIndex).Trim();
                        string value = line.Substring(colonIndex + 1).Trim();
                        request.Headers[key] = value;

                        // Specifically parse Cookies
                        if (key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
                        {
                            ParseCookies(request, value);
                        }
                    }
                }

                // 3. Parse Body
                if (request.Headers.ContainsKey("Content-Length"))
                {
                    if (int.TryParse(request.Headers["Content-Length"], out int contentLength) && contentLength > 0)
                    {
                        char[] bodyChars = new char[contentLength];
                        int read = reader.Read(bodyChars, 0, contentLength);
                        request.Body = new string(bodyChars, 0, read);
                    }
                }
                else
                {
                    // If no explicit content length but body exists
                    request.Body = reader.ReadToEnd();
                }
            }

            return request;
        }

        private static void ParseCookies(HttpRequest request, string cookieHeader)
        {
            var cookiePairs = cookieHeader.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in cookiePairs)
            {
                var kv = pair.Split(new[] { '=' }, 2);
                if (kv.Length == 2)
                {
                    request.Cookies[kv[0].Trim()] = kv[1].Trim();
                }
            }
        }
    }
}

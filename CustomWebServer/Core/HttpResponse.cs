using System;
using System.Collections.Generic;
using System.Text;

namespace CustomWebServer.Core
{
    public class HttpResponse
    {
        public string Version { get; set; } = "HTTP/1.1";
        public int StatusCode { get; set; } = 200;
        public string StatusMessage { get; set; } = "OK";
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public List<string> Cookies { get; set; } = new List<string>();
        public byte[] Body { get; set; }

        public HttpResponse()
        {
            Headers["Server"] = "CustomCSharpServer/1.0";
            Headers["Connection"] = "close";
        }

        public void SetCookie(string key, string value, int expireMinutes = 30)
        {
            var expires = DateTime.UtcNow.AddMinutes(expireMinutes).ToString("R");
            Cookies.Add($"{key}={value}; Path=/; Expires={expires}; HttpOnly");
        }

        public byte[] ToBytes()
        {
            if (Body != null)
            {
                Headers["Content-Length"] = Body.Length.ToString();
            }
            else
            {
                Headers["Content-Length"] = "0";
            }

            var builder = new StringBuilder();
            
            // 1. Status Line
            builder.Append($"{Version} {StatusCode} {StatusMessage}\r\n");

            // 2. Headers
            foreach (var header in Headers)
            {
                builder.Append($"{header.Key}: {header.Value}\r\n");
            }

            // 3. Set-Cookies
            foreach (var cookie in Cookies)
            {
                builder.Append($"Set-Cookie: {cookie}\r\n");
            }

            // 4. Empty line implies end of headers
            builder.Append("\r\n");

            byte[] headerBytes = Encoding.UTF8.GetBytes(builder.ToString());

            if (Body != null && Body.Length > 0)
            {
                byte[] responseBytes = new byte[headerBytes.Length + Body.Length];
                Buffer.BlockCopy(headerBytes, 0, responseBytes, 0, headerBytes.Length);
                Buffer.BlockCopy(Body, 0, responseBytes, headerBytes.Length, Body.Length);
                return responseBytes;
            }

            return headerBytes;
        }

        // --- Utility Methods to generate quick responses --- //

        public static HttpResponse HtmlResponse(string html, int statusCode = 200)
        {
            var res = new HttpResponse { StatusCode = statusCode, StatusMessage = GetStatusMessage(statusCode) };
            res.Headers["Content-Type"] = "text/html; charset=utf-8";
            res.Body = Encoding.UTF8.GetBytes(html);
            return res;
        }

        public static HttpResponse JsonResponse(string json, int statusCode = 200)
        {
            var res = new HttpResponse { StatusCode = statusCode, StatusMessage = GetStatusMessage(statusCode) };
            res.Headers["Content-Type"] = "application/json; charset=utf-8";
            res.Body = Encoding.UTF8.GetBytes(json);
            return res;
        }

        public static HttpResponse Redirect(string url)
        {
            var res = new HttpResponse { StatusCode = 302, StatusMessage = "Found" };
            res.Headers["Location"] = url;
            return res;
        }

        private static string GetStatusMessage(int code)
        {
            return code switch
            {
                200 => "OK",
                302 => "Found",
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => "Unknown",
            };
        }
    }
}

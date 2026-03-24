using System;
using System.Collections.Generic;
using System.IO;

namespace CustomWebServer.Core
{
    public class Router
    {
        private readonly Dictionary<string, Func<HttpRequest, HttpResponse>> _routes = new Dictionary<string, Func<HttpRequest, HttpResponse>>(StringComparer.OrdinalIgnoreCase);

        public void AddRoute(string method, string path, Func<HttpRequest, HttpResponse> handler)
        {
            // Maps e.g. "GET /login" -> Handler Function
            _routes[$"{method.ToUpper()} {path}"] = handler;
        }

        public void Get(string path, Func<HttpRequest, HttpResponse> handler) => AddRoute("GET", path, handler);
        public void Post(string path, Func<HttpRequest, HttpResponse> handler) => AddRoute("POST", path, handler);

        public HttpResponse Route(HttpRequest request)
        {
            if (request == null) return HttpResponse.HtmlResponse("<h1>400 Bad Request</h1>", 400);

            string pathOnly = request.Url.Split('?')[0];
            string routeKey = $"{request.Method} {pathOnly}";

            // 1. Exact matches
            if (_routes.ContainsKey(routeKey))
            {
                return _routes[routeKey](request);
            }

            // 2. Dynamic matches (e.g. /chat/:id)
            foreach (var route in _routes)
            {
                var parts = route.Key.Split(' ');
                if (parts[0].Equals(request.Method, StringComparison.OrdinalIgnoreCase))
                {
                    var routeSubDirs = parts[1].Split('/');
                    var requestSubDirs = pathOnly.Split('/');

                    if (routeSubDirs.Length == requestSubDirs.Length)
                    {
                        bool match = true;
                        var extractedParams = new Dictionary<string, string>();

                        for (int i = 0; i < routeSubDirs.Length; i++)
                        {
                            if (routeSubDirs[i].StartsWith(":"))
                            {
                                extractedParams[routeSubDirs[i].Substring(1)] = requestSubDirs[i];
                            }
                            else if (!routeSubDirs[i].Equals(requestSubDirs[i], StringComparison.OrdinalIgnoreCase))
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            request.PathParams = extractedParams;
                            return route.Value(request);
                        }
                    }
                }
            }

            // 3. Static Files Middleware Fallback
            return ServeStaticFile(pathOnly);
        }

        private HttpResponse ServeStaticFile(string path)
        {
            if (path == "/") path = "/index.html"; // Default page fallback (though router should catch / above)
            
            // Protect against directory traversal
            if (path.Contains("..")) return HttpResponse.HtmlResponse("<h1>403 Forbidden</h1>", 403);

            string filePath = Path.Combine("wwwroot", path.TrimStart('/'));

            if (File.Exists(filePath))
            {
                var res = new HttpResponse { StatusCode = 200 };
                res.Body = File.ReadAllBytes(filePath);
                res.Headers["Content-Type"] = GetMimeType(filePath);
                return res;
            }

            return HttpResponse.HtmlResponse("<h1>404 Not Found - Route missing</h1>", 404);
        }

        private string GetMimeType(string path)
        {
            if (path.EndsWith(".html")) return "text/html; charset=utf-8";
            if (path.EndsWith(".css")) return "text/css";
            if (path.EndsWith(".js")) return "application/javascript";
            if (path.EndsWith(".png")) return "image/png";
            if (path.EndsWith(".jpg") || path.EndsWith(".jpeg")) return "image/jpeg";
            if (path.EndsWith(".json")) return "application/json";
            return "application/octet-stream";
        }
    }
}

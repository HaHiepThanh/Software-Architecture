using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CustomWebServer.Core;

namespace CustomWebServer.Controllers
{
    public static class AuthController
    {
        // MUST BE CONFIGURED WITH GOOGLE API CONSOLE CREDENTIALS
        private const string ClientId = "YOUR_CLIENT_ID.apps.googleusercontent.com"; 
        private const string ClientSecret = "YOUR_CLIENT_SECRET"; 
        private const string RedirectUri = "http://localhost:8080/auth/google/callback";

        public static HttpResponse RedirectToGoogle(HttpRequest req)
        {
            string googleAuthUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                                 $"client_id={ClientId}&" +
                                 $"redirect_uri={RedirectUri}&" +
                                 $"response_type=code&" +
                                 $"scope=email profile";
            
            return HttpResponse.Redirect(googleAuthUrl);
        }

        public static HttpResponse GoogleCallback(HttpRequest req)
        {
            string queryString = "";
            var urlParts = req.Url.Split('?');
            if (urlParts.Length > 1) queryString = urlParts[1];

            var queryParams = ParseQueryString(queryString);
            if (queryParams.TryGetValue("code", out string code))
            {
                // Exchange the authorization code for an email profile synchronously for simplicity
                var email = ExchangeCodeForEmail(code).GetAwaiter().GetResult();
                
                if (!string.IsNullOrEmpty(email))
                {
                    // Authentication successful
                    string token = SessionManager.CreateSession(email);
                    var res = HttpResponse.Redirect("/chat");
                    
                    // Issue cookie valid for 24 hours
                    res.SetCookie("auth_token", token, 60 * 24); 
                    return res;
                }
            }

            return HttpResponse.HtmlResponse("<h1>Google Login Failed</h1><a href=\"/login\">Try again</a>", 401);
        }

        private static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(queryString)) return dict;
            
            var pairs = queryString.Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=');
                if (kv.Length == 2)
                {
                    dict[kv[0]] = Uri.UnescapeDataString(kv[1]);
                }
            }
            return dict;
        }

        private static async Task<string> ExchangeCodeForEmail(string code)
        {
            try
            {
                // Note: We use HttpClient for the *outbound* request to Google.
                // This doesn't violate the rule since the *Web Server itself* is built with System.Net.Sockets.
                using var client = new HttpClient();
                var values = new Dictionary<string, string>
                {
                    { "client_id", ClientId },
                    { "client_secret", ClientSecret },
                    { "code", code },
                    { "grant_type", "authorization_code" },
                    { "redirect_uri", RedirectUri }
                };

                var content = new FormUrlEncodedContent(values);
                var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", content);
                string tokenResult = await tokenResponse.Content.ReadAsStringAsync();

                using JsonDocument tokenDoc = JsonDocument.Parse(tokenResult);
                if (tokenDoc.RootElement.TryGetProperty("access_token", out JsonElement accessTokenElement))
                {
                    string accessToken = accessTokenElement.GetString();
                    
                    // Call the userinfo endpoint to get the email explicitly requested by scope
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    var userResponse = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                    string userResult = await userResponse.Content.ReadAsStringAsync();
                    
                    using JsonDocument userDoc = JsonDocument.Parse(userResult);
                    if (userDoc.RootElement.TryGetProperty("email", out JsonElement emailElement))
                    {
                        return emailElement.GetString();
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[ERROR] OAuth Exchange: {ex.Message}");
            }
            
            return null;
        }
    }
}

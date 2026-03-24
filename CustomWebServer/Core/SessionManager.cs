using System;
using System.Collections.Concurrent;

namespace CustomWebServer.Core
{
    public static class SessionManager
    {
        // Thread-safe dictionary to map dynamic tokens to usernames
        private static ConcurrentDictionary<string, string> _sessions = new ConcurrentDictionary<string, string>();

        public static string CreateSession(string username)
        {
            string token = Guid.NewGuid().ToString("N");
            _sessions[token] = username;
            return token;
        }

        public static string GetUsername(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return _sessions.TryGetValue(token, out string username) ? username : null;
        }

        public static void RemoveSession(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                _sessions.TryRemove(token, out _);
            }
        }
    }
}

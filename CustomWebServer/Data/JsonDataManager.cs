using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using CustomWebServer.Models; 

namespace CustomWebServer.Data
{
    public class JsonDataManager
    {
        private static JsonDataManager _instance;
        private static readonly object _lock = new object();
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

        private readonly string _usersFilePath = "Data/users.json";
        private readonly string _chatDir = "Data/chats/";

        private JsonDataManager()
        {
            if (!Directory.Exists("Data")) Directory.CreateDirectory("Data");
            if (!Directory.Exists(_chatDir)) Directory.CreateDirectory(_chatDir);
            
            if (!File.Exists(_usersFilePath))
            {
                var users = new List<User> { 
                    new User { Username = "nntu", Password = "56789" },
                    new User { Username = "AdTekDev", Password = "56789" }
                };
                File.WriteAllText(_usersFilePath, JsonSerializer.Serialize(users));
            }
        }

        public static JsonDataManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new JsonDataManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public bool ValidateUser(string username, string password)
        {
            _rwLock.EnterReadLock();
            try
            {
                if (!File.Exists(_usersFilePath)) return false;
                
                string json = File.ReadAllText(_usersFilePath);
                var users = JsonSerializer.Deserialize<List<User>>(json);
                if (users == null) return false;

                foreach (var u in users)
                {
                    if (u.Username == username && u.Password == password)
                        return true;
                }
                return false;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public void SaveChatMessage(string roomId, string username, string message)
        {
            string roomFile = Path.Combine(_chatDir, $"room_{roomId}.json");
            
            _rwLock.EnterWriteLock();
            try
            {
                List<ChatMessage> history = new List<ChatMessage>();
                if (File.Exists(roomFile))
                {
                    string json = File.ReadAllText(roomFile);
                    var existing = JsonSerializer.Deserialize<List<ChatMessage>>(json);
                    if (existing != null) history = existing;
                }

                history.Add(new ChatMessage { Time = DateTime.UtcNow, Username = username, Message = message });
                File.WriteAllText(roomFile, JsonSerializer.Serialize(history));
                
                File.WriteAllText(Path.Combine(_chatDir, $"room_{roomId}_activity.txt"), DateTime.UtcNow.ToString("o"));
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public List<ChatMessage> GetChatHistory(string roomId)
        {
            string roomFile = Path.Combine(_chatDir, $"room_{roomId}.json");
            _rwLock.EnterReadLock();
            try
            {
                if (!File.Exists(roomFile)) return new List<ChatMessage>();
                return JsonSerializer.Deserialize<List<ChatMessage>>(File.ReadAllText(roomFile)) ?? new List<ChatMessage>();
            }
            finally { _rwLock.ExitReadLock(); }
        }

        public void ClearRoomHistory(string roomId)
        {
            string roomFile = Path.Combine(_chatDir, $"room_{roomId}.json");
            string activityFile = Path.Combine(_chatDir, $"room_{roomId}_activity.txt");

            _rwLock.EnterWriteLock();
            try
            {
                if (File.Exists(roomFile)) File.Delete(roomFile);
                if (File.Exists(activityFile)) File.Delete(activityFile);
            }
            finally { _rwLock.ExitWriteLock(); }
        }
    }
}

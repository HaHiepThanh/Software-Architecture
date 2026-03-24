using System;
using System.Text.Json.Serialization;

namespace CustomWebServer.Models
{
    public class ChatMessage
    {
        [JsonPropertyName("time")]
        public DateTime Time { get; set; }
        
        [JsonPropertyName("username")]
        public string Username { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}

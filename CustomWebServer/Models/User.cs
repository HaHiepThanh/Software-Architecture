using System.Text.Json.Serialization;

namespace CustomWebServer.Models
{
    public class User
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }
        
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
}

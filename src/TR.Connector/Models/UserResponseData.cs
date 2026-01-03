using System.Text.Json.Serialization;

namespace TR.Connector.Models;

internal class UserResponseData
{
    [JsonPropertyName("login")]
    public string Login { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; }
}
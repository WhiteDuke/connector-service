using System.Text.Json.Serialization;

namespace TR.Connector.Models;

internal class UserResponse
{
    [JsonPropertyName("data")]
    public List<UserResponseData> Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("errorText")]
    public string ErrorText { get; set; }
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
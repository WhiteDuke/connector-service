using System.Text.Json.Serialization;

namespace TR.Connector.Models;

public class UserPropertyResponse
{
    [JsonPropertyName("data")]
    public UserPropertyData Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("errorText")]
    public string ErrorText { get; set; }
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
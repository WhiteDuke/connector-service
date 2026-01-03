using System.Text.Json.Serialization;

namespace TR.Connector.Models;

internal class TokenResponse
{
    [JsonPropertyName("data")]
    public TokenResponseData Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("errorText")]
    public string ErrorText { get; set; }
    
    [JsonPropertyName("count")]
    public object Count { get; set; }
}
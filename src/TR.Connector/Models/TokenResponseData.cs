using System.Text.Json.Serialization;

namespace TR.Connector.Models;

public class TokenResponseData
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
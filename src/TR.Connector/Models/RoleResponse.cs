using System.Text.Json.Serialization;

namespace TR.Connector.Models;

public class RoleResponse
{
    [JsonPropertyName("data")]
    public List<RoleResponseData> Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("errorText")]
    public string ErrorText { get; set; }
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
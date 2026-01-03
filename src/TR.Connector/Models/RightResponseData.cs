using System.Text.Json.Serialization;

namespace TR.Connector.Models;

internal class RightResponseData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("users")]
    public object Users { get; set; }
}
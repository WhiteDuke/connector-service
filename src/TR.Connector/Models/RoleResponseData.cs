using System.Text.Json.Serialization;

namespace TR.Connector.Models;

public class RoleResponseData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("corporatePhoneNumber")]
    public string CorporatePhoneNumber { get; set; }
}
using System.Text.Json.Serialization;

namespace TR.Connector.Models;

internal sealed class CreateUserDto : UserPropertyData
{
    [JsonPropertyName("password")]
    public string Password { get; set; }
}
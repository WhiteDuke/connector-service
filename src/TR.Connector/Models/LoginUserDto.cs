using System.Text.Json.Serialization;

namespace TR.Connector.Models;

internal sealed class LoginUserDto
{
    public LoginUserDto(string login, string password)
    {
        Login = login;
        Password = password;
    }

    [JsonPropertyName("login")]
    public string Login { get; set; }
    
    [JsonPropertyName("password")]
    public string Password { get; set; }
}
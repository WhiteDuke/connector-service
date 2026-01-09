using TR.Connectors.Api.Entities;
using TR.Connectors.Api.Interfaces;

namespace TR.Connector.Tests;

public class ConnectorTests : IAsyncLifetime
{
    private readonly IConnector _connector;
    private readonly string _connectorString = "url=http://localhost:5000;login=login;password=password";

    public ConnectorTests()
    {
        _connector = new Connector();
        _connector.SetLogger(new ConsoleLogger());
    }

    public async Task InitializeAsync()
    {
        await _connector.StartUpAsync(_connectorString);
    }

    public Task DisposeAsync()
    {
        _connector.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetAllPermissions_Ok()
    {
        var permissions = (await _connector.GetAllPermissionsAsync()).ToList();
        Assert.NotNull(permissions);

        var itRole9 = permissions.FirstOrDefault(p => p.Name == "ITRole9");
        Assert.NotNull(itRole9);
        Assert.Equal("ItRole,9", itRole9.Id);

        var requestRight5 = permissions.FirstOrDefault(p => p.Name == "RequestRight5");
        Assert.NotNull(requestRight5);
        Assert.Equal("RequestRight,5", requestRight5.Id);
    }

    [Fact]
    public async Task GetUserPermissions_Ok()
    {
        const string login = "Login3";
        var permissions = (await _connector.GetUserPermissionsAsync(login)).ToList();

        Assert.NotNull(permissions);
        Assert.NotNull(permissions.FirstOrDefault(s => s.Contains("ItRole")));
        Assert.NotNull(permissions.FirstOrDefault(s => s.Contains("RequestRight")));
    }

    [Fact]
    public async Task Add_Drop_Permissions_Ok()
    {
        const string login = "Login7";
        const string userRole = "ItRole,5";
        const string userRight = "RequestRight,5";
        await _connector.AddUserPermissionsAsync(login, new List<string>(){userRole, userRight});

        var userPermissions = (await _connector.GetUserPermissionsAsync(login)).ToList();
        Assert.NotNull(userPermissions.FirstOrDefault(x => x.Contains(userRole)));
        Assert.NotNull(userPermissions.FirstOrDefault(x => x.Contains(userRight)));

        await _connector.RemoveUserPermissionsAsync(login, new List<string>(){userRole, userRight});

        userPermissions = (await _connector.GetUserPermissionsAsync(login)).ToList();
        Assert.Null(userPermissions.FirstOrDefault(x => x.Contains(userRole)));
        Assert.Null(userPermissions.FirstOrDefault(x => x.Contains(userRight)));
    }

    [Fact]
    public void GetAllProperties_Ok()
    {
        var allProperties = _connector.GetAllProperties();

        Assert.NotNull(allProperties);
        Assert.NotNull(allProperties.FirstOrDefault(p => p.Name.Contains("isLead")));
    }

    [Fact]
    public async Task Get_UpdateUserProperties_Ok()
    {
        const string login = "Login3";
        var userProperties = (await _connector.GetUserPropertiesAsync(login)).ToList();
        Assert.NotNull(userProperties);

        var firstNameProperty = userProperties.FirstOrDefault(p => p.Name == "firstName");
        Assert.NotNull(firstNameProperty);
        Assert.Equal("FirstName3", firstNameProperty.Value);
        
        var telephoneNumberProperty = userProperties.FirstOrDefault(p => p.Name == "telephoneNumber");
        Assert.NotNull(telephoneNumberProperty);
        Assert.Equal("TelephoneNumber3", telephoneNumberProperty.Value);

        var userProps = new List<UserProperty>()
        {
            new UserProperty("firstName", "FirstName13"),
            new UserProperty("telephoneNumber", "TelephoneNumber13"),
        };
        await _connector.UpdateUserPropertiesAsync(userProps, login);

        userProperties = (await _connector.GetUserPropertiesAsync(login)).ToList();
        Assert.NotNull(userProperties);

        firstNameProperty = userProperties.FirstOrDefault(p => p.Name == "firstName");
        Assert.NotNull(firstNameProperty);
        Assert.Equal("FirstName13", firstNameProperty.Value);
        
        telephoneNumberProperty = userProperties.FirstOrDefault(p => p.Name == "telephoneNumber");
        Assert.NotNull(telephoneNumberProperty);
        Assert.Equal("TelephoneNumber13", telephoneNumberProperty.Value);
    }

    [Fact]
    public async Task Get_CreateUser_Ok()
    {
        const string login = "Login100";

        var isUser = await _connector.IsUserExistsAsync(login);
        Assert.False(isUser);

        var user = new UserToCreate(login, "Password100")
        {
            Properties = new List<UserProperty>()
            {
                new UserProperty("firstName", "FirstName100"),
                new UserProperty("lastName", ""),
                new UserProperty("middleName", ""),
                new UserProperty("telephoneNumber", ""),
                new UserProperty("isLead", ""),
            }
        };

        await _connector.CreateUserAsync(user);

        isUser = await _connector.IsUserExistsAsync(login);
        Assert.True(isUser);
    }
}
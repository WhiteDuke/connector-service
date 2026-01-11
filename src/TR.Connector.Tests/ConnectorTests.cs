using System.Text.Json;
using TR.Connector.Exceptions;
using TR.Connector.Models;
using TR.Connectors.Api.Entities;
using TR.Connectors.Api.Interfaces;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace TR.Connector.Tests;

public class ConnectorTests : IAsyncLifetime
{
    private readonly IConnector _connector;
    private const string ConnectorString = "url=http://localhost:5000;login=login;password=password";
    private readonly WireMockServer _server;
    private const int MockServerStartTimeOutInSeconds = 2;

    public ConnectorTests()
    {
        _connector = new Connector();
        _connector.SetLogger(new ConsoleLogger());

        var serverSettings = new WireMockServerSettings
        {
            Logger = new WireMockConsoleLogger(),
            Urls = ["http://localhost:5000"],
            StartTimeout = MockServerStartTimeOutInSeconds
        };

        _server = WireMockServer.Start(serverSettings);
        
        var tokenResponse = new TokenResponse
        {
            Count = 1,
            Success = true,
            Data = new TokenResponseData
            {
                AccessToken = "some access token",
                ExpiresIn = TimeSpan.FromDays(1).Hours
            }
        };

        _server.Given(
                Request
                    .Create()
                    .UsingPost()
                    .WithPath("/api/v1/login"))
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(JsonSerializer.Serialize(tokenResponse))
            );
    }

    public async Task InitializeAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(MockServerStartTimeOutInSeconds));
        await _connector.StartUpAsync(ConnectorString);
    }

    public Task DisposeAsync()
    {
        _connector.Dispose();
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetAllPermissions_Ok()
    {
        var rightsForResponse = new RoleResponse
        {
            Count = 1,
            Data =
            [
                new RoleResponseData
                {
                    CorporatePhoneNumber = "8 800 2000 500",
                    Id = 5,
                    Name = "RequestRight5"
                }
            ],
            Success = true
        };
        
        var rolesForResponse = new RoleResponse
        {
            Count = 1,
            Data =
            [
                new RoleResponseData
                {
                    CorporatePhoneNumber = "8 800 2000 550",
                    Id = 9,
                    Name = "ITRole9"
                }
            ],
            Success = true
        }; 

        _server.Given(Request.Create()
                .UsingGet()
                .WithPath("/api/v1/roles/all"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(rolesForResponse)));

        _server.Given(Request.Create()
                .UsingGet()
                .WithPath("/api/v1/rights/all"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(rightsForResponse)));

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
        var rightsForResponse = new RoleResponse
        {
            Count = 1,
            Data =
            [
                new RoleResponseData
                {
                    CorporatePhoneNumber = "8 800 2000 500",
                    Id = 5,
                    Name = "RequestRight5"
                }
            ],
            Success = true
        };
        
        var rolesForResponse = new RoleResponse
        {
            Count = 1,
            Data =
            [
                new RoleResponseData
                {
                    CorporatePhoneNumber = "8 800 2000 550",
                    Id = 9,
                    Name = "ITRole9"
                }
            ],
            Success = true
        }; 

        const string login = "Login3";

        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}/roles"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(rolesForResponse)));

        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}/rights"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(rightsForResponse)));

        
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
        
        var userResponse = new UserPropertyResponse()
        {
            Count = 1,
            Data = new UserPropertyData()
            {
                FirstName = "first name",
                IsLead = true,
                LastName = "last name",
                Login = "Login7",
                MiddleName = "middle name",
                Status = "Unlock",
                TelephoneNumber = "8 800 2000 500"
            },
            Success = true
        };

        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(userResponse)));

        _server
            .Given(Request.Create().UsingPut().WithPath($"/api/v1/users/{login}/add/role/*"))
            .RespondWith(Response.Create().WithStatusCode(200));

        _server
            .Given(Request.Create().UsingPut().WithPath($"/api/v1/users/{login}/add/right/*"))
            .RespondWith(Response.Create().WithStatusCode(200));

        _server
            .Given(Request.Create().UsingDelete().WithPath($"/api/v1/users/{login}/drop/role/*"))
            .RespondWith(Response.Create().WithStatusCode(200));

        _server
            .Given(Request.Create().UsingDelete().WithPath($"/api/v1/users/{login}/drop/right/*"))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _connector.AddUserPermissionsAsync(login, new List<string> {userRole, userRight});

        var rightsForResponse = new RoleResponse
        {
            Count = 1,
            Data =
            [
                new RoleResponseData
                {
                    CorporatePhoneNumber = "8 800 2000 500",
                    Id = 5,
                    Name = "RequestRight"
                }
            ],
            Success = true
        };
        
        var rolesForResponse = new RoleResponse
        {
            Count = 1,
            Data =
            [
                new RoleResponseData
                {
                    CorporatePhoneNumber = "8 800 2000 550",
                    Id = 5,
                    Name = "ITRole9"
                }
            ],
            Success = true
        }; 

        var rolesRequestMapping = _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}/roles"));

        rolesRequestMapping.RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(rolesForResponse)));

        var rightRequestMapping = _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}/rights"));

        rightRequestMapping.RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(rightsForResponse)));

        var userPermissions = (await _connector.GetUserPermissionsAsync(login)).ToList();
        Assert.NotNull(userPermissions.FirstOrDefault(x => x.Contains(userRole)));
        Assert.NotNull(userPermissions.FirstOrDefault(x => x.Contains(userRight)));

        await _connector.RemoveUserPermissionsAsync(login, new List<string> {userRole, userRight});

        var emptyRolesResponse = new RoleResponse()
        {
            Count = 0,
            Success = true,
            Data = []
        };

        _server.DeleteMapping(rolesRequestMapping.Guid);
        _server.DeleteMapping(rightRequestMapping.Guid);

        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}/roles"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(emptyRolesResponse)));

        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}/rights"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(emptyRolesResponse)));

        userPermissions = (await _connector.GetUserPermissionsAsync(login)).ToList();
        Assert.Null(userPermissions.FirstOrDefault(x => x.Contains(userRole)));
        Assert.Null(userPermissions.FirstOrDefault(x => x.Contains(userRight)));
    }

    [Fact]
    public async Task Add_Drop_Permissions_UserNotFoundExceptionThrown()
    {
        const string login = "Login7";
        const string userRole = "ItRole,5";
        const string userRight = "RequestRight,5";
        
        var userResponse = new UserPropertyResponse()
        {
            Count = 0,
            Data = null,
            Success = true
        };

        _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(userResponse)));

        await Assert.ThrowsAsync<UserNotFoundException>(() => _connector.AddUserPermissionsAsync(login, new List<string> {userRole, userRight}));
    }

    [Fact]
    public void GetAllProperties_Ok()
    {
        var allProperties = _connector.GetAllProperties();

        Assert.NotNull(allProperties);
        Assert.NotNull(allProperties.FirstOrDefault(p => p.Name.Contains("IsLead")));
    }

    [Fact]
    public async Task Get_UpdateUserProperties_Ok()
    {
        const string login = "Login3";

        var userResponse = new UserPropertyResponse()
        {
            Count = 1,
            Data = new UserPropertyData()
            {
                FirstName = "FirstName3",
                IsLead = true,
                LastName = "LastName3",
                Login = "Login3",
                MiddleName = "MiddleName3",
                Status = "Unlock",
                TelephoneNumber = "TelephoneNumber3"
            },
            Success = true
        };
            
        var getUserMapping = _server.Given(Request.Create()
                .UsingGet()
                .WithPath($"/api/v1/users/{login}"));

        getUserMapping.RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(userResponse)));

        var userProperties = (await _connector.GetUserPropertiesAsync(login)).ToList();
        Assert.NotNull(userProperties);

        var firstNameProperty = userProperties.FirstOrDefault(p => p.Name == "FirstName");
        Assert.NotNull(firstNameProperty);
        Assert.Equal("FirstName3", firstNameProperty.Value);
        
        var telephoneNumberProperty = userProperties.FirstOrDefault(p => p.Name == "TelephoneNumber");
        Assert.NotNull(telephoneNumberProperty);
        Assert.Equal("TelephoneNumber3", telephoneNumberProperty.Value);

        _server
            .Given(Request.Create().UsingPut().WithPath("/api/v1/users/edit"))
            .RespondWith(Response.Create().WithStatusCode(200));

        var userProps = new List<UserProperty>
        {
            new("FirstName", "FirstName13"),
            new("TelephoneNumber", "TelephoneNumber13"),
        };
        await _connector.UpdateUserPropertiesAsync(userProps, login);

        userResponse.Data.FirstName = "FirstName13";
        userResponse.Data.TelephoneNumber = "TelephoneNumber13";
        _server.DeleteMapping(getUserMapping.Guid);
        
        _server.Given(Request.Create()
            .UsingGet()
            .WithPath($"/api/v1/users/{login}"))
        .RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(userResponse)));

        userProperties = (await _connector.GetUserPropertiesAsync(login)).ToList();
        Assert.NotNull(userProperties);

        firstNameProperty = userProperties.FirstOrDefault(p => p.Name == "FirstName");
        Assert.NotNull(firstNameProperty);
        Assert.Equal("FirstName13", firstNameProperty.Value);
        
        telephoneNumberProperty = userProperties.FirstOrDefault(p => p.Name == "TelephoneNumber");
        Assert.NotNull(telephoneNumberProperty);
        Assert.Equal("TelephoneNumber13", telephoneNumberProperty.Value);
    }

    [Fact]
    public async Task Get_CreateUser_Ok()
    {
        const string login = "Login100";

        var userResponse = new UserPropertyResponse()
        {
            Count = 0,
            Data = null,
            Success = true
        };

        var userMapping = _server.Given(Request.Create()
            .UsingGet()
            .WithPath($"/api/v1/users/{login}"));
    
        userMapping.RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(userResponse)));

        var isUser = await _connector.IsUserExistsAsync(login);
        Assert.False(isUser);

        var userToCreate = new UserToCreate(login, "Password100")
        {
            Properties = new List<UserProperty>
            {
                new("firstName", "FirstName100"),
                new("lastName", ""),
                new("middleName", ""),
                new("telephoneNumber", ""),
                new("isLead", ""),
            }
        };

        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/api/v1/users/create")
                .WithBody(JsonSerializer.Serialize(userToCreate)))
            .RespondWith(Response.Create().WithStatusCode(200));

        _server.DeleteMapping(userMapping.Guid);
        userResponse.Data = new UserPropertyData()
        {
            FirstName = "FirstName100",
            LastName = "",
            MiddleName = "",
            TelephoneNumber = "",
            IsLead = false
        };
        
        await _connector.CreateUserAsync(userToCreate);

        _server.Given(Request.Create()
            .UsingGet()
            .WithPath($"/api/v1/users/{login}"))
        .RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(userResponse)));

        isUser = await _connector.IsUserExistsAsync(login);
        Assert.True(isUser);
    }
}
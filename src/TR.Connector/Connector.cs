using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TR.Connector.Models;
using TR.Connectors.Api.Entities;
using TR.Connectors.Api.Interfaces;

namespace TR.Connector;

public class Connector : IConnector
{
    public ILogger Logger { get; private set; }

    private string _url = "";
    private string _login = "";
    private string _password = "";

    private string _token = "";

    private readonly HttpClient _httpClient;

    //Пустой конструктор
    public Connector()
    {
        _httpClient = new HttpClient();
    }

    public void SetLogger(ILogger logger)
    {
        Logger = logger;
    }

    public async Task StartUpAsync(string connectionString)
    {
        if (Logger == null)
        {
            throw new Exception("Logger is not set");
        }

        //Парсим строку подключения.
        Logger.Debug("Строка подключения: " + connectionString);
        foreach (var item in connectionString.Split(';'))
        {
            if (item.StartsWith("url"))
            {
                _url = item.Split('=')[1];
                continue;
            }

            if (item.StartsWith("login"))
            {
                _login = item.Split('=')[1];
                continue;
            }

            if (item.StartsWith("password"))
            {
                _password = item.Split('=')[1];
            }
        }

        //Проходим аунтификацию на сервере.
        _httpClient.BaseAddress = new Uri(_url);
        var body = new { login = _login, password = _password };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("api/v1/login", content);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync());
        _token = tokenResponse.Data.AccessToken;
        // TODO: добавить обработку ошибок

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public async Task<IEnumerable<Permission>> GetAllPermissions()
    {
        //Получаем ИТРоли
        var response = await _httpClient.GetAsync("api/v1/roles/all");
        var itRoleResponse = JsonSerializer.Deserialize<RoleResponse>(await response.Content.ReadAsStringAsync());
        var itRolePermissions =
            itRoleResponse.Data.Select(x => new Permission($"ItRole,{x.Id}", x.Name, x.CorporatePhoneNumber));

        //Получаем права
        response = await _httpClient.GetAsync("api/v1/rights/all");
        var rightResponse = JsonSerializer.Deserialize<RoleResponse>(await response.Content.ReadAsStringAsync());
        var rightPermissions = rightResponse.Data.Select(x =>
            new Permission($"RequestRight,{x.Id}", x.Name, x.CorporatePhoneNumber));

        return itRolePermissions.Concat(rightPermissions);
    }

    public async Task<IEnumerable<string>> GetUserPermissions(string userLogin)
    {
        //Получаем ИТРоли
        var response = await _httpClient.GetAsync($"api/v1/users/{userLogin}/roles");
        var itRoleResponse = JsonSerializer.Deserialize<UserRoleResponse>(await response.Content.ReadAsStringAsync());
        var result1 = itRoleResponse.Data.Select(x => $"ItRole,{x.Id}").ToList();

        //Получаем права
        response = await _httpClient.GetAsync($"api/v1/users/{userLogin}/rights");
        var rightResponse = JsonSerializer.Deserialize<UserRoleResponse>(await response.Content.ReadAsStringAsync());
        var result2 = rightResponse.Data.Select(x => $"RequestRight,{x.Id}").ToList();

        return result1.Concat(result2).ToList();
    }

    public async Task AddUserPermissions(string userLogin, IEnumerable<string> rightIds)
    {
        //проверяем что пользователь не залочен.
        var response = await _httpClient.GetAsync($"api/v1/users/all");
        var userResponse = JsonSerializer.Deserialize<UserResponse>(await response.Content.ReadAsStringAsync());
        var user = userResponse.Data.FirstOrDefault(x => x.Login == userLogin);

        if (user != null && user.Status == "Lock")
        {
            Logger.Error($"Пользователь {userLogin} залочен.");
            return;
        }

        //Назначаем права.
        if (user != null && user.Status == "Unlock")
        {
            foreach (var rightId in rightIds)
            {
                var rightStr = rightId.Split(',');
                switch (rightStr[0])
                {
                    case "ItRole":
                        await _httpClient.PutAsync($"api/v1/users/{userLogin}/add/role/{rightStr[1]}", null);
                        break;
                    case "RequestRight":
                        await _httpClient.PutAsync($"api/v1/users/{userLogin}/add/right/{rightStr[1]}", null);
                        break;
                    default: 
                        throw new Exception($"Тип доступа {rightStr[0]} не определен");
                }
            }
        }
    }

    public async Task RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds)
    {
        //проверяем что пользователь не залочен.
        var response = await _httpClient.GetAsync($"api/v1/users/all");
        var userResponse = JsonSerializer.Deserialize<UserResponse>(await response.Content.ReadAsStringAsync());
        var user = userResponse.Data.FirstOrDefault(d => d.Login == userLogin);

        if (user != null && user.Status == "Lock")
        {
            Logger.Error($"Пользователь {userLogin} залочен.");
            return;
        }

        //отзываем права.
        if (user != null && user.Status == "Unlock")
        {
            foreach (var rightId in rightIds)
            {
                var rightStr = rightId.Split(',');
                switch (rightStr[0])
                {
                    case "ItRole":
                        await _httpClient.DeleteAsync($"api/v1/users/{userLogin}/drop/role/{rightStr[1]}");
                        break;
                    case "RequestRight":
                        await _httpClient.DeleteAsync($"api/v1/users/{userLogin}/drop/right/{rightStr[1]}");
                        break;
                    default:
                        throw new Exception($"Тип доступа {rightStr[0]} не определен");
                }
            }
        }
    }

    public IEnumerable<Property> GetAllProperties()
    {
        var props = new List<Property>();

        foreach (var propertyInfo in typeof(UserPropertyData).GetProperties())
        {
            if (propertyInfo.Name == "Login")
            {
                continue;
            }

            props.Add(new Property(propertyInfo.Name, propertyInfo.Name));
        }
        return props;
    }

    public async Task<IEnumerable<UserProperty>> GetUserProperties(string userLogin)
    {
        var response = await _httpClient.GetAsync($"api/v1/users/{userLogin}");
        var userResponse = JsonSerializer.Deserialize<UserPropertyResponse>(await response.Content.ReadAsStringAsync());

        var user = userResponse?.Data ?? throw new NullReferenceException($"Пользователь {userLogin} не найден");

        if (user.Status == "Lock")
        {
            throw new Exception($"Невозможно получить свойства, пользователь {userLogin} залочен");
        }

        return user.GetType().GetProperties()
            .Select(x => new UserProperty(x.Name, x.GetValue(user) as string));
    }

    public async Task UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin)
    {
        var response = await _httpClient.GetAsync($"api/v1/users/{userLogin}");
        var userResponse = JsonSerializer.Deserialize<UserPropertyResponse>(await response.Content.ReadAsStringAsync());

        var user = userResponse?.Data ?? throw new NullReferenceException($"Пользователь {userLogin} не найден");
        if (user.Status == "Lock")
            throw new Exception($"Невозможно обновить свойства, пользователь {userLogin} залочен");

        foreach (var property in properties)
        {
            foreach (var userProp in user.GetType().GetProperties())
            {
                if (property.Name == userProp.Name)
                {
                    userProp.SetValue(user, property.Value);
                }
            }
        }

        var content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json");
        await _httpClient.PutAsync("api/v1/users/edit", content);
    }

    public async Task<bool> IsUserExists(string userLogin)
    {
        var response = await _httpClient.GetAsync($"api/v1/users/all");
        var userResponse = JsonSerializer.Deserialize<UserResponse>(await response.Content.ReadAsStringAsync());
        var user = userResponse?.Data.FirstOrDefault(x => x.Login == userLogin);

        return user != null;
    }

    public async Task CreateUser(UserToCreate user)
    {
        var newUser = new CreateUserDto()
        {
            Login = user.Login,
            Password = user.HashPassword,

            LastName = user.Properties.FirstOrDefault(p => p.Name.Equals("lastName", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
            FirstName = user.Properties.FirstOrDefault(p => p.Name.Equals("firstName", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
            MiddleName = user.Properties.FirstOrDefault(p => p.Name.Equals("middleName", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,

            TelephoneNumber = user.Properties.FirstOrDefault(p => p.Name.Equals("telephoneNumber", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
            IsLead = bool.TryParse(user.Properties.FirstOrDefault(p => p.Name.Equals("isLead", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty, out var isLeadValue) 
                     && isLeadValue,

            Status = string.Empty
        };

        var content = new StringContent(JsonSerializer.Serialize(newUser), Encoding.UTF8, "application/json");
        await _httpClient.PostAsync("api/v1/users/create", content);
    }

    private async Task<TR> PostRequestAsync<TD, TR>(string endpoint, TD data)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();
            var reponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TR>(reponse);
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка при запросе к {endpoint}: {ex.Message}");
            throw;
        }
    }

    private async Task<T> SendRequestAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content);
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка при запросе к {endpoint}: {ex.Message}");
            throw;
        }
    }

    private async Task SendRequestAsync(string endpoint)
    {
        try
        {
            await _httpClient.GetAsync(endpoint);
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка при запросе к {endpoint}: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
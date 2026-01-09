using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TR.Connector.Exceptions;
using TR.Connector.Models;
using TR.Connectors.Api.Entities;
using TR.Connectors.Api.Interfaces;

namespace TR.Connector;

public class Connector : IConnector
{
    public ILogger Logger { get; private set; }

    private string _url = string.Empty;
    private string _login = string.Empty;
    private string _password = string.Empty;

    private string _token = string.Empty;

    private readonly HttpClient _httpClient;
    private const string AppJsonMimeType = "application/json";

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
            throw new ConnectorInitializationException("Logger is not set");
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

        if (string.IsNullOrWhiteSpace(_url) 
            || string.IsNullOrWhiteSpace(_login)
            || string.IsNullOrWhiteSpace(_password))
        {
            throw new ConnectorInitializationException("Incorrect connection string");
        }

        //Проходим аунтификацию на сервере.
        _httpClient.BaseAddress = new Uri(_url);
        var loginDto = new LoginUserDto(_login, _password);
        var tokenRequestResult = await PostAsync<TokenResponse, LoginUserDto> ("api/v1/login", loginDto);

        if (!tokenRequestResult.IsSuccess)
        {
            throw new ConnectorInitializationException(tokenRequestResult.Error);
        }

        if (!tokenRequestResult.Value.Success)
        {
            throw new ConnectorInitializationException(tokenRequestResult.Value.ErrorText);
        }

        _token = tokenRequestResult.Value.Data.AccessToken;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public async Task<IEnumerable<Permission>> GetAllPermissionsAsync()
    {
        //Получаем ИТРоли
        var rolePermissions = await GetPermissionsInternalAsync();

        //Получаем права
        var rightPermissions = await GetRightsInternalAsync();

        return rolePermissions.Concat(rightPermissions);
    }

    private async Task<IEnumerable<Permission>> GetPermissionsInternalAsync()
    {
        var permissionRequestResult = await GetAsync<RoleResponse>("api/v1/roles/all");

        if (!permissionRequestResult.IsSuccess)
        {
            throw new InvalidOperationException($"Не удалось получить роли: {permissionRequestResult.Error}");
        }

        if (!permissionRequestResult.Value.Success)
        {
            throw new InvalidOperationException($"Не удалось получить роли: {permissionRequestResult.Value.ErrorText}");
        }

        if (permissionRequestResult.Value.Data == null)
        {
            throw new InvalidOperationException("Не удалось получить роли");
        }
        
        return permissionRequestResult.Value.Data.Select(x => new Permission($"ItRole,{x.Id}", x.Name, x.CorporatePhoneNumber)); 
    }
    
    private async Task<IEnumerable<Permission>> GetRightsInternalAsync()
    {
        var rightsRequestResult = await GetAsync<RoleResponse>("api/v1/rights/all");

        if (!rightsRequestResult.IsSuccess)
        {
            throw new InvalidOperationException($"Не удалось получить права: {rightsRequestResult.Error}");
        }

        if (!rightsRequestResult.Value.Success)
        {
            throw new InvalidOperationException($"Не удалось получить права: {rightsRequestResult.Value.ErrorText}");
        }

        if (rightsRequestResult.Value.Data == null)
        {
            throw new InvalidOperationException("Не удалось получить права");
        }

        return rightsRequestResult.Value.Data.Select(x =>
            new Permission($"RequestRight,{x.Id}", x.Name, x.CorporatePhoneNumber));
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(string userLogin)
    {
        //Получаем ИТРоли
        var result1 = await GetUserPermissionsInternalAsync($"api/v1/users/{userLogin}/roles", x => $"ItRole,{x.Id}");

        //Получаем права
        var result2 = await GetUserPermissionsInternalAsync($"api/v1/users/{userLogin}/rights", x => $"RequestRight,{x.Id}");

        return result1.Concat(result2).ToList();
    }

    private async Task<IEnumerable<string>> GetUserPermissionsInternalAsync(string url, Func<RoleResponseData, string> builder)
    {
        var requestResult = await GetAsync<UserRoleResponse>(url);

        if (!requestResult.IsSuccess)
        {
            throw new InvalidOperationException($"Не удалось получить права пользователя: {requestResult.Error}");
        }

        if (!requestResult.Value.Success)
        {
            throw new InvalidOperationException($"Не удалось получить права пользователя: {requestResult.Value.ErrorText}");
        }

        return requestResult.Value.Data == null 
            ? throw new InvalidOperationException("Не удалось получить права пользователя")
            : requestResult.Value.Data.Select(builder).ToList();
    }

    public async Task AddUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds)
    {
        //проверяем что пользователь не залочен.
        var user = await GetUserAsync(userLogin);

        switch (user.Status)
        {
            //проверяем что пользователь не залочен.
            case "Lock":
            {
                Logger.Error($"Пользователь {userLogin} залочен.");
                throw new UserLockedException($"Пользователь {userLogin} залочен.");
            }
            //Назначаем права.
            case "Unlock":
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

                break;
            }
        }
    }

    public async Task RemoveUserPermissionsAsync(string userLogin, IEnumerable<string> rightIds)
    {
        //проверяем что пользователь не залочен.
        var user = await GetUserAsync(userLogin);

        switch (user.Status)
        {
            case "Lock":
            {
                Logger.Error($"Пользователь {userLogin} залочен.");
                throw new UserLockedException($"Невозможно получить свойства, пользователь {userLogin} залочен");
            }
            //отзываем права.
            case "Unlock":
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
                break;
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

    /// <summary>
    /// Возвращает список свойств пользователя.
    /// </summary>
    /// <param name="userLogin"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="UserNotFoundException"></exception>
    /// <exception cref="UserLockedException"></exception>
    public async Task<IEnumerable<UserProperty>> GetUserPropertiesAsync(string userLogin)
    {
        var user = await GetUserAsync(userLogin);

        if (user.Status == "Lock")
        {
            throw new UserLockedException($"Невозможно получить свойства, пользователь {userLogin} залочен");
        }

        return user.GetType().GetProperties()
            .Select(x => new UserProperty(x.Name, x.GetValue(user) as string));
    }

    private async Task<UserPropertyData> GetUserAsync(string userLogin)
    {
        var getUserResult = await GetAsync<UserPropertyResponse>($"api/v1/users/{userLogin}");

        if (!getUserResult.IsSuccess)
        {
            throw new InvalidOperationException($"Не удалось получить пользователя: {getUserResult.Error}");
        }

        if (getUserResult.Value.Data == null)
        {
            throw new UserNotFoundException($"Пользователь {userLogin} не найден");
        }

        return getUserResult.Value.Data;
    }

    public async Task UpdateUserPropertiesAsync(IEnumerable<UserProperty> properties, string userLogin)
    {
        var getUserResult = await GetAsync<UserPropertyResponse>($"api/v1/users/{userLogin}");

        if (!getUserResult.IsSuccess)
        {
            throw new InvalidOperationException($"Не удалось получить свойства пользователя: {getUserResult.Error}");
        }

        if (getUserResult.Value.Data == null)
        {
            throw new UserNotFoundException($"Пользователь {userLogin} не найден");
        }

        var user = getUserResult.Value.Data;

        if (user.Status == "Lock")
        {
            throw new UserLockedException($"Невозможно обновить свойства, пользователь {userLogin} залочен");
        }

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

        // TODO:
        var content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, AppJsonMimeType);
        await _httpClient.PutAsync("api/v1/users/edit", content);
    }

    public async Task<bool> IsUserExistsAsync(string userLogin)
    {
        var requestResult = await GetAsync<UserResponse>("api/v1/users/all");
        if (!requestResult.IsSuccess)
        {
            throw new InvalidOperationException($"Не удалось проверить наличие пользователя: {requestResult.Error}");
        }

        var user = requestResult.Value.Data.FirstOrDefault(x => x.Login == userLogin);
        return user != null;
    }

    /// <summary>
    /// Создаёт пользователя в системе.
    /// </summary>
    /// <param name="user"></param>
    public async Task CreateUserAsync(UserToCreate user)
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

        // TODO:
        var content = new StringContent(JsonSerializer.Serialize(newUser), Encoding.UTF8, AppJsonMimeType);
        await _httpClient.PostAsync("api/v1/users/create", content);

        //var result = await PostAsync<CreateUserDto>();
    }

    private async Task<RequestResult<T>> PostAsync<T, TT>(string endpoint, TT data)
    {
        try
        {
            var requestBody = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, AppJsonMimeType);
            var response = await _httpClient.PostAsync(endpoint, requestBody);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseBody))
            {
                return RequestResult<T>.Failed("Неверный ответ от сервера");
            }

            var result = JsonSerializer.Deserialize<T>(responseBody);

            return result == null 
                ? RequestResult<T>.Failed("Нет данных")
                : RequestResult<T>.Successful(result);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"Ошибка HTTP-запрос к {endpoint}: {ex.Message}");
            return RequestResult<T>.Failed($"Ошибка {ex.StatusCode} при запросе к серверу");
        }
        catch (JsonException ex)
        {
            Logger.Error($"Ошибка десериализации ответа сервера: {ex.Message}");
            return RequestResult<T>.Failed("Ошибка обработки ответа сервера");
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка при запросе к {endpoint}: {ex.Message}");
            return RequestResult<T>.Failed("Возникла ошибка при запросе к серверу");
        }
    }

    private async Task<RequestResult<T>> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(content))
            {
                return RequestResult<T>.Failed("Неверный ответ от сервера");
            }

            var result = JsonSerializer.Deserialize<T>(content);

            return result == null 
                ? RequestResult<T>.Failed("Нет данных")
                : RequestResult<T>.Successful(result);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"Ошибка HTTP-запрос к {endpoint}: {ex.Message}");
            return RequestResult<T>.Failed($"Ошибка {ex.StatusCode} при запросе к серверу");
        }
        catch (JsonException ex)
        {
            Logger.Error($"Ошибка десериализации ответа сервера: {ex.Message}");
            return RequestResult<T>.Failed("Ошибка обработки ответа сервера");
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка при запросе к {endpoint}: {ex.Message}");
            return RequestResult<T>.Failed("Возникла ошибка при запросе к серверу");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
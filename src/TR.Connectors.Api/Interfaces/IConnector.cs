using TR.Connectors.Api.Entities;

namespace TR.Connectors.Api.Interfaces;

public interface IConnector : IDisposable
{
    public ILogger Logger { get; }
    void SetLogger(ILogger logger);
    Task StartUpAsync(string connectionString);
    Task CreateUser(UserToCreate user);
    IEnumerable<Property> GetAllProperties();
    Task<IEnumerable<UserProperty>> GetUserProperties(string userLogin);
    Task<bool> IsUserExists(string userLogin);
    Task UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin);
    Task<IEnumerable<Permission>> GetAllPermissions();
    Task AddUserPermissions(string userLogin, IEnumerable<string> rightIds);
    Task RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds);
    Task<IEnumerable<string>> GetUserPermissions(string userLogin);
}

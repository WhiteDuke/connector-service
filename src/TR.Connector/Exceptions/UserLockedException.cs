namespace TR.Connector.Exceptions;

public sealed class UserLockedException: Exception
{
    public UserLockedException(string? message) : base(message)
    {
    }
}
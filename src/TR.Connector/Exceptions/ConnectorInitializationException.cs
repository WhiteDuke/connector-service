namespace TR.Connector.Exceptions;

public sealed class ConnectorInitializationException : Exception
{
    public ConnectorInitializationException(string? message) : base(message)
    {
    }

    public ConnectorInitializationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
namespace TR.Connector;

internal sealed class RequestResult<T>
{
    public bool IsSuccess { get; private set; }
    
    public string Error { get; private set; }
    
    public T Value { get; private set; }

    public static RequestResult<T> Successful(T result) => new()
    {
        IsSuccess = true,
        Value = result
    };

    public static RequestResult<T> Failed(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };
}
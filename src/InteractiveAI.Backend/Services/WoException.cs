namespace InteractiveAI.Backend.Services;

public class WoException : Exception
{
    public int StatusCode { get; }

    public WoException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

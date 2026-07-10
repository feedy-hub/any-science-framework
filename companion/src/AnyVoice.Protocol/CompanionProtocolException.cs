namespace AnyVoice.Protocol;

public sealed class CompanionProtocolException : Exception
{
    public CompanionProtocolException(string message)
        : base(message)
    {
    }

    public CompanionProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

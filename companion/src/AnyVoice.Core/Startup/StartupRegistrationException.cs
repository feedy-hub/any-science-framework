namespace AnyVoice.Core.Startup;

public sealed class StartupRegistrationException : Exception
{
    public StartupRegistrationException(string message)
        : base(message)
    {
    }

    public StartupRegistrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace AnyVoice.Core.Voice;

public sealed class VoiceOperationException : Exception
{
    public VoiceOperationException(string message)
        : base(message)
    {
    }

    public VoiceOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

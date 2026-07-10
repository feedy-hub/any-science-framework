using System.Text.RegularExpressions;

namespace AnyVoice.Protocol;

public static partial class CompanionEventValidator
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumTextLength = 8192;

    public static void Validate(CompanionEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.SchemaVersion != CurrentSchemaVersion)
        {
            throw new CompanionProtocolException("Unsupported schema version.");
        }

        if (!Enum.IsDefined(value.Type))
        {
            throw new CompanionProtocolException("Unknown event type.");
        }

        if (!SourcePattern().IsMatch(value.Source ?? string.Empty))
        {
            throw new CompanionProtocolException("Invalid event source.");
        }

        if (value.CreatedAtUtc == default || value.CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new CompanionProtocolException("Event timestamp must be UTC.");
        }

        if (value.Text?.Length > MaximumTextLength)
        {
            throw new CompanionProtocolException("Event text is too long.");
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex SourcePattern();
}

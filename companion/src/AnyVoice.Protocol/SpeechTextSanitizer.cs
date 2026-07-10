using System.Text.RegularExpressions;

namespace AnyVoice.Protocol;

public static partial class SpeechTextSanitizer
{
    public const int MaximumOutputLength = 320;

    public static string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var value = PrivateKeyPattern().Replace(input, " ");
        value = FencedCodePattern().Replace(value, " ");
        value = AuthorizationPattern().Replace(value, "$1[redacted]");
        value = CredentialUrlPattern().Replace(value, "[credential-url]");
        value = CredentialPattern().Replace(value, "$1$2[redacted]");
        value = QuotedWindowsPathPattern().Replace(value, "[path]");
        value = WindowsPathPattern().Replace(value, "[path]");
        value = UncPathPattern().Replace(value, "[path]");
        value = ControlCharacterPattern().Replace(value, " ");
        value = WhitespacePattern().Replace(value, " ").Trim();

        if (value.Length <= MaximumOutputLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, MaximumOutputLength - 3), "...");
    }

    [GeneratedRegex("(?is)-----BEGIN(?: [A-Z0-9]+)* PRIVATE KEY-----[\\s\\S]*?-----END(?: [A-Z0-9]+)* PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex("```[^\\r\\n]*\\r?\\n?[\\s\\S]*?```", RegexOptions.CultureInvariant)]
    private static partial Regex FencedCodePattern();

    [GeneratedRegex("(?im)\\b(Authorization\\s*:\\s*)(?:(?:Bearer|Basic)\\s+)?[^\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationPattern();

    [GeneratedRegex("(?i)\\bhttps?://[^\\s/@:]+:[^\\s/@]+@[^\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialUrlPattern();

    [GeneratedRegex("(?i)\\b([a-z0-9_.-]*(?:api[-_]?key|token|secret|password)[a-z0-9_.-]*)(\\s*[:=]\\s*)(?:\"[^\"]*\"|'[^']*'|[^\\s,;]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialPattern();

    [GeneratedRegex("(?i)(?:\"(?:[a-z]:\\\\|\\\\\\\\)[^\"\\r\\n]+\"|'(?:[a-z]:\\\\|\\\\\\\\)[^'\\r\\n]+')", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedWindowsPathPattern();

    [GeneratedRegex("(?i)(?<![a-z0-9_])(?:[a-z]:\\\\)(?:[^\\\\\\r\\n\\t ]+\\\\)*[^\\\\\\r\\n\\t ,;:!?]+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathPattern();

    [GeneratedRegex("(?i)(?<![a-z0-9_])\\\\\\\\[^\\s\\\\]+\\\\[^\\s,;:!?]+(?:\\\\[^\\s,;:!?]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex UncPathPattern();

    [GeneratedRegex("[\\u0000-\\u0008\\u000B\\u000C\\u000E-\\u001F\\u007F]", RegexOptions.CultureInvariant)]
    private static partial Regex ControlCharacterPattern();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}

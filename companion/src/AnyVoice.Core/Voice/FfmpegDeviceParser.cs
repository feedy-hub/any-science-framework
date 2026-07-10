using System.Text.RegularExpressions;

namespace AnyVoice.Core.Voice;

public static partial class FfmpegDeviceParser
{
    public static IReadOnlyList<string> ParseMicrophones(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AudioDevicePattern().Matches(output))
        {
            var name = match.Groups["name"].Value.Trim();
            if (name.Length > 0 && seen.Add(name))
            {
                result.Add(name);
            }
        }

        return result;
    }

    [GeneratedRegex("\"(?<name>[^\"\\r\\n]+)\"\\s+\\(audio\\)", RegexOptions.CultureInvariant)]
    private static partial Regex AudioDevicePattern();
}

using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace AnyVoice.Core;

public static partial class CompanionPipeNames
{
    public static string ForCurrentUser()
    {
        var identity = GetCurrentIdentity();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"AnyVoiceCompanion-{Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant()}";
    }

    public static string ForTests(string suffix)
    {
        if (!TestSuffixPattern().IsMatch(suffix ?? string.Empty))
        {
            throw new ArgumentException("Test pipe suffix is invalid.", nameof(suffix));
        }

        return $"AnyVoiceCompanion-test-{suffix}";
    }

    internal static void Validate(string pipeName)
    {
        if (!PipeNamePattern().IsMatch(pipeName ?? string.Empty))
        {
            throw new ArgumentException("Pipe name is invalid.", nameof(pipeName));
        }
    }

    private static string GetCurrentIdentity()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsIdentity.GetCurrent().User?.Value
                ?? throw new InvalidOperationException("The current Windows user has no SID.");
        }

        return $"{Environment.UserDomainName}\\{Environment.UserName}";
    }

    [GeneratedRegex("^[a-f0-9-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TestSuffixPattern();

    [GeneratedRegex("^AnyVoiceCompanion-[A-Za-z0-9-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex PipeNamePattern();
}

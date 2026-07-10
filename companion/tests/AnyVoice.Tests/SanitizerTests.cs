using AnyVoice.Protocol;

namespace AnyVoice.Tests;

internal static class SanitizerTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("plain text is preserved", () =>
        {
            suite.Equal("Build completed", SpeechTextSanitizer.Sanitize("Build completed"));
        });

        suite.Test("credential assignment is redacted", () =>
        {
            suite.Equal("token=[redacted]", SpeechTextSanitizer.Sanitize("token=abc123"));
            suite.Equal("api_key: [redacted]", SpeechTextSanitizer.Sanitize("api_key: very-secret"));
        });

        suite.Test("authorization header is redacted", () =>
        {
            suite.Equal(
                "Authorization: [redacted]",
                SpeechTextSanitizer.Sanitize("Authorization: Bearer abc.def.ghi"));
        });

        suite.Test("absolute Windows path is replaced", () =>
        {
            suite.Equal(
                "See [path]",
                SpeechTextSanitizer.Sanitize(@"See C:\Users\PS\secret.txt"));
            suite.Equal(
                "Open [path] now",
                SpeechTextSanitizer.Sanitize("Open \"C:\\Users\\PS\\My Files\\secret.txt\" now"));
            suite.Equal(
                "Copy [path] now",
                SpeechTextSanitizer.Sanitize("Copy \"\\\\server\\private share\\secret.txt\" now"));
        });

        suite.Test("private key block is removed", () =>
        {
            suite.Equal(
                "Before After",
                SpeechTextSanitizer.Sanitize(
                    "Before\n-----BEGIN PRIVATE KEY-----\nabc123\n-----END PRIVATE KEY-----\nAfter"));
        });

        suite.Test("URL credentials are removed", () =>
        {
            suite.Equal(
                "Visit [credential-url]",
                SpeechTextSanitizer.Sanitize("Visit https://user:password@example.com/private"));
        });

        suite.Test("fenced code is removed", () =>
        {
            suite.Equal(
                "Summary ready",
                SpeechTextSanitizer.Sanitize("Summary\n```powershell\nGet-Secret\n```\nready"));
        });

        suite.Test("control characters and whitespace are collapsed", () =>
        {
            suite.Equal("one two", SpeechTextSanitizer.Sanitize(" one\0\t\r\n two "));
        });

        suite.Test("speech output is capped", () =>
        {
            var result = SpeechTextSanitizer.Sanitize(new string('x', 500));

            suite.Equal(320, result.Length);
            suite.Equal(true, result.EndsWith("...", StringComparison.Ordinal));
        });

        suite.Test("null and unsafe-only input becomes empty", () =>
        {
            suite.Equal(string.Empty, SpeechTextSanitizer.Sanitize(null));
            suite.Equal(string.Empty, SpeechTextSanitizer.Sanitize("```text\nsecret\n```"));
        });
    }
}

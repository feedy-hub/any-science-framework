using AnyVoice.Protocol;

namespace AnyVoice.Tests;

internal static class ProtocolTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("valid event is accepted", () =>
        {
            var value = CompanionEvent.Create(CompanionEventType.Thinking, "codex", "Reviewing files");

            CompanionEventValidator.Validate(value);

            suite.Equal(1, value.SchemaVersion);
            suite.Equal(CompanionEventType.Thinking, value.Type);
            suite.Equal("codex", value.Source);
        });

        suite.Test("unsupported schema is rejected", () =>
        {
            var value = new CompanionEvent(
                2,
                CompanionEventType.Idle,
                "manual",
                null,
                DateTimeOffset.UtcNow);

            suite.Throws<CompanionProtocolException>(() => CompanionEventValidator.Validate(value));
        });

        suite.Test("invalid source is rejected", () =>
        {
            var value = CompanionEvent.Create(CompanionEventType.Success, "../../bad", "done");

            suite.Throws<CompanionProtocolException>(() => CompanionEventValidator.Validate(value));
        });

        suite.Test("default timestamp is rejected", () =>
        {
            var value = new CompanionEvent(
                1,
                CompanionEventType.Listening,
                "manual",
                null,
                default);

            suite.Throws<CompanionProtocolException>(() => CompanionEventValidator.Validate(value));
        });

        suite.Test("oversized input text is rejected", () =>
        {
            var value = CompanionEvent.Create(CompanionEventType.Speaking, "manual", new string('x', 8193));

            suite.Throws<CompanionProtocolException>(() => CompanionEventValidator.Validate(value));
        });
    }
}

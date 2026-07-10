using AnyVoice.Core.Voice;
using AnyVoice.Protocol;

namespace AnyVoice.Tests;

internal static class SpeechCoordinatorTests
{
    public static void Register(TestSuite suite)
    {
        suite.TestAsync("speech coordinator announces actionable external events", async () =>
        {
            var output = new FakeSpeechOutput();
            var coordinator = new SpeechCoordinator(output, () => true);

            await coordinator.NotifyAsync(CompanionEvent.Create(
                CompanionEventType.Success,
                "codex",
                "Task complete."));

            suite.Equal("Task complete.", output.Spoken.Single());
        });

        suite.TestAsync("speech coordinator ignores dictation and non-actionable events", async () =>
        {
            var output = new FakeSpeechOutput();
            var coordinator = new SpeechCoordinator(output, () => true);

            await coordinator.NotifyAsync(CompanionEvent.Create(
                CompanionEventType.Success,
                "dictation",
                "Raw transcript"));
            await coordinator.NotifyAsync(CompanionEvent.Create(
                CompanionEventType.Thinking,
                "claude",
                "Working"));

            suite.Equal(0, output.Spoken.Count);
        });

        suite.TestAsync("speech coordinator respects disabled speech", async () =>
        {
            var output = new FakeSpeechOutput();
            var coordinator = new SpeechCoordinator(output, () => false);

            await coordinator.NotifyAsync(CompanionEvent.Create(
                CompanionEventType.Error,
                "codex",
                "Needs attention."));

            suite.Equal(0, output.Spoken.Count);
        });
    }

    private sealed class FakeSpeechOutput : ISpeechOutput
    {
        public List<string> Spoken { get; } = [];

        public Task SpeakAsync(string? text, CancellationToken cancellationToken = default)
        {
            Spoken.Add(text ?? string.Empty);
            return Task.CompletedTask;
        }
    }
}

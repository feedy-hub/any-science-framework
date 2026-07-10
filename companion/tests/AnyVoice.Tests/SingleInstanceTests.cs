using AnyVoice.Core;

namespace AnyVoice.Tests;

internal static class SingleInstanceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("only one coordinator owns an instance name", () =>
        {
            var name = $"Local\\AnyVoiceCompanion-Test-{Guid.NewGuid():N}";
            using var first = new SingleInstanceCoordinator(name);
            bool? secondOwnsInstance = null;
            Exception? secondException = null;
            var secondThread = new Thread(() =>
            {
                try
                {
                    using var second = new SingleInstanceCoordinator(name);
                    secondOwnsInstance = second.OwnsInstance;
                }
                catch (Exception exception)
                {
                    secondException = exception;
                }
            });
            secondThread.Start();
            secondThread.Join();

            if (secondException is not null)
            {
                throw secondException;
            }

            suite.Equal(true, first.OwnsInstance);
            suite.Equal<bool?>(false, secondOwnsInstance);

            first.Dispose();
            using var third = new SingleInstanceCoordinator(name);
            suite.Equal(true, third.OwnsInstance);
        });

        suite.Test("production instance name does not expose user name", () =>
        {
            var name = SingleInstanceCoordinator.GetCurrentUserName();

            suite.Equal(true, name.StartsWith("Local\\AnyVoiceCompanion-", StringComparison.Ordinal));
            suite.Equal(false, name.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase));
        });
    }
}

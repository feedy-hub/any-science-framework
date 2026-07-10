using AnyVoice.Core;

namespace AnyVoice.Tests;

internal static class LocalPathTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("companion paths stay below local application data root", () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "anyvoice-path-test", "local-app-data");
            var paths = new CompanionPaths(root);
            var expectedBase = Path.GetFullPath(Path.Combine(root, "AnyVoiceCompanion"));

            suite.Equal(expectedBase, paths.BaseDirectory);
            suite.Equal(Path.Combine(expectedBase, "config.json"), paths.ConfigFile);
            suite.Equal(Path.Combine(expectedBase, "characters"), paths.CharactersDirectory);
            suite.Equal(Path.Combine(expectedBase, "logs"), paths.LogsDirectory);
        });

        suite.Test("empty local application data root is rejected", () =>
        {
            suite.Throws<ArgumentException>(() => new CompanionPaths(" "));
        });
    }
}

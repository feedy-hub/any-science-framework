using AnyVoice.Core.Startup;
using AnyVoice.Desktop;

namespace AnyVoice.Tests;

internal static class StartupRegistrationTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("startup command quotes verified absolute paths", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var dotnet = temporary.CreateFile("runtime path/dotnet.exe");
            var assembly = temporary.CreateFile("app path/AnyVoice.Desktop.dll");

            var command = StartupLaunchCommand.Build(dotnet, assembly);

            suite.Equal($"\"{dotnet}\" \"{assembly}\"", command);
        });

        suite.Test("startup command rejects relative or missing paths", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var dotnet = temporary.CreateFile("dotnet.exe");

            suite.Throws<StartupRegistrationException>(() =>
                StartupLaunchCommand.Build("dotnet.exe", dotnet));
            suite.Throws<StartupRegistrationException>(() =>
                StartupLaunchCommand.Build(dotnet, Path.Combine(temporary.Path, "missing.dll")));
        });

        suite.Test("startup registration enables idempotently and replaces stale command", () =>
        {
            var store = new FakeStartupValueStore
            {
                Value = "stale command",
            };
            var service = new StartupRegistrationService(store, "safe command");

            service.SetEnabled(true);
            service.SetEnabled(true);

            suite.Equal("safe command", store.Value);
            suite.Equal(1, store.SetCount);
            suite.Equal(true, service.IsEnabled());
        });

        suite.Test("startup registration disables only its fixed value", () =>
        {
            var store = new FakeStartupValueStore { Value = "safe command" };
            var service = new StartupRegistrationService(store, "safe command");

            service.SetEnabled(false);
            service.SetEnabled(false);

            suite.Equal<string?>(null, store.Value);
            suite.Equal(1, store.DeleteCount);
            suite.Equal(StartupRegistrationService.ValueName, store.LastDeletedName);
        });

        suite.Test("startup registration wraps value store failures", () =>
        {
            var store = new FakeStartupValueStore { Exception = new InvalidOperationException("fail") };
            var service = new StartupRegistrationService(store, "safe command");

            suite.Throws<StartupRegistrationException>(() => service.SetEnabled(true));
        });

        suite.Test("current application startup command uses dotnet host and entry assembly", () =>
        {
            var command = CurrentApplicationStartupCommand.Build();

            suite.Equal(true, command.Contains("dotnet.exe\"", StringComparison.OrdinalIgnoreCase));
            suite.Equal(true, command.Contains("AnyVoice.Tests.dll\"", StringComparison.OrdinalIgnoreCase));
        });
    }

    private sealed class FakeStartupValueStore : IStartupValueStore
    {
        public string? Value { get; set; }

        public int SetCount { get; private set; }

        public int DeleteCount { get; private set; }

        public string? LastDeletedName { get; private set; }

        public Exception? Exception { get; init; }

        public string? GetValue(string name)
        {
            ThrowIfRequested();
            return Value;
        }

        public void SetValue(string name, string value)
        {
            ThrowIfRequested();
            SetCount++;
            Value = value;
        }

        public void DeleteValue(string name)
        {
            ThrowIfRequested();
            DeleteCount++;
            LastDeletedName = name;
            Value = null;
        }

        private void ThrowIfRequested()
        {
            if (Exception is not null)
            {
                throw Exception;
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "anyvoice-startup-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public string CreateFile(string relativePath)
        {
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, relativePath));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "test");
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

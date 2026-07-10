namespace AnyVoice.Tests;

internal sealed class TestSuite
{
    private readonly List<(string Name, Func<Task> Body)> tests = [];

    public int RegisteredCount => tests.Count;

    public void Test(string name, Action body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);
        tests.Add((name, () =>
        {
            body();
            return Task.CompletedTask;
        }));
    }

    public void TestAsync(string name, Func<Task> body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);
        tests.Add((name, body));
    }

    public void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new TestFailureException($"Expected <{expected}>, got <{actual}>.");
        }
    }

    public TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new TestFailureException(
                $"Expected {typeof(TException).Name}, got {exception.GetType().Name}.",
                exception);
        }

        throw new TestFailureException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    public async Task<int> RunAsync()
    {
        var failures = 0;
        foreach (var (name, body) in tests)
        {
            try
            {
                await body().ConfigureAwait(false);
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"RESULT {tests.Count - failures}/{tests.Count} passed");
        return failures == 0 ? 0 : 1;
    }

    private sealed class TestFailureException : Exception
    {
        public TestFailureException(string message)
            : base(message)
        {
        }

        public TestFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

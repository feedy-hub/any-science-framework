namespace AnyVoice.Core;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex mutex;
    private bool disposed;

    public SingleInstanceCoordinator(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        mutex = new Mutex(initiallyOwned: false, name);
        try
        {
            OwnsInstance = mutex.WaitOne(millisecondsTimeout: 0, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            OwnsInstance = true;
        }
    }

    public bool OwnsInstance { get; }

    public static string GetCurrentUserName()
    {
        return $"Local\\{CompanionPipeNames.ForCurrentUser()}";
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (OwnsInstance)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
    }
}

namespace AnyVoice.Core.Startup;

public sealed class StartupRegistrationService
{
    public const string ValueName = "AnyVoiceCompanion";

    private readonly IStartupValueStore valueStore;
    private readonly string launchCommand;

    public StartupRegistrationService(IStartupValueStore valueStore, string launchCommand)
    {
        this.valueStore = valueStore ?? throw new ArgumentNullException(nameof(valueStore));
        ArgumentException.ThrowIfNullOrWhiteSpace(launchCommand);
        this.launchCommand = launchCommand;
    }

    public bool IsEnabled()
    {
        return Execute(() => string.Equals(
            valueStore.GetValue(ValueName),
            launchCommand,
            StringComparison.Ordinal));
    }

    public void SetEnabled(bool enabled)
    {
        Execute(() =>
        {
            var current = valueStore.GetValue(ValueName);
            if (enabled)
            {
                if (!string.Equals(current, launchCommand, StringComparison.Ordinal))
                {
                    valueStore.SetValue(ValueName, launchCommand);
                }
            }
            else if (current is not null)
            {
                valueStore.DeleteValue(ValueName);
            }

            return true;
        });
    }

    private static T Execute<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (StartupRegistrationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StartupRegistrationException(
                "Windows startup registration could not be updated.",
                exception);
        }
    }
}

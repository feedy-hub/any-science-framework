namespace AnyVoice.Core.Startup;

public interface IStartupValueStore
{
    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name);
}

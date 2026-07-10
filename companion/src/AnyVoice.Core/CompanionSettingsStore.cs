using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace AnyVoice.Core;

public sealed class CompanionSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly string configFile;

    public CompanionSettingsStore(string configFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFile);
        this.configFile = Path.GetFullPath(configFile);
    }

    public CompanionSettings Load()
    {
        if (!File.Exists(configFile))
        {
            return CompanionSettings.Default;
        }

        try
        {
            var json = File.ReadAllText(configFile, Encoding.UTF8);
            return (JsonSerializer.Deserialize<CompanionSettings>(json, SerializerOptions)
                ?? CompanionSettings.Default).Normalize();
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            QuarantineInvalidFile();
            return CompanionSettings.Default;
        }
    }

    public void Save(CompanionSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Normalize();
        var directory = Path.GetDirectoryName(configFile)
            ?? throw new InvalidOperationException("Settings path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryFile = $"{configFile}.tmp-{Guid.NewGuid():N}";
        try
        {
            var json = JsonSerializer.Serialize(normalized, SerializerOptions);
            File.WriteAllText(temporaryFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryFile, configFile, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }

    private void QuarantineInvalidFile()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var quarantine = $"{configFile}.invalid-{timestamp}-{Guid.NewGuid():N}";
        File.Move(configFile, quarantine);
    }
}

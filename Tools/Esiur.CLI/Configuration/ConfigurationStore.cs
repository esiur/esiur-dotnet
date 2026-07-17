using System.Text.Json;

namespace Esiur.CLI.Configuration;

public interface IConfigurationStore
{
    Task<CliConfiguration> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(CliConfiguration configuration, CancellationToken cancellationToken);
}

public sealed class ConfigurationStore : IConfigurationStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public ConfigurationStore(string? path = null) => Path = path ?? GetDefaultPath();

    public string Path { get; }

    public static string GetDefaultPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return System.IO.Path.Combine(appData, "Esiur", "config.json");
        }

        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome))
            configHome = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return System.IO.Path.Combine(configHome, "esiur", "config.json");
    }

    public async Task<CliConfiguration> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Path))
            return new CliConfiguration();

        await using var stream = File.OpenRead(Path);
        var value = await JsonSerializer.DeserializeAsync<CliConfiguration>(stream, JsonOptions, cancellationToken)
            ?? new CliConfiguration();
        value.Profiles = new Dictionary<string, ConnectionProfile>(
            value.Profiles ?? [], StringComparer.OrdinalIgnoreCase);
        return value;
    }

    public async Task SaveAsync(CliConfiguration configuration, CancellationToken cancellationToken)
    {
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken);
        File.Move(temporary, Path, true);
    }
}

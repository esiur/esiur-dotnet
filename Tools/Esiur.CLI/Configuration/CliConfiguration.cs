namespace Esiur.CLI.Configuration;

public sealed class CliConfiguration
{
    public string? DefaultProfile { get; set; }
    public string OutputFormat { get; set; } = "table";
    public string Timeout { get; set; } = "30s";
    public Dictionary<string, ConnectionProfile> Profiles { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

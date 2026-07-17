namespace Esiur.CLI.Configuration;

public sealed class ConnectionProfile
{
    public required string Name { get; init; }
    public required string Endpoint { get; init; }
    public string? Provider { get; init; }
    public string? Identity { get; init; }
    public string? Domain { get; init; }
    public string? DefaultResourcePath { get; init; }
    public string OutputFormat { get; init; } = "table";
}

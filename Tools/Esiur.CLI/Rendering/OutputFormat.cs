namespace Esiur.CLI;

public enum OutputFormat { Table, Json, JsonLines, Raw }

public static class OutputFormatExtensions
{
    public static OutputFormat Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "table" or null => OutputFormat.Table,
        "json" => OutputFormat.Json,
        "jsonl" => OutputFormat.JsonLines,
        "raw" => OutputFormat.Raw,
        _ => throw new CliException(
            $"Output format \"{value}\" is invalid. Expected table, json, jsonl, or raw.",
            ExitCodes.InvalidArguments),
    };
}

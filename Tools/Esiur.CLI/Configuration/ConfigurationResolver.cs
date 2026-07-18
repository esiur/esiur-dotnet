namespace Esiur.CLI.Configuration;

public sealed record GlobalOptions(
    string? Profile,
    string? Endpoint,
    string? Provider,
    string? Identity,
    string? Output,
    TimeSpan? Timeout,
    bool Verbose,
    bool Debug);

public sealed record ResolvedConnection(
    string DisplayName,
    string Endpoint,
    string? Provider,
    string? Identity,
    string? Domain,
    string OutputFormat,
    TimeSpan Timeout,
    ConnectionProfile? Profile);

public static class ConfigurationResolver
{
    public static ResolvedConnection Resolve(CliConfiguration configuration, GlobalOptions options)
    {
        var profileName = options.Profile
            ?? Environment.GetEnvironmentVariable("ESIUR_PROFILE")
            ?? configuration.DefaultProfile;
        ConnectionProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(profileName)
            && !configuration.Profiles.TryGetValue(profileName, out profile))
            throw new CliException($"Profile \"{profileName}\" was not found.", ExitCodes.InvalidArguments);

        var endpoint = options.Endpoint
            ?? Environment.GetEnvironmentVariable("ESIUR_ENDPOINT")
            ?? profile?.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new CliException("No endpoint was supplied and no profile is selected.", ExitCodes.InvalidArguments);

        ValidateEndpoint(endpoint);
        var output = options.Output
            ?? Environment.GetEnvironmentVariable("ESIUR_OUTPUT")
            ?? profile?.OutputFormat
            ?? configuration.OutputFormat;
        OutputFormatExtensions.Parse(output);

        var timeout = options.Timeout
            ?? DurationParser.TryParse(Environment.GetEnvironmentVariable("ESIUR_TIMEOUT"))
            ?? DurationParser.Parse(configuration.Timeout);

        return new ResolvedConnection(
            profile?.Name ?? "endpoint",
            endpoint,
            options.Provider ?? Environment.GetEnvironmentVariable("ESIUR_PROVIDER") ?? profile?.Provider,
            options.Identity ?? Environment.GetEnvironmentVariable("ESIUR_IDENTITY") ?? profile?.Identity,
            profile?.Domain,
            output,
            timeout,
            profile);
    }

    static readonly string[] ValidSchemes = ["ep", "eps", "ws", "wss"];

    /// <summary>
    /// Accepts <c>ep(s)://host:port</c> (a bare Esiur endpoint, connects at the
    /// WebSocket root) as well as <c>ws(s)://host:port/path</c> (for hosts like
    /// ASP.NET Core's <c>MapEsiur("/esiur")</c> that mount the WebSocket route
    /// somewhere other than root) — see <see cref="EndpointParser"/> for how
    /// the two forms are dialed.
    /// </summary>
    public static void ValidateEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || !ValidSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new CliException($"Endpoint \"{endpoint}\" is not a valid ep:// or ws:// endpoint.", ExitCodes.InvalidArguments);
    }
}

public static class DurationParser
{
    public static TimeSpan Parse(string value) => TryParse(value)
        ?? throw new CliException($"Duration \"{value}\" is invalid. Use values such as 500ms, 30s, 5m, or 1h.", ExitCodes.InvalidArguments);

    public static TimeSpan? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        var suffixes = new (string Suffix, double Factor)[]
        {
            ("ms", 1), ("s", 1_000), ("m", 60_000), ("h", 3_600_000),
        };
        foreach (var item in suffixes)
            if (value.EndsWith(item.Suffix, StringComparison.OrdinalIgnoreCase)
                && double.TryParse(value[..^item.Suffix.Length],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var number)
                && number >= 0)
                return TimeSpan.FromMilliseconds(number * item.Factor);
        return TimeSpan.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed >= TimeSpan.Zero ? parsed : null;
    }
}

using System.Diagnostics;
using Esiur.CLI.Authentication;
using Esiur.CLI.Client;
using Esiur.CLI.Configuration;
using Esiur.CLI.Rendering;

namespace Esiur.CLI;

public sealed class CliApplication
{
    readonly IConfigurationStore configurationStore;
    readonly ICredentialService credentials;
    readonly IEsiurSessionFactory sessions;
    readonly ResourceInspectionService resources;
    readonly TextReader input;
    readonly TextWriter output;
    readonly TextWriter error;

    public CliApplication(
        IConfigurationStore configurationStore,
        ICredentialService credentials,
        IEsiurSessionFactory sessions,
        ResourceInspectionService resources,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        this.configurationStore = configurationStore;
        this.credentials = credentials;
        this.sessions = sessions;
        this.resources = resources;
        this.input = input;
        this.output = output;
        this.error = error;
    }

    public static async Task<int> RunAsync(
        string[] arguments, TextReader input, TextWriter output, TextWriter error)
    {
        var credentials = new PromptCredentialService();
        var app = new CliApplication(
            new ConfigurationStore(), credentials, new EsiurSessionFactory(credentials),
            new ResourceInspectionService(), input, output, error);
        return await app.RunAsync(arguments, CancellationToken.None);
    }

    public async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler? handler = null;
        if (!Console.IsInputRedirected || !Console.IsOutputRedirected)
        {
            handler = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += handler;
        }

        GlobalOptions? global = null;
        try
        {
            var tokens = arguments.ToList();
            global = ParseGlobalOptions(tokens);
            if (tokens.Count == 0 || tokens[0] is "help" or "--help" or "-h")
            {
                await PrintHelpAsync();
                return tokens.Count == 0 ? ExitCodes.InvalidArguments : ExitCodes.Success;
            }

            var command = Take(tokens).ToLowerInvariant();
            var configuration = await configurationStore.LoadAsync(cancellation.Token);
            return command switch
            {
                "version" => await VersionAsync(tokens),
                "login" => await LoginAsync(tokens, configuration, global, cancellation.Token),
                "logout" => await LogoutAsync(tokens, configuration, global, cancellation.Token),
                "profile" => await ProfileAsync(tokens, configuration, global, cancellation.Token),
                "query" => await QueryAsync(tokens, configuration, global, cancellation.Token),
                "describe" => await DescribeAsync(tokens, configuration, global, cancellation.Token),
                "get" => await GetAsync(tokens, configuration, global, cancellation.Token),
                _ => throw new CliException($"Unknown command \"{command}\".", ExitCodes.InvalidArguments),
            };
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Error: Operation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (CliException exception)
        {
            await error.WriteLineAsync($"Error: {RedactSecrets(exception.Message)}");
            if (global?.Debug == true && exception.InnerException is not null)
                await error.WriteLineAsync(RedactSecrets(exception.InnerException.ToString()));
            return exception.ExitCode;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync($"Error: {RedactSecrets(exception.Message)}");
            if (global?.Debug == true) await error.WriteLineAsync(RedactSecrets(exception.ToString()));
            return ExitCodes.GeneralFailure;
        }
        finally
        {
            if (handler is not null) Console.CancelKeyPress -= handler;
        }
    }

    async Task<int> VersionAsync(List<string> tokens)
    {
        EnsureEmpty(tokens);
        var assembly = typeof(CliApplication).Assembly.GetName();
        await output.WriteLineAsync($"Esiur CLI {assembly.Version?.ToString(3) ?? "3.0.0"}");
        return ExitCodes.Success;
    }

    async Task<int> LoginAsync(
        List<string> tokens, CliConfiguration configuration, GlobalOptions global,
        CancellationToken cancellationToken)
    {
        if (tokens.Count < 2)
            throw new CliException("Usage: esiur login <name> <ep://endpoint> [--provider password] [--identity <name>] [--password-stdin]", ExitCodes.InvalidArguments);
        var name = Take(tokens);
        var endpoint = Take(tokens);
        var passwordStdin = TakeFlag(tokens, "--password-stdin");
        var domain = TakeOption(tokens, "--domain");
        EnsureEmpty(tokens);
        ConfigurationResolver.ValidateEndpoint(endpoint);
        var provider = global.Provider ?? (string.IsNullOrWhiteSpace(global.Identity) ? null : "password");
        if (!string.IsNullOrWhiteSpace(provider) && !PromptCredentialService.IsPasswordProvider(provider))
            throw new CliException($"Authentication provider \"{provider}\" is not supported by this CLI build.", ExitCodes.InvalidArguments);
        var profile = new ConnectionProfile
        {
            Name = name,
            Endpoint = endpoint,
            Provider = provider,
            Identity = global.Identity,
            Domain = domain,
            OutputFormat = global.Output ?? configuration.OutputFormat,
        };
        var temporary = new CliConfiguration
        {
            DefaultProfile = name,
            OutputFormat = configuration.OutputFormat,
            Timeout = configuration.Timeout,
            Profiles = new Dictionary<string, ConnectionProfile>(StringComparer.OrdinalIgnoreCase)
            {
                [name] = profile,
            },
        };
        var resolved = ConfigurationResolver.Resolve(temporary, global with
        {
            Profile = name, Endpoint = null, Provider = null, Identity = null,
        });
        await using (await sessions.ConnectAsync(
            resolved, passwordStdin, input, error, cancellationToken)) { }

        configuration.Profiles[name] = profile;
        configuration.DefaultProfile ??= name;
        await configurationStore.SaveAsync(configuration, cancellationToken);
        await output.WriteLineAsync($"Profile \"{name}\" saved. Credentials were not written to the profile file.");
        return ExitCodes.Success;
    }

    async Task<int> LogoutAsync(
        List<string> tokens, CliConfiguration configuration, GlobalOptions global,
        CancellationToken cancellationToken)
    {
        var name = tokens.Count > 0 ? Take(tokens) : global.Profile ?? configuration.DefaultProfile;
        EnsureEmpty(tokens);
        if (string.IsNullOrWhiteSpace(name))
            throw new CliException("No profile is selected.", ExitCodes.InvalidArguments);
        if (!configuration.Profiles.ContainsKey(name))
            throw new CliException($"Profile \"{name}\" was not found.", ExitCodes.InvalidArguments);
        await credentials.RemoveAsync(name, cancellationToken);
        await output.WriteLineAsync($"Logged out of profile \"{name}\". The profile was retained.");
        return ExitCodes.Success;
    }

    async Task<int> ProfileAsync(
        List<string> tokens, CliConfiguration configuration, GlobalOptions global,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0)
            throw new CliException("Usage: esiur profile <list|show|use|remove>", ExitCodes.InvalidArguments);
        var operation = Take(tokens).ToLowerInvariant();
        var renderer = new OutputRenderer(output);
        var format = OutputFormatExtensions.Parse(global.Output ?? configuration.OutputFormat);
        switch (operation)
        {
            case "list":
                EnsureEmpty(tokens);
                var profiles = configuration.Profiles.Values.OrderBy(x => x.Name).Select(x => new
                {
                    x.Name,
                    x.Endpoint,
                    x.Provider,
                    x.Identity,
                    Default = string.Equals(x.Name, configuration.DefaultProfile, StringComparison.OrdinalIgnoreCase),
                }).ToArray();
                await renderer.RenderAsync(profiles, format, cancellationToken);
                break;
            case "show":
                var shown = RequiredProfile(tokens, configuration);
                EnsureEmpty(tokens);
                await renderer.RenderAsync(shown, format, cancellationToken);
                break;
            case "use":
                var selected = RequiredProfile(tokens, configuration);
                EnsureEmpty(tokens);
                configuration.DefaultProfile = selected.Name;
                await configurationStore.SaveAsync(configuration, cancellationToken);
                await output.WriteLineAsync($"Default profile set to \"{selected.Name}\".");
                break;
            case "remove":
                var removed = RequiredProfile(tokens, configuration);
                EnsureEmpty(tokens);
                configuration.Profiles.Remove(removed.Name);
                if (string.Equals(configuration.DefaultProfile, removed.Name, StringComparison.OrdinalIgnoreCase))
                    configuration.DefaultProfile = null;
                await credentials.RemoveAsync(removed.Name, cancellationToken);
                await configurationStore.SaveAsync(configuration, cancellationToken);
                await output.WriteLineAsync($"Profile \"{removed.Name}\" removed.");
                break;
            default:
                throw new CliException($"Unknown profile command \"{operation}\".", ExitCodes.InvalidArguments);
        }
        return ExitCodes.Success;
    }

    async Task<int> QueryAsync(
        List<string> tokens, CliConfiguration configuration, GlobalOptions global,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0) throw new CliException("Usage: esiur query <path> [--recursive|--depth <number>] [--type <name>]", ExitCodes.InvalidArguments);
        var path = Take(tokens);
        var recursive = TakeFlag(tokens, "--recursive");
        var depthText = TakeOption(tokens, "--depth");
        var type = TakeOption(tokens, "--type");
        EnsureEmpty(tokens);
        var depth = recursive ? int.MaxValue : depthText is null ? 1
            : int.TryParse(depthText, out var parsed) ? parsed
            : throw new CliException($"Depth \"{depthText}\" is invalid.", ExitCodes.InvalidArguments);
        var settings = ConfigurationResolver.Resolve(configuration, global);
        using var timeout = CreateTimeout(settings.Timeout, cancellationToken);
        try
        {
            await using var session = await sessions.ConnectAsync(settings, false, input, error, timeout.Token);
            var result = await resources.QueryAsync(session, path, depth, type, timeout.Token);
            await new OutputRenderer(output).RenderAsync(result, OutputFormatExtensions.Parse(settings.OutputFormat), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CliException("The operation timed out.", ExitCodes.Timeout);
        }
        return ExitCodes.Success;
    }

    async Task<int> DescribeAsync(
        List<string> tokens, CliConfiguration configuration, GlobalOptions global,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0) throw new CliException("Usage: esiur describe <path> [--values|--schema-only]", ExitCodes.InvalidArguments);
        var path = Take(tokens);
        var values = TakeFlag(tokens, "--values");
        var schemaOnly = TakeFlag(tokens, "--schema-only");
        if (values && schemaOnly) throw new CliException("--values and --schema-only cannot be combined.", ExitCodes.InvalidArguments);
        EnsureEmpty(tokens);
        var settings = ConfigurationResolver.Resolve(configuration, global);
        using var timeout = CreateTimeout(settings.Timeout, cancellationToken);
        try
        {
            await using var session = await sessions.ConnectAsync(settings, false, input, error, timeout.Token);
            var result = await resources.DescribeAsync(session, path, values && !schemaOnly, timeout.Token);
            await new OutputRenderer(output).RenderAsync(result, OutputFormatExtensions.Parse(settings.OutputFormat), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CliException("The operation timed out.", ExitCodes.Timeout);
        }
        return ExitCodes.Success;
    }

    async Task<int> GetAsync(
        List<string> tokens, CliConfiguration configuration, GlobalOptions global,
        CancellationToken cancellationToken)
    {
        if (tokens.Count < 2) throw new CliException("Usage: esiur get <path> <property> [property...]", ExitCodes.InvalidArguments);
        var path = Take(tokens);
        var members = tokens.ToArray();
        tokens.Clear();
        var settings = ConfigurationResolver.Resolve(configuration, global);
        using var timeout = CreateTimeout(settings.Timeout, cancellationToken);
        try
        {
            await using var session = await sessions.ConnectAsync(settings, false, input, error, timeout.Token);
            var result = await resources.GetAsync(session, path, members, timeout.Token);
            var format = OutputFormatExtensions.Parse(settings.OutputFormat);
            await new OutputRenderer(output).RenderAsync(result, format, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CliException("The operation timed out.", ExitCodes.Timeout);
        }
        return ExitCodes.Success;
    }

    static CancellationTokenSource CreateTimeout(TimeSpan duration, CancellationToken parent)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(parent);
        if (duration > TimeSpan.Zero) source.CancelAfter(duration);
        return source;
    }

    static ConnectionProfile RequiredProfile(List<string> tokens, CliConfiguration configuration)
    {
        if (tokens.Count == 0) throw new CliException("A profile name is required.", ExitCodes.InvalidArguments);
        var name = Take(tokens);
        return configuration.Profiles.TryGetValue(name, out var profile) ? profile
            : throw new CliException($"Profile \"{name}\" was not found.", ExitCodes.InvalidArguments);
    }

    static GlobalOptions ParseGlobalOptions(List<string> tokens)
    {
        var profile = TakeOption(tokens, "--profile");
        var endpoint = TakeOption(tokens, "--endpoint");
        var provider = TakeOption(tokens, "--provider");
        var identity = TakeOption(tokens, "--identity");
        var output = TakeOption(tokens, "--output");
        var timeoutText = TakeOption(tokens, "--timeout");
        return new GlobalOptions(profile, endpoint, provider, identity, output,
            timeoutText is null ? null : DurationParser.Parse(timeoutText),
            TakeFlag(tokens, "--verbose"), TakeFlag(tokens, "--debug"));
    }

    public static string? TakeOption(List<string> tokens, string name)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].StartsWith(name + "=", StringComparison.Ordinal))
            {
                var value = tokens[index][(name.Length + 1)..];
                tokens.RemoveAt(index);
                return value;
            }
            if (tokens[index] != name) continue;
            if (index + 1 >= tokens.Count)
                throw new CliException($"Option {name} requires a value.", ExitCodes.InvalidArguments);
            var result = tokens[index + 1];
            tokens.RemoveRange(index, 2);
            return result;
        }
        return null;
    }

    public static bool TakeFlag(List<string> tokens, string name)
    {
        var index = tokens.IndexOf(name);
        if (index < 0) return false;
        tokens.RemoveAt(index);
        return true;
    }

    static string Take(List<string> tokens)
    {
        var value = tokens[0];
        tokens.RemoveAt(0);
        return value;
    }

    static void EnsureEmpty(List<string> tokens)
    {
        if (tokens.Count > 0)
            throw new CliException($"Unexpected argument \"{tokens[0]}\".", ExitCodes.InvalidArguments);
    }

    public static string RedactSecrets(string value) => System.Text.RegularExpressions.Regex.Replace(
        value, "(?i)(password|token)(\\s*[:=]\\s*)[^\\s,;]+", "$1$2***");

    async Task PrintHelpAsync() => await output.WriteLineAsync(
        """
        Usage: esiur [global options] <command> [arguments]

        Commands:
          version
          login <name> <ep://endpoint> [--provider password] [--identity <name>] [--password-stdin]
          logout [name]
          profile list|show|use|remove [name]
          query <path> [--recursive|--depth <number>] [--type <name>]
          describe <path> [--values|--schema-only]
          get <path> <property> [property...]

        Global options:
          --profile <name>       Use a saved profile
          --endpoint <ep://...>  Temporarily override the endpoint
          --output <format>      table, json, jsonl, or raw
          --timeout <duration>   For example 30s or 2m
          --verbose              Show diagnostics
          --debug                Show exception details
        """);
}

using Esiur.CLI.Rendering;
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.CLI.Client;

public sealed record ResourceSummary(
    string Name,
    string Path,
    uint SessionId,
    string Type,
    string TypeId,
    int ChildCount);

public sealed record PropertyDescription(
    string Name,
    byte Index,
    string Type,
    bool ReadOnly,
    bool Constant,
    bool Historical,
    bool Inherited,
    object? Value,
    IReadOnlyDictionary<string, string> Annotations);

public sealed record FunctionDescription(
    string Name,
    byte Index,
    string Arguments,
    string ReturnType,
    string Flags,
    bool Inherited,
    IReadOnlyDictionary<string, string> Annotations);

public sealed record EventDescription(
    string Name,
    byte Index,
    string ArgumentType,
    bool Subscribable,
    bool Inherited,
    IReadOnlyDictionary<string, string> Annotations);

public sealed record ConstantDescription(
    string Name,
    byte Index,
    string Type,
    object? Value,
    bool Inherited,
    IReadOnlyDictionary<string, string> Annotations);

public sealed record ResourceDescription(
    string Path,
    string Type,
    string TypeId,
    uint SessionId,
    ulong Age,
    string? ParentTypeId,
    IReadOnlyDictionary<string, string> Annotations,
    IReadOnlyList<PropertyDescription> Properties,
    IReadOnlyList<FunctionDescription> Functions,
    IReadOnlyList<EventDescription> Events,
    IReadOnlyList<ConstantDescription> Constants);

public sealed record PropertyResult(string Resource, string Property, byte Index, object? Value);

public sealed class ResourceInspectionService
{
    public async Task<IReadOnlyList<ResourceSummary>> QueryAsync(
        EsiurSession session,
        string path,
        int depth,
        string? typeFilter,
        CancellationToken cancellationToken)
    {
        if (depth < 1) throw new CliException("Query depth must be at least 1.", ExitCodes.InvalidArguments);
        var output = new List<ResourceSummary>();
        var queue = new Queue<(string Path, int Level)>();
        var seen = new HashSet<uint>();
        queue.Enqueue((NormalizePath(path), 1));

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = queue.Dequeue();
            IResource[] children;
            try
            {
                children = await EsiurSessionFactory.AwaitReply(
                    session.Connection.Query(current.Path), cancellationToken) ?? [];
            }
            catch (Exception exception)
            {
                throw MapResourceException(current.Path, exception);
            }

            foreach (var child in children.OfType<EpResource>())
            {
                if (!seen.Add(child.ResourceInstanceId)) continue;
                var childPath = NormalizePath(child.ResourceLink ?? child.Instance.Link);
                var definition = child.Instance.Definition;
                var childCount = 0;
                try
                {
                    var descendants = await EsiurSessionFactory.AwaitReply(
                        session.Connection.Query(childPath), cancellationToken);
                    childCount = descendants?.Length ?? 0;
                }
                catch { }

                if (string.IsNullOrWhiteSpace(typeFilter)
                    || definition.Name.Contains(typeFilter, StringComparison.OrdinalIgnoreCase))
                {
                    output.Add(new ResourceSummary(
                        child.Instance.Name,
                        childPath,
                        child.ResourceInstanceId,
                        definition.Name,
                        TypeIdFormatter.Format(definition.Id),
                        childCount));
                }

                if (current.Level < depth)
                    queue.Enqueue((childPath, current.Level + 1));
            }
        }

        return output;
    }

    public async Task<ResourceDescription> DescribeAsync(
        EsiurSession session,
        string path,
        bool includeValues,
        CancellationToken cancellationToken)
    {
        var resource = await ResolveAsync(session, path, cancellationToken);
        var definition = resource.Instance.Definition;
        return new ResourceDescription(
            NormalizePath(resource.ResourceLink ?? path),
            definition.Name,
            TypeIdFormatter.Format(definition.Id),
            resource.ResourceInstanceId,
            resource.Instance.Age,
            definition.ParentTypeId is ulong parent ? TypeIdFormatter.Format(parent) : null,
            ToDictionary(definition.Annotations),
            definition.Properties.OrderBy(x => x.Index).Select(property =>
                new PropertyDescription(
                    property.Name,
                    property.Index,
                    TypeFormatter.Format(property.ValueType),
                    property.ReadOnly,
                    property.Constant,
                    property.Historical,
                    property.Inherited,
                    includeValues && resource.TryGetPropertyValue(property.Index, out var value) ? value : null,
                    ToDictionary(property.Annotations))).ToArray(),
            definition.Functions.OrderBy(x => x.Index).Select(function =>
                new FunctionDescription(
                    function.Name,
                    function.Index,
                    string.Join(", ", (function.Arguments ?? []).Select(argument =>
                        $"{argument.Name}: {TypeFormatter.Format(argument.Type)}{(argument.Optional ? " = optional" : "")}")),
                    TypeFormatter.Format(function.ReturnType),
                    FunctionFlags(function),
                    function.Inherited,
                    ToDictionary(function.Annotations))).ToArray(),
            definition.Events.OrderBy(x => x.Index).Select(@event =>
                new EventDescription(
                    @event.Name,
                    @event.Index,
                    TypeFormatter.Format(@event.ArgumentType),
                    @event.Subscribable,
                    @event.Inherited,
                    ToDictionary(@event.Annotations))).ToArray(),
            definition.Constants.OrderBy(x => x.Index).Select(constant =>
                new ConstantDescription(
                    constant.Name,
                    constant.Index,
                    TypeFormatter.Format(constant.ValueType),
                    constant.Value,
                    constant.Inherited,
                    ToDictionary(constant.Annotations))).ToArray());
    }

    public async Task<IReadOnlyList<PropertyResult>> GetAsync(
        EsiurSession session,
        string path,
        IReadOnlyList<string> members,
        CancellationToken cancellationToken)
    {
        var resource = await ResolveAsync(session, path, cancellationToken);
        var results = new List<PropertyResult>();
        foreach (var member in members)
        {
            var property = ResolveProperty(resource.Instance.Definition, member)
                ?? throw new CliException(
                    $"Property \"{member}\" was not found on \"{path}\".", ExitCodes.MemberNotFound);
            if (!resource.TryGetPropertyValue(property.Index, out var value))
                throw new CliException(
                    $"Property \"{property.Name}\" has no attached value.", ExitCodes.GeneralFailure);
            results.Add(new PropertyResult(NormalizePath(resource.ResourceLink ?? path), property.Name, property.Index, value));
        }
        return results;
    }

    public async Task<EpResource> ResolveAsync(
        EsiurSession session, string path, CancellationToken cancellationToken)
    {
        try
        {
            var resource = await EsiurSessionFactory.AwaitReply(
                session.Connection.Get(NormalizePath(path)), cancellationToken);
            return resource as EpResource
                ?? throw new CliException($"Resource \"{path}\" was not found.", ExitCodes.ResourceNotFound);
        }
        catch (CliException) { throw; }
        catch (Exception exception) { throw MapResourceException(path, exception); }
    }

    static PropertyDef? ResolveProperty(TypeDef definition, string member)
    {
        if (byte.TryParse(member, out var index)) return definition.GetPropertyDefByIndex(index);
        return definition.GetPropertyDefByName(member);
    }

    static CliException MapResourceException(string path, Exception exception)
    {
        if (exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return new CliException($"Resource \"{path}\" was not found.", ExitCodes.ResourceNotFound, exception);
        if (exception.Message.Contains("allow", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return new CliException($"Access to resource \"{path}\" was denied.", ExitCodes.AccessDenied, exception);
        return new CliException($"Could not inspect resource \"{path}\": {exception.Message}", ExitCodes.GeneralFailure, exception);
    }

    public static string NormalizePath(string path) => path.Trim().Trim('/');

    static IReadOnlyDictionary<string, string> ToDictionary(Esiur.Data.Map<string, string>? map) =>
        map is null ? new Dictionary<string, string>() : map.ToDictionary(x => x.Key, x => x.Value);

    static string FunctionFlags(FunctionDef function) => string.Join(", ", new[]
    {
        function.IsStatic ? "static" : null,
        function.ReadOnly ? "read-only" : null,
        function.Idempotent ? "idempotent" : null,
        function.Cancellable ? "cancellable" : null,
        function.StreamMode != StreamMode.None ? $"stream:{function.StreamMode}" : null,
        function.Pausable ? "pausable" : null,
    }.Where(x => x is not null));
}

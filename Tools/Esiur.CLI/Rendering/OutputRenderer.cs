using System.Collections;
using System.Globalization;
using System.Text.Json;
using Esiur.Resource;

namespace Esiur.CLI.Rendering;

public sealed class OutputRenderer(TextWriter output)
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task RenderAsync(object? value, OutputFormat format, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (format)
        {
            case OutputFormat.Json:
                await output.WriteLineAsync(JsonSerializer.Serialize(Normalize(value), JsonOptions));
                break;
            case OutputFormat.JsonLines:
                if (value is IEnumerable values and not string)
                    foreach (var item in values)
                        await output.WriteLineAsync(JsonSerializer.Serialize(Normalize(item), JsonOptions));
                else
                    await output.WriteLineAsync(JsonSerializer.Serialize(Normalize(value), JsonOptions));
                break;
            case OutputFormat.Raw:
                await RenderRawAsync(value);
                break;
            default:
                await RenderTableAsync(value);
                break;
        }
    }

    async Task RenderRawAsync(object? value)
    {
        if (value is IEnumerable values and not string and not byte[])
        {
            foreach (var item in values)
            {
                var scalar = item?.GetType().GetProperty("Value")?.GetValue(item) ?? item;
                await output.WriteLineAsync(Scalar(scalar));
            }
        }
        else await output.WriteLineAsync(Scalar(value));
    }

    async Task RenderTableAsync(object? value)
    {
        if (value is Esiur.CLI.Client.ResourceDescription description)
        {
            await WriteKeyValuesAsync(new[]
            {
                ("Path", (object?)description.Path), ("Type", description.Type),
                ("Type ID", description.TypeId), ("Session ID", description.SessionId),
                ("Age", description.Age), ("Parent Type ID", description.ParentTypeId),
            });
            await WriteSectionAsync("Properties", description.Properties);
            await WriteSectionAsync("Functions", description.Functions);
            await WriteSectionAsync("Events", description.Events);
            await WriteSectionAsync("Constants", description.Constants);
            if (description.Annotations.Count > 0)
                await WriteKeyValuesAsync(description.Annotations.Select(x => (x.Key, (object?)x.Value)));
            return;
        }

        if (value is IEnumerable enumerable and not string)
        {
            var rows = enumerable.Cast<object?>().ToArray();
            await WriteObjectsAsync(rows);
            return;
        }
        if (value is not null)
        {
            var properties = value.GetType().GetProperties().Where(x => x.CanRead).ToArray();
            if (properties.Length > 0)
            {
                await WriteKeyValuesAsync(properties.Select(x => (Humanize(x.Name), x.GetValue(value))));
                return;
            }
        }
        await output.WriteLineAsync(Scalar(value));
    }

    async Task WriteSectionAsync(string title, IEnumerable values)
    {
        var rows = values.Cast<object?>().ToArray();
        if (rows.Length == 0) return;
        await output.WriteLineAsync();
        await output.WriteLineAsync(title);
        await WriteObjectsAsync(rows);
    }

    async Task WriteObjectsAsync(object?[] rows)
    {
        if (rows.Length == 0) return;
        var first = rows.FirstOrDefault(x => x is not null);
        if (first is null) return;
        var properties = first.GetType().GetProperties()
            .Where(property => property.CanRead
                && !typeof(IEnumerable<KeyValuePair<string, string>>).IsAssignableFrom(property.PropertyType))
            .ToArray();
        if (properties.Length == 0)
        {
            foreach (var row in rows) await output.WriteLineAsync(Scalar(row));
            return;
        }

        var cells = rows.Select(row => properties.Select(property =>
            Scalar(row is null ? null : property.GetValue(row))).ToArray()).ToArray();
        var widths = properties.Select((property, column) => Math.Min(80,
            cells.Select(row => row[column].Length).Append(Humanize(property.Name).Length).Max())).ToArray();
        await output.WriteLineAsync(string.Join("  ", properties.Select((property, i) =>
            Humanize(property.Name).PadRight(widths[i]))));
        await output.WriteLineAsync(string.Join("  ", widths.Select(width => new string('-', width))));
        foreach (var row in cells)
            await output.WriteLineAsync(string.Join("  ", row.Select((cell, i) =>
                Truncate(cell, widths[i]).PadRight(widths[i]))));
    }

    async Task WriteKeyValuesAsync(IEnumerable<(string Key, object? Value)> values)
    {
        var rows = values.Where(x => x.Value is not null).ToArray();
        var width = rows.Length == 0 ? 0 : rows.Max(x => x.Key.Length);
        foreach (var row in rows)
            await output.WriteLineAsync($"{row.Key.PadRight(width)}  {Scalar(row.Value)}");
    }

    public static object? Normalize(object? value)
    {
        if (value is null || value is string || value.GetType().IsPrimitive
            || value is decimal || value is DateTime || value is DateTimeOffset || value is Guid)
            return value;
        if (value is Enum) return value.ToString();
        if (value is byte[] bytes) return Convert.ToBase64String(bytes);
        if (value is IResource resource) return resource.Instance?.Link;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object?>();
            foreach (DictionaryEntry entry in dictionary)
                result[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] = Normalize(entry.Value);
            return result;
        }
        if (value is IEnumerable enumerable)
            return enumerable.Cast<object?>().Select(Normalize).ToArray();
        var properties = value.GetType().GetProperties().Where(property => property.CanRead);
        return properties.ToDictionary(property => property.Name, property => Normalize(property.GetValue(value)));
    }

    static string Scalar(object? value) => Normalize(value) switch
    {
        null => "null",
        bool boolean => boolean ? "true" : "false",
        DateTime date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        string text => text,
        object normalized when normalized.GetType().IsPrimitive || normalized is decimal
            => Convert.ToString(normalized, CultureInfo.InvariantCulture) ?? string.Empty,
        object normalized => JsonSerializer.Serialize(normalized, JsonOptions),
    };

    static string Humanize(string name) => string.Concat(name.Select((character, index) =>
        index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
    static string Truncate(string value, int width) => value.Length <= width ? value : value[..Math.Max(0, width - 1)] + "…";
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Esiur.AspNetCore;

/// <summary>Maps Esiur WebSocket endpoints into ASP.NET Core endpoint routing.</summary>
public static class EsiurEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an EP WebSocket endpoint. The returned builder supports ASP.NET Core endpoint
    /// conventions such as authorization and rate limiting.
    /// </summary>
    public static IEndpointConventionBuilder MapEsiur(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/esiur")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("An endpoint route pattern is required.", nameof(pattern));

        return endpoints.Map(
                pattern,
                context => context.RequestServices
                    .GetRequiredService<EsiurWebSocketEndpoint>()
                    .HandleAsync(context))
            .WithDisplayName("Esiur EP WebSocket");
    }
}

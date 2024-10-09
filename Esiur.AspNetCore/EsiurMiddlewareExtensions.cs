using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace Esiur.AspNetCore
{
    public static class EsiurMiddlewareExtensions
    {
        public static IApplicationBuilder UseEsiur(this IApplicationBuilder app, EsiurOptions options)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(options);

            return app.UseMiddleware<EsiurMiddleware>(Options.Create(options));
        }

    }
}

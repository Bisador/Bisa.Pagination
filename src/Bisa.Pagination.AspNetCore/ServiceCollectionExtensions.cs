using Bisa.Pagination.Abstractions;
using Bisa.Pagination.Abstractions.Enums;
using Bisa.Pagination.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Bisa.Pagination.AspNetCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Default PaginationOptions and ICursorCodec registration in DI.
    /// Example:
    /// <code>
    /// services.AddBisaPagination(options =>
    /// {
    ///     options.DefaultPageSize = 20;
    ///     options.MaxPageSize = 100;
    ///     options.CursorProtection = CursorProtection.HashSigned;
    ///     options.CursorSigningKey = Convert.FromBase64String(configuration["Pagination:CursorKey"]!);
    ///     options.CursorTimeToLive = TimeSpan.FromHours(6);
    /// });
    /// </code>
    /// </summary>
    public static IServiceCollection AddBisaPagination(
        this IServiceCollection services,
        Action<PaginationOptions>? configure = null)
    {
        var options = new PaginationOptions();
        configure?.Invoke(options);

        if (options is { CursorProtection: CursorProtection.HashSigned, CursorSigningKey.Length: 0 })
        {
            // Best Practice: In Production, be sure to inject the key from the secure configuration;
            // This is just to avoid crashes in the development environment.
            options.CursorSigningKey = PaginationOptionsFactory.GenerateRandomKey();
        }

        services.AddSingleton(options);
        services.AddSingleton<ICursorCodec, DefaultCursorCodec>();
        return services;
    }
}

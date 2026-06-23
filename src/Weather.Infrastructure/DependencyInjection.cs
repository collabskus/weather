using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Caching;
using Weather.Infrastructure.Nws;
using Weather.Infrastructure.Services;

namespace Weather.Infrastructure;

/// <summary>
/// The single composition root for the infrastructure layer. One call wires up
/// configuration, the SQLite caches, the resilient NWS HTTP client, the
/// orchestrating weather service and the background neighbourhood warmer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWeatherInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<NwsClientOptions>(configuration.GetSection(NwsClientOptions.SectionName));
        services.Configure<SqliteCacheOptions>(configuration.GetSection(SqliteCacheOptions.SectionName));

        // Clock — the BCL abstraction. Tests substitute a controllable one.
        services.TryAddSingleton(TimeProvider.System);

        // Custom metrics + traces (the meter/source names are registered with
        // OpenTelemetry in the API host).
        services.TryAddSingleton<WeatherTelemetry>();

        // SQLite caches are stateless and thread-safe → singletons.
        services.TryAddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.TryAddSingleton<IForecastCache, SqliteForecastCache>();
        services.TryAddSingleton<IPointMetadataCache, SqlitePointMetadataCache>();
        services.TryAddSingleton<ICellExtrasCache, SqliteCellExtrasCache>();
        services.TryAddSingleton<IAlertCache, SqliteAlertCache>();
        services.AddHostedService<DatabaseInitializer>();

        // Single-flight coalescer for forecast fetches. MUST be a singleton:
        // its whole job is to collapse concurrent misses ACROSS requests into
        // one upstream call, which only works if every request shares the same
        // in-flight dictionary. A scoped registration would give each request
        // its own dictionary and reintroduce the stampede.
        services.TryAddSingleton<IRequestCoalescer<GridPoint>, RequestCoalescer<GridPoint>>();

        // Single-flight coalescer for alert fetches, keyed by the rounded
        // coordinate's cache key. Same rationale as the forecast coalescer:
        // when an alert entry is cold/expired and many browsers refresh at
        // once, only the first request calls /alerts/active; the rest await it.
        services.TryAddSingleton<IRequestCoalescer<string>, RequestCoalescer<string>>();

        // Resilient, identified NWS client. The standard resilience handler
        // wraps Polly v8 (retry w/ jitter, circuit breaker, timeout) and emits
        // its own OpenTelemetry signals.
        services.AddHttpClient<INwsApiClient, NwsApiClient>(static (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<NwsClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);

            // The resilience pipeline owns timeouts; disable HttpClient's own.
            client.Timeout = Timeout.InfiniteTimeSpan;

            // NWS rejects requests with no identifying User-Agent.
            if (!client.DefaultRequestHeaders.UserAgent.TryParseAdd(options.UserAgent))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
            }

            client.DefaultRequestHeaders.Accept.ParseAdd("application/geo+json");
        }).AddStandardResilienceHandler();

        // Request-scoped orchestration.
        services.TryAddScoped<IWeatherService, WeatherService>();

        // Background neighbourhood warming with a shared, global rate limit.
        services.TryAddSingleton<INeighborhoodWarmer, NeighborhoodWarmer>();
        services.TryAddSingleton<RateLimiter>(static _ => new TokenBucketRateLimiter(
            new TokenBucketRateLimiterOptions
            {
                TokenLimit = 20,
                TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 64,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }));
        services.AddHostedService<NeighborhoodWarmingBackgroundService>();

        return services;
    }
}

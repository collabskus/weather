using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
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

        // ---------------------------------------------------------------------
        // Single-flight coalescers. Each MUST be a singleton: their whole job is
        // to collapse concurrent misses ACROSS requests into one upstream call,
        // which only works if every request shares the same in-flight
        // dictionary. A scoped registration would give each request its own
        // dictionary and reintroduce the stampede.
        // ---------------------------------------------------------------------

        // Daily forecast fetches, keyed by grid cell.
        services.TryAddSingleton<IRequestCoalescer<GridPoint>, RequestCoalescer<GridPoint>>();

        // Cell "extras" (hourly + observation) fetches, also keyed by grid cell.
        // Registered under a DI key because it is a SECOND GridPoint coalescer
        // with a distinct in-flight set: the daily-forecast coalescer bounds
        // /forecast, this one bounds /forecast/hourly + /stations +
        // /observations/latest, and they must not share a flight (their work
        // and lifetimes differ).
        services.TryAddKeyedSingleton<IRequestCoalescer<GridPoint>, RequestCoalescer<GridPoint>>(
            WeatherService.ExtrasCoalescerKey);

        // Point-metadata resolution, keyed by the rounded coordinate. Collapses
        // concurrent /points misses for the same coordinate into one call.
        services.TryAddSingleton<IRequestCoalescer<GeoCoordinate>, RequestCoalescer<GeoCoordinate>>();

        // Alert fetches, keyed by the rounded coordinate's cache key (a string).
        // When an alert entry is cold/expired and many browsers refresh at once,
        // only the first request calls /alerts/active; the rest await it.
        services.TryAddSingleton<IRequestCoalescer<string>, RequestCoalescer<string>>();

        // ---------------------------------------------------------------------
        // Resilient, identified NWS client.
        //
        // The resilience pipeline is added ONCE, globally, by the service
        // defaults (ServiceDefaults.AddServiceDefaults → ConfigureHttpClientDefaults
        // → AddStandardResilienceHandler). We deliberately do NOT add a second
        // AddStandardResilienceHandler() here: stacking two standard handlers on
        // the same client nests two retry+timeout pipelines, so a single slow
        // upstream call is retried multiplicatively and can hang for tens of
        // seconds. We instead TUNE that single global handler for this client by
        // name (the typed-client name is its options name), wiring in the
        // NwsClientOptions timeouts/retries that were previously inert.
        // ---------------------------------------------------------------------
        var nwsBuilder = services.AddHttpClient<INwsApiClient, NwsApiClient>(static (serviceProvider, client) =>
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
        });

        // Override the standard resilience options for THIS client only. The
        // standard handler names its options after the HTTP client, so binding
        // HttpStandardResilienceOptions under nwsBuilder.Name layers on top of
        // the defaults the global handler registered for this client.
        services.AddOptions<HttpStandardResilienceOptions>(nwsBuilder.Name)
            .Configure<IOptions<NwsClientOptions>>(static (resilience, nwsOptionsAccessor) =>
            {
                var nws = nwsOptionsAccessor.Value;

                var attempt = nws.RequestTimeout;
                var total = nws.TotalRequestTimeout;

                // The validator requires the total to exceed a single attempt;
                // guard against a misconfiguration that would fail validation.
                if (total <= attempt)
                {
                    total = attempt + attempt;
                }

                resilience.AttemptTimeout.Timeout = attempt;
                resilience.TotalRequestTimeout.Timeout = total;

                // The validator also requires the circuit-breaker sampling
                // window to be at least double the attempt timeout. Use the
                // larger of (2 × attempt) and the total so it always satisfies
                // both that rule and common sense.
                var samplingTicks = Math.Max(attempt.Ticks * 2, total.Ticks);
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromTicks(samplingTicks);

                resilience.Retry.MaxRetryAttempts = nws.MaxRetryAttempts;
                resilience.Retry.Delay = nws.RetryBaseDelay;
                resilience.Retry.BackoffType = DelayBackoffType.Exponential;
                resilience.Retry.UseJitter = true;
            });

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

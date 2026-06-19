# Weather.Infrastructure

The implementations behind `Weather.Core`'s abstractions: the NWS client, the
SQLite caches, the orchestration service, and the background warmer.

## Contents

- **Nws/**
  - `NwsApiClient` — a resilient, typed `HttpClient` wrapper implementing all of
    `INwsApiClient`: point metadata, daily forecast (with `ETag`/`If-None-Match`
    conditional GETs and a parsed polygon **centroid**), hourly forecast, latest
    observation (resolved via the cell's nearest station), and active alerts.
    Failures map to typed results or empty collections rather than exceptions, and
    every call is timed and tagged through `WeatherTelemetry`.
  - `NwsClientOptions` (bound from the `Nws` config section — base URL, the
    **required** descriptive `User-Agent`, TTL fallback/clamp, timeout),
    `NwsJsonModels` (the `application/geo+json` DTOs), and `NwsUnits` (SI→US
    conversions: °C→°F, km/h→mph, Pa→inHg, m→mi, degrees→compass).
- **Caching/**
  - `SqliteConnectionFactory` / `SqliteCacheOptions`, `DatabaseInitializer`
    (creates the tables/indexes on startup), `TimestampText` (ISO-8601 round-trip
    persistence), and the three caches: `SqlitePointMetadataCache`,
    `SqliteForecastCache`, and `SqliteCellExtrasCache`. Access is via **Dapper**.
- **Services/**
  - `WeatherService` — the cache-aside orchestrator and the only type that knows
    the freshness rules. It serves the single forecast, the neighbourhood, and the
    full **area** (primary + ring assembled cache-aside with bounded concurrency,
    ordered by distance).
  - `NeighborhoodWarmer` (bounded `Channel<GridPoint>`) and
    `NeighborhoodWarmingBackgroundService` (token-bucket rate limiter, in-flight
    de-dup, skip-if-fresh, per-scope DI).
- **Serialization/** — `WeatherJson`, the shared `System.Text.Json` options.
- `DependencyInjection.AddWeatherInfrastructure` wires it all up (including
  `TryAddSingleton<ICellExtrasCache, SqliteCellExtrasCache>`).

## Notes

The NWS `User-Agent` is mandatory — set your own contact in configuration.
Tests run against a stub `HttpMessageHandler` and a real temporary SQLite file,
so parsing, conditional GETs, failure mapping, and the cache-aside policy are all
exercised without the network — see `tests/Weather.Infrastructure.Tests`.

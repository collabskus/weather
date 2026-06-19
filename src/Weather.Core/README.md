# Weather.Core

The domain core. It holds the models, abstractions, and telemetry primitives the
rest of the solution is built on, and **depends on nothing else** — no ASP.NET,
no SQLite, no NWS specifics. Everything here is plain, testable C#.

## Contents

- **Models/** — immutable domain records:
  - `GeoCoordinate` (validated lat/lon, with `DistanceMetersTo` haversine),
    `GridPoint`, `PointMetadata`.
  - `Forecast`, `ForecastPeriod`, `ForecastFetchResult` (the success / not-modified
    / unavailable / not-found outcome of a fetch, with an optional cell centre).
  - `Observation` and `WeatherAlert` (US-unit observation fields and the
    point-based alert shape).
  - `CellExtras` (hourly + observation + centre for one cell), `CellWeather` (one
    fully-assembled cell), `AreaForecast` (primary + distance-ordered neighbours +
    alerts), `NeighborhoodForecast`.
  - `CachedForecast`, `CachedPointMetadata` (cached envelopes with retrieval/expiry
    instants), and `GridNeighborhood` (pure geometry for the surrounding ring).
- **Abstractions/** — the seams the infrastructure implements: `INwsApiClient`,
  `IPointMetadataCache`, `IForecastCache`, `ICellExtrasCache`, `IWeatherService`,
  `INeighborhoodWarmer`.
- **Telemetry/** — `WeatherTelemetry`, which owns the `Weather.Cache` meter and
  the `Weather.Nws` activity source and exposes typed `Record…` methods for cache
  hits/misses, NWS request latency/status, throttling, and errors.

## Why it's isolated

Keeping the domain free of framework and vendor types means the cache-aside
policy, the neighbourhood geometry, and the fetch-outcome modelling can be unit
tested without a web host, a database, or the network — see
`tests/Weather.Core.Tests`.

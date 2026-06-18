# Weather.Core

The domain core. Pure models and abstractions with **no I/O and no framework
dependencies** — everything here could be unit-tested without a network, a
database, or a web host. Implementations live in
[`Weather.Infrastructure`](../Weather.Infrastructure/README.md).

## What's in here

### Models (`Models/`)

- **`GeoCoordinate`** — a validated `readonly record struct` (latitude/longitude,
  rejecting out-of-range and `NaN`). Knows how to round itself to four decimals
  (`Rounded()`), which is the basis of the cache key, and how to format itself
  for the NWS API with the invariant culture (`ToApiString()`), so a comma
  decimal separator can never leak in under a non-US culture.
- **`GridPoint`** — an NWS forecast grid cell `(GridId, GridX, GridY)`.
- **`GridNeighborhood`** — the geometry of the surrounding cells
  (`Surrounding(origin, radius = 1)` → the ring minus the origin, dropping
  negative indices at the grid edge). This is what the background warmer fans
  out over.
- **`Forecast`** / **`ForecastPeriod`** — the domain forecast and its periods.
- **`PointMetadata`** — the resolved coordinate→grid mapping plus location
  details (city/state/time-zone/radar).
- **`NeighborhoodForecast`** — a primary forecast plus already-warm neighbours.
- **`CachedForecast`** / **`CachedPointMetadata`** — cache envelopes carrying the
  payload, ETag (forecasts), and retrieved/expiry timestamps, with an
  `IsFresh(now)` test.
- **`ForecastFetchResult`** — the outcome of an NWS fetch as a small state
  machine: `Success`, `NotModified`, `NotFound`, `Unavailable`. This is how the
  HTTP layer tells the orchestrator what happened without leaking HTTP details.

### Abstractions (`Abstractions/`)

- **`IWeatherService`** — the orchestration surface used by the API.
- **`INwsApiClient`** — the NWS HTTP client contract.
- **`IForecastCache`** / **`IPointMetadataCache`** — the two cache tiers.
- **`INeighborhoodWarmer`** — the background warming queue.

### Telemetry (`Telemetry/`)

- **`WeatherTelemetry`** — owns the app-specific `Meter` ("Weather.Cache") and
  `ActivitySource` ("Weather.Nws") and the methods that record cache hit/miss,
  NWS request duration, throttling, errors, and neighbour-warming. The names are
  registered with OpenTelemetry by the API host.

## Design notes

- Time comes from the BCL **`TimeProvider`** (passed in, never `DateTime.UtcNow`
  directly), so any freshness logic built on these models is deterministic under
  test.
- The cache key strategy lives on `GeoCoordinate` itself (`Rounded().ToCacheKey()`),
  keeping "physically close users share a row" a property of the domain rather
  than of the storage layer.

## Tested by

[`Weather.Core.Tests`](../../tests/Weather.Core.Tests/) — validation and
boundary behaviour, neighbourhood geometry, fetch-result factories, and the
telemetry instruments (verified with a `MeterListener`).

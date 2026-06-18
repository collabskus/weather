# Weather.Api

The HTTP edge: a thin **ASP.NET Core Minimal API**. It validates incoming
coordinates and delegates everything else to
[`IWeatherService`](../Weather.Core/README.md). Caching, resilience, and
background warming are implementation details it knows nothing about.

## Endpoints

Both live under `/api/forecast` and take `latitude` and `longitude` query
parameters.

### `GET /api/forecast?latitude={lat}&longitude={lon}`

The forecast for the grid cell containing the coordinate.

- `200 OK` — a `ForecastResponse` (grid id/x/y, generated/updated timestamps,
  and the list of periods).
- `400 Bad Request` — coordinate out of range, as a `ProblemDetails`.
- `404 Not Found` — no NWS coverage for that coordinate.

### `GET /api/forecast/neighborhood?latitude={lat}&longitude={lon}`

The primary cell plus any **already-warm** adjacent cells. It returns
immediately and never blocks to fetch neighbours — they appear only if the
background warmer has already fetched them.

- `200 OK` — a `NeighborhoodResponse` (the query coordinate, the primary
  forecast, and zero-or-more neighbour forecasts).
- `400` / `404` as above.

## Request/response shape

The wire contract is decoupled from the domain model. `ForecastResponse`,
`ForecastPeriodResponse`, and `NeighborhoodResponse` (in `Contracts/`) are plain
records with `FromDomain(...)` mappers, so the public API and the internal
`Forecast` can evolve independently. Coordinate validation uses
`GeoCoordinate.IsValid` at the edge; invalid input returns RFC-7807
`ProblemDetails` via `TypedResults.Problem`.

## How it's wired (`Program.cs`)

```csharp
builder.AddServiceDefaults();                 // Aspire: OTel, health, discovery, resilient HTTP
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddWeatherInfrastructure(builder.Configuration);

// Register the app's own meter/trace source with OpenTelemetry. Done here (not
// in ServiceDefaults) so ServiceDefaults stays app-agnostic.
builder.Services.ConfigureOpenTelemetryMeterProvider(m => m.AddMeter(WeatherTelemetry.MeterName));
builder.Services.ConfigureOpenTelemetryTracerProvider(t => t.AddSource(WeatherTelemetry.ActivitySourceName));
```

The pipeline uses `UseExceptionHandler` + `UseStatusCodePages` (so failures come
back as `ProblemDetails`), maps the default health endpoints, exposes an OpenAPI
document at `/openapi/v1.json` in Development, and maps the forecast endpoints.

`Program` ends with `public partial class Program;` so the test project can
drive it with `WebApplicationFactory<Program>`.

## Observability

Because of `AddServiceDefaults()` plus the meter/source registration above, the
API emits ASP.NET Core, `HttpClient`, and runtime metrics **and** the app's cache
hit/miss and NWS latency/throttling signals over OTLP — visible in the Aspire
dashboard when run under the [AppHost](../Weather.AppHost/README.md). See
[`docs/ARCHITECTURE.md`](../../docs/ARCHITECTURE.md#decision-3--observability).

## Tested by

[`Weather.Api.Tests`](../../tests/Weather.Api.Tests/) — the endpoints end-to-end
via `WebApplicationFactory<Program>`, with the NWS client replaced by a fake and
the cache pointed at a throwaway temp database, asserting the `200` / `400` /
`404` behaviours and the response shape.

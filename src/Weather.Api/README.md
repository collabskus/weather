# Weather.Api

The backend: an **ASP.NET Core Minimal API** that exposes the cached NWS data.
It owns no weather logic itself — it calls `IWeatherService` (from
`Weather.Infrastructure`) and maps the domain results to JSON contracts. Caching,
freshness, and the NWS calls all live below it.

## Endpoints

Mapped in `Endpoints/WeatherEndpoints.cs`:

- `GET /api/forecast?lat={lat}&lon={lon}` — the daily forecast for the cell
  containing the coordinate. `200` with the forecast, or `404` if the point is
  outside NWS coverage.
- `GET /api/forecast/neighborhood?lat={lat}&lon={lon}` — the primary cell plus
  the already-fresh surrounding cells.
- `GET /api/forecast/area?lat={lat}&lon={lon}&radius={1..3}` — the full area
  view: the primary cell and **every** neighbouring cell (each with daily +
  hourly + observation), distance-ordered, plus active alerts. Invalid radius →
  `400`; uncovered point → `404`.

Coordinates are validated; bad input returns `400` via Problem Details.

## Contracts

`Contracts/` holds the response DTOs (`ForecastResponse`, `NeighborhoodResponse`,
`AreaResponse` with `CellResponse` / `ObservationResponse` / `AlertResponse`, all
reusing `ForecastPeriodResponse`). They are serialized as camelCase JSON.

## Wiring

`Program.cs` calls `AddServiceDefaults()` (OpenTelemetry, health, discovery,
resilience), `AddWeatherInfrastructure(...)` (NWS client, caches, services,
warmer), registers the app's meter/trace source, and adds Problem Details and the
OpenAPI document (served at `/openapi/v1.json` in Development). The class is
`public partial` so `Weather.Api.Tests` can drive it with
`WebApplicationFactory<Program>`.

## Configuration

`appsettings.json` carries the `Nws` options (base URL, **required** `User-Agent`,
TTLs, timeout), the SQLite `Cache:ConnectionString`, and the `Uptrace` block
(`Enabled` / `Dsn`). See `tests/Weather.Api.Tests` for the end-to-end coverage.

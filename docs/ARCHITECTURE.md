# Architecture

This document records the three decisions that shape the system and how they are
implemented. It is written to match the code in this repository; the per-project
READMEs go deeper on each layer.

## Shape

```
Browser ──geolocation──▶ Weather.Web (Blazor Server)
                              │  typed HttpClient
                              ▼
                         Weather.Api (Minimal API)
                              │  cache-aside
                              ▼
        ┌───────────────── Weather.Infrastructure ─────────────────┐
        │  NwsApiClient (resilient HttpClient)                      │
        │  SQLite caches: point-metadata · forecast · cell-extras   │
        │  WeatherService (orchestration) · NeighborhoodWarmer      │
        └───────────────────────────────────────────────────────────┘
                              │
                              ▼
                    api.weather.gov (NWS)
```

`Weather.Core` holds the domain models, abstractions, and telemetry primitives
and depends on nothing else. `Weather.ServiceDefaults` supplies the Aspire
defaults (OpenTelemetry, health, discovery, resilience) and is intentionally
app-agnostic. `Weather.AppHost` orchestrates the API and the dashboard for local
development.

## Decision 1 — Caching: metadata vs. forecast (and extras)

NWS splits the work in two: `/points/{lat},{lon}` maps a coordinate to a grid
cell (`gridId`, `gridX`, `gridY`) and is effectively immutable, while
`/gridpoints/{office}/{x},{y}/forecast` returns data that changes through the
day. The cache mirrors that split, in three SQLite tiers:

- **Point metadata** — long-lived (30 days by default). The key is the
  coordinate **rounded to 4 decimal places** (~11 m), so nearby users share one
  row and the `/points` call is made once per neighbourhood rather than per user.
- **Forecast** — short-lived. The TTL honours the NWS `Cache-Control: max-age`
  response, clamped to a configurable ceiling (6 h) so an odd upstream value
  can't pin stale data. Refreshes are **conditional**: the stored `ETag` is sent
  as `If-None-Match`, and a `304 Not Modified` renews the expiry without
  transferring or re-parsing a body. If NWS is unreachable, the **last good
  forecast is served stale** rather than surfacing an error.
- **Cell extras** — a separate short-lived tier holding the per-cell **hourly
  forecast** and **latest observation** as a JSON blob keyed by grid cell. It is
  kept apart from the daily-forecast tier so the original forecast path is
  unchanged and the heavier extras are only fetched when the area view needs them.

`WeatherService` is the only type that knows these freshness rules; everything
above it simply asks for data. Cache hits and misses are recorded per tier as
metrics (see Decision 3).

## Decision 2 — Neighbourhood strategy

A user near a cell boundary may be closer to an adjacent cell's centre than to
their own, so the dashboard shows the whole neighbourhood. Two mechanisms keep
that cheap:

- **Background warming.** When a user's cell is resolved, the surrounding ring is
  enqueued on a bounded `Channel<GridPoint>` and warmed by a rate-limited
  `BackgroundService` (a token-bucket limiter, in-flight de-duplication, and a
  skip-if-already-fresh check). The user's own request is never blocked by this.
- **Area assembly.** `GetAreaForecastAsync` fetches the primary cell plus every
  cell in the ring **cache-aside**, with **bounded concurrency**, then orders the
  neighbours by true distance from the user. Distance uses each cell's polygon
  **centroid** (parsed from the forecast geometry) and the haversine formula
  (`GeoCoordinate.DistanceMetersTo`), falling back to grid-index distance when a
  centroid is unknown so ordering is always stable. The request **radius is
  capped** so a cold area cannot stampede NWS.

Active **alerts** are point-based (`/alerts/active?point=lat,lon`) and assembled
alongside the cells.

### Cold-start cost

The first view of an uncached area can be several upstream calls (a cell plus its
ring, each fetching daily + hourly + observation). It is bounded by cache-aside
(paid once per TTL), the background warmer, capped concurrency, and the capped
radius, so steady state is inexpensive — the cache-hit ratio trending toward 1.0
is the signal it is working.

## Decision 3 — Observability

`Weather.ServiceDefaults` configures OpenTelemetry for every service: ASP.NET
Core, `HttpClient`, and runtime instrumentation, plus the application's own
signals from a custom meter (`Weather.Cache`) and activity source (`Weather.Nws`):

- **cache hit/miss** counters per tier (`metadata`, `forecast`, `extras`),
- **NWS request** latency and status, tagged by endpoint and whether the call was
  conditional, and
- **NWS throttling and errors** counted separately, so rate-limiting shows up as
  a metric rather than as latency.

Export is vendor-neutral **OTLP** — no backend SDK is referenced — and resolves
in priority order: an explicit `OTEL_EXPORTER_OTLP_ENDPOINT` (injected by Aspire
locally, or pointed at the OpenTelemetry Collector in containers) wins; otherwise
the app ships straight to **Uptrace** via a DSN unless `Uptrace:Enabled=false`;
otherwise nothing is exported (the integration tests use this). Because every
step is plain OTLP, switching backends is configuration, not code. The container
stack runs a collector that fans every signal out to both the Aspire dashboard
and Uptrace — see [`OBSERVABILITY.md`](OBSERVABILITY.md) and
[`CONTAINERS.md`](CONTAINERS.md).

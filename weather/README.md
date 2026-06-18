# Weather

A weather dashboard built on the U.S. National Weather Service (NWS) API,
written in **.NET 10 / C# 14**. It pairs a **Blazor Server** front end with an
**ASP.NET Core Minimal API** backend, caches forecasts in **SQLite** (via
**Dapper**), is wired for observability with **OpenTelemetry**, and is
orchestrated with **.NET Aspire**.

Repository: <https://github.com/collabskus/weather.git>

> [!IMPORTANT]
> The package and GitHub Action versions committed here are a **baseline
> snapshot**, centralised so they can be updated in one place. The bundled
> [Dependabot config](.github/dependabot.yml) opens weekly PRs to keep them
> current, and the [CI workflows](.github/workflows) are the source of truth for
> whether the solution builds and the tests pass.

## What it does

1. The browser is asked for the user's location.
2. That coordinate is resolved to an NWS **forecast grid cell** and the forecast
   for that cell is shown — a "sky-state" hero that paints the sky implied by the
   current conditions, plus cards for the upcoming periods.
3. If the user declines to share their location, or the browser can't provide it,
   the dashboard falls back to manual latitude/longitude entry (with a one-click
   sample location). It works the same either way.

Forecasts are cached for a short window and reused, so the dashboard stays quick
and the weather service isn't polled more than necessary — all invisible to the
user, who always sees current data.

## Design at a glance

Three decisions shape the system; the full reasoning is in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

- **Caching** — two SQLite tiers: long-lived coordinate→grid metadata (30 days,
  keyed by a 4-dp coordinate so nearby users share a row) and short-lived grid
  forecasts (honouring NWS `Cache-Control`, clamped to 6 h). Conditional GETs
  (`If-None-Match` → `304`) refresh expiry cheaply; if NWS is unreachable, the
  last good forecast is served rather than an error.
- **Neighbourhood warming** — the user's own cell is fetched synchronously; the
  surrounding 3×3 ring is warmed in the background via a bounded channel and a
  rate-limited `BackgroundService`, so panning feels instant without ever
  slowing the user's request.
- **Observability** — Aspire `ServiceDefaults` set up OpenTelemetry, health
  checks, and service discovery; a custom meter/trace source records cache
  hit-rate and NWS latency/throttling. It all flows into the Aspire dashboard.

## Geolocation fallback behaviour

Browser geolocation can fail in several ways, and the app treats every one as a
normal path rather than an error:

| Situation | What the user sees |
| --- | --- |
| Location shared | The forecast for their cell |
| Permission denied | "Location is off" + manual entry + sample location |
| Position unavailable / timeout / unsupported | "Couldn't pin down where you are" + manual entry + sample |
| Coordinate outside NWS coverage | "No coverage for that spot" + manual entry + sample |
| API/network error | "The forecast didn't load" + retry + sample |

The browser bridge (`wwwroot/js/geolocation.js`) **never rejects** — it always
resolves a result object — so "the user said no" never lands on an exception
path. Details in [`src/Weather.Web/README.md`](src/Weather.Web/README.md).

## Prerequisites

- **.NET 10 SDK** (the repo pins the `10.0.x` feature band in
  [`global.json`](global.json)).
- For the Aspire run experience, the **.NET Aspire** tooling/workload as
  described in the [Aspire docs](https://learn.microsoft.com/dotnet/aspire/).
- No database to install — SQLite is file-based and created on first run.

## Run it

### With .NET Aspire (recommended)

One command starts the API and the dashboard together and opens the Aspire
dashboard for logs, traces, and metrics:

```bash
dotnet run --project src/Weather.AppHost
```

### Standalone (without Aspire)

Run the two services in separate terminals:

```bash
# Terminal 1 — the API
dotnet run --project src/Weather.Api

# Terminal 2 — the dashboard, pointed at the API
dotnet run --project src/Weather.Web
```

When not running under Aspire, tell the web app where the API is. Either set it
in configuration:

```jsonc
// src/Weather.Web/appsettings.Development.json
{
  "WeatherApi": { "BaseUrl": "https://localhost:7001" }
}
```

or via the service-discovery environment variable:

```bash
services__api__https__0=https://localhost:7001 dotnet run --project src/Weather.Web
```

## Build and test

```bash
# Restore + build the whole solution
dotnet restore weather.slnx
dotnet build weather.slnx --configuration Release

# Run all tests (TUnit + bUnit + Shouldly)
dotnet test weather.slnx --configuration Release

# Verify formatting (as CI does)
dotnet format weather.slnx --verify-no-changes
```

The solution uses **`weather.slnx`** (the XML solution format), **Central
Package Management** ([`Directory.Packages.props`](Directory.Packages.props)),
and shared build settings ([`Directory.Build.props`](Directory.Build.props)).

> [!NOTE]
> Warnings are **not** treated as errors by default, so a fresh clone builds on
> the first try even if a newer analyzer surfaces a new suggestion. To tighten
> this up once your tree is green, set `TreatWarningsAsErrors` to `true` in
> `Directory.Build.props` or build with `-warnaserror`.

## Tests

Extensive coverage across four projects, using **TUnit** (runner), **bUnit**
(Blazor component tests), **Shouldly** (assertions), and **NSubstitute** (fakes):

- **Weather.Core.Tests** — coordinate validation/rounding, grid-neighbourhood
  geometry, fetch-result factories, telemetry instruments.
- **Weather.Infrastructure.Tests** — the NWS client (parsing, conditional GET,
  failure mapping) against a stub handler; the SQLite caches against a real
  temp database; the full `WeatherService` cache-aside policy; the background
  warmer.
- **Weather.Api.Tests** — the endpoints end-to-end via `WebApplicationFactory`,
  with a fake NWS source and a real SQLite cache (200 / 400 / 404).
- **Weather.Web.Tests** — the dashboard's state machine and every geolocation
  fallback rendered with bUnit, plus the sky-state palette logic.

## Project layout

```
weather/
├─ src/
│  ├─ Weather.Core/             # domain models, abstractions, telemetry
│  ├─ Weather.Infrastructure/   # NWS client, SQLite caches, services, warmer
│  ├─ Weather.ServiceDefaults/  # Aspire: OTel, health, discovery, resilience
│  ├─ Weather.Api/              # ASP.NET Core Minimal API
│  ├─ Weather.Web/              # Blazor Server dashboard
│  └─ Weather.AppHost/          # .NET Aspire orchestrator
├─ tests/
│  ├─ Weather.Core.Tests/
│  ├─ Weather.Infrastructure.Tests/
│  ├─ Weather.Api.Tests/
│  └─ Weather.Web.Tests/
├─ docs/ARCHITECTURE.md
├─ .github/                     # CI workflows + Dependabot
├─ Directory.Build.props
├─ Directory.Packages.props
├─ global.json
└─ weather.slnx
```

Each project has its own README with the detail for that layer.

## Observability

Both services emit OpenTelemetry over OTLP. Under the AppHost, signals appear in
the Aspire dashboard automatically. Beyond the standard ASP.NET Core / HttpClient
/ runtime instrumentation, the app records cache hit/miss ratios and the latency,
throttling, and errors of every NWS call. See
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md#decision-3--observability) and
[`src/Weather.ServiceDefaults/README.md`](src/Weather.ServiceDefaults/README.md).

## A note on the NWS API

The NWS API **requires a descriptive `User-Agent`** that identifies your app and
a contact; requests without one are rejected. The default is set in
`NwsClientOptions` and should be overridden with your own details in
configuration:

```jsonc
// appsettings.json
{
  "Nws": { "UserAgent": "your-app/1.0 (you@example.com)" }
}
```

See the [NWS API documentation](https://www.weather.gov/documentation/services-web-api).

## CI

The workflows under [`.github/workflows`](.github/workflows) cover build, test,
`dotnet format` verification, CodeQL security analysis, dependency review, and
Markdown linting. Action versions are kept current by Dependabot.

## Licence

[MIT](LICENSE).

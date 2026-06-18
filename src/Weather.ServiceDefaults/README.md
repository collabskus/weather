# Weather.ServiceDefaults

The shared **.NET Aspire service defaults**. Every executable service in the
solution (the API and the Blazor app) calls `AddServiceDefaults()` to get
consistent observability, health, service discovery, and resilient HTTP without
repeating the wiring. This project is deliberately **app-agnostic** — it knows
nothing about forecasts or caches.

## What `AddServiceDefaults()` configures

- **OpenTelemetry** (`ConfigureOpenTelemetry`):
  - **Logging** with formatted messages and scopes included.
  - **Metrics**: ASP.NET Core, `HttpClient`, and .NET runtime instrumentation.
  - **Tracing**: the application's own activity source plus ASP.NET Core and
    `HttpClient` instrumentation.
  - **OTLP export**: enabled automatically when `OTEL_EXPORTER_OTLP_ENDPOINT` is
    set (which Aspire does for you when running under the AppHost).
- **Health checks** (`AddDefaultHealthChecks`): a default "self" liveness check.
- **Service discovery**: logical service names (e.g. `https+http://api`) resolve
  to real addresses.
- **Resilient HttpClient defaults** (`ConfigureHttpClientDefaults`): every
  outbound `HttpClient` gets the standard resilience handler (retry with jitter,
  circuit breaker, timeout) and service-discovery resolution by default.

## What `MapDefaultEndpoints()` adds

Health endpoints, intended for non-production by default:

- `GET /health` — readiness: all health checks must pass.
- `GET /alive` — liveness: only checks tagged `live`.

## Why app-specific telemetry lives elsewhere

The app's own meter (`Weather.Cache`) and trace source (`Weather.Nws`) are
defined in [`Weather.Core`](../Weather.Core/README.md) and **registered with
OpenTelemetry in the API host** (`Weather.Api/Program.cs`), not here. Keeping
this project free of app specifics means it could be lifted into any service
unchanged — which is the whole point of the Aspire "service defaults" pattern.

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();          // OTel, health, discovery, resilient HTTP
// ... app-specific registrations ...
var app = builder.Build();
app.MapDefaultEndpoints();             // /health and /alive
```

Both [`Weather.Api`](../Weather.Api/README.md) and
[`Weather.Web`](../Weather.Web/README.md) do exactly this; the
[AppHost](../Weather.AppHost/README.md) then surfaces the resulting telemetry in
the Aspire dashboard.

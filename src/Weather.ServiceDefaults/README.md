# Weather.ServiceDefaults

The canonical .NET Aspire "service defaults" shared by every service. Calling
`builder.AddServiceDefaults()` wires up OpenTelemetry, health checks, service
discovery, and resilient HTTP in one line. The project is deliberately
**app-agnostic** — it knows nothing about `Weather.Core`, so app-specific meters
and trace sources are registered by the host (`Weather.Api/Program.cs`).

## What it configures

- **OpenTelemetry** — logging (formatted messages + scopes), metrics (ASP.NET
  Core, `HttpClient`, runtime), and tracing (ASP.NET Core, `HttpClient`), plus the
  OTLP exporter (below).
- **Health checks** — a `self` liveness check; `MapDefaultEndpoints` maps
  `/health` (ready) and `/alive` (live) in Development.
- **Service discovery** — logical service names (e.g. `https+http://api`) resolve
  through configuration.
- **Resilient HTTP** — `AddStandardResilienceHandler` on every outbound
  `HttpClient` by default (retries, timeouts, circuit breaker).

## OTLP exporter precedence (vendor-neutral)

There is **no backend SDK** — export is plain OTLP and the destination is chosen
by configuration, in order:

1. **`OTEL_EXPORTER_OTLP_ENDPOINT` is set** → export there. Aspire injects this
   locally; the container compose points it at the OpenTelemetry Collector.
2. **Otherwise → Uptrace.** Unless `Uptrace:Enabled=false`, the standard
   `OTEL_EXPORTER_OTLP_*` variables are derived from an Uptrace **DSN**
   (`Uptrace:Dsn`, env `UPTRACE_DSN`, or a built-in demo default), authenticating
   with the `uptrace-dsn` header and using gzip + delta metric temporality. This
   makes a bare `dotnet run` light up in Uptrace with no setup.
3. **Otherwise → nothing** (the integration tests set `Uptrace:Enabled=false` so
   no network is touched).

Operator-set `OTEL_*` variables are never overridden. See
[`../../docs/OBSERVABILITY.md`](../../docs/OBSERVABILITY.md) for the full picture
and the collector fan-out used in containers.

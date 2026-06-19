# Observability — OpenTelemetry, OTLP, and Uptrace

Both services are instrumented with **OpenTelemetry** and export every signal
(traces, metrics, logs) over **OTLP**. There is deliberately **no vendor SDK**
in the codebase: the backend is chosen entirely by configuration, so the same
build ships to the Aspire dashboard during development, to an OpenTelemetry
Collector in containers, or straight to a hosted backend such as **Uptrace**.

## What is measured

On top of the standard ASP.NET Core, `HttpClient`, and .NET runtime
instrumentation, the application's own `WeatherTelemetry` records:

- **Cache hit/miss** counters per cache (`metadata`, `forecast`, and the
  per-cell `extras` cache) — the cache-hit ratio is the headline health metric.
- **NWS request latency** and status, tagged by endpoint and whether the call
  was a conditional GET — so slow upstreams or excessive calls are visible.
- **NWS throttling and errors** — `429`s and failures are counted separately, so
  rate-limiting shows up as a metric rather than as mysterious latency.

These come from a custom meter (`Weather.Cache`) and activity source
(`Weather.Nws`), registered with OpenTelemetry in `Weather.Api/Program.cs` and
kept out of `ServiceDefaults` so that project stays app-agnostic.

## How export is wired

`Weather.ServiceDefaults` resolves the exporter in priority order:

1. **`OTEL_EXPORTER_OTLP_ENDPOINT` is set** — an explicit endpoint always wins.
   Aspire injects this during local orchestration; the container compose points
   it at the OpenTelemetry Collector. This is the path that fans out to both the
   Aspire dashboard and Uptrace.
2. **Otherwise, ship straight to Uptrace over OTLP** — unless disabled, the app
   derives the standard `OTEL_EXPORTER_OTLP_*` variables from an Uptrace **DSN**
   (config `Uptrace:Dsn`, env `UPTRACE_DSN`, or a built-in demo default),
   authenticating with the `uptrace-dsn` header and using gzip plus delta metric
   temporality. This makes a bare `dotnet run` light up in Uptrace with zero
   configuration.
3. **Otherwise, export nothing** — set `Uptrace:Enabled=false` to opt out. The
   integration tests set exactly this so nothing touches the network.

Because every step emits plain OTLP, pointing at Grafana, Honeycomb, Jaeger, or
any other OTLP-compatible backend is a configuration change, not a code change.

## The collector fan-out (containers)

In the container stack a single **OpenTelemetry Collector** receives OTLP from
both services and exports each signal to two destinations at once:

- the **standalone Aspire dashboard** (`aspire-dashboard:18889`) for live, local
  inspection, and
- **Uptrace** (`api.uptrace.dev:4317`, authenticated with `UPTRACE_DSN`) for a
  hosted view.

The collector config ([`deploy/otelcol-config.yaml`](../deploy/otelcol-config.yaml))
is baked into a small image so there is no host bind mount to relabel under
SELinux. Adding or swapping a destination is a one-line change to that file.

## Seeing your data in Uptrace

Set `UPTRACE_DSN` in `deploy/.env` to your own DSN (from the Uptrace project
settings) before `podman compose up`. With the demo DSN you will export to a
shared public project; with your own you will see your services, traces, and the
cache-hit and NWS-latency metrics under your account.

## A note on cold-start cost

Assembling a brand-new neighbourhood (a cell plus its ring, each with daily +
hourly + observation) can be several upstream calls on the very first view of an
uncached area. Three things keep this in check, and they are worth watching in
the metrics:

- every cell is **cache-aside**, so the cost is paid once per TTL, not per view;
- the **background warmer** pre-fetches the ring's daily forecasts; and
- the area fan-out uses **bounded concurrency** and the request **radius is
  capped**, so a cold area can never stampede NWS.

The cache-hit ratio climbing toward 1.0 after the first views — visible in the
dashboard or Uptrace — is the signal that the caching is doing its job.

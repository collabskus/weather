# Containers — Podman, Compose, and Cloudflare quick tunnels

This document explains the container setup: how to run the full stack, what each
piece is, and the Fedora/SELinux and Aspire nuances that shaped it. It targets
**Podman** on Fedora but works with Docker too.

## TL;DR

```bash
cd deploy
cp .env.example .env          # set UPTRACE_DSN, or keep the demo one
podman compose -f compose.yaml up --build
```

- Dashboard → <http://localhost:8081>
- API → <http://localhost:8080>
- Aspire dashboard → <http://localhost:18888>
- Public URLs → printed in the `tunnel-web`, `tunnel-api`, `tunnel-aspire` logs:

```bash
podman compose -f compose.yaml logs tunnel-web | grep trycloudflare
```

## What runs

| Service | Image / build | Purpose |
| --- | --- | --- |
| `api` | `Containerfile.api` | The Weather Minimal API |
| `web` | `Containerfile.web` | The Blazor Server dashboard |
| `aspire-dashboard` | `mcr.microsoft.com/dotnet/aspire-dashboard` | Standalone traces/metrics/logs UI |
| `otelcol` | `deploy/Containerfile.otelcol` | OpenTelemetry Collector, fans out to Aspire + Uptrace |
| `tunnel-web/-api/-aspire` | `cloudflare/cloudflared` | Public HTTPS URL for each UI |

## The images

`Containerfile.api` and `Containerfile.web` are multi-stage:

1. **build** — `mcr.microsoft.com/dotnet/sdk:10.0` restores and publishes a
   Release build. They are built from the **repository root** so the Central
   Package Management files (`Directory.Packages.props`, `Directory.Build.props`,
   `nuget.config`, `global.json`) and every referenced project are in context.
2. **final** — `mcr.microsoft.com/dotnet/aspnet:10.0` runs the published app as
   the **non-root** `app` user (UID 1654, which ships in the .NET images), listening
   on plain HTTP `:8080`. TLS is terminated upstream by the Cloudflare tunnel.

Build them directly if you like:

```bash
podman build -f Containerfile.api -t weather-api .
podman build -f Containerfile.web -t weather-web .
```

## SELinux / rootless notes (Fedora)

The stack is designed to run rootless under Podman with SELinux enforcing,
without `chcon`/`:Z` gymnastics:

- **No host bind mounts.** The SQLite cache lives on a **named volume**
  (`api-cache:/data`), and the collector configuration is **baked into its
  image** rather than mounted from the host. Named volumes are labeled correctly
  by Podman automatically, so there is nothing to relabel.
- **Writable cache directory.** The API image creates `/data` owned by UID 1654
  and points the cache connection string at `/data/weather-cache.db`, so the
  non-root user can write even though the rest of the filesystem is read-only to
  it in practice.
- **Non-root users.** Both app images run as UID 1654; nothing requires
  privileged mode or host networking.

If you *prefer* to bind-mount the collector config instead of baking it in, add
the `:Z` suffix so Podman relabels it for the container's SELinux context:

```yaml
volumes:
  - ./otelcol-config.yaml:/etc/otel/config.yaml:Z,ro
```

## The Aspire nuance: dashboard vs. AppHost

There are two different "Aspire" things, and only one of them is containerized:

- **The AppHost** (`src/Weather.AppHost`) is a **developer orchestrator**. When
  you `dotnet run` it, it launches the API and the dashboard itself (via the
  Developer Control Plane) and wires service discovery and telemetry for you. It
  is a local development tool and is intentionally **not** part of the container
  stack — containerizing an orchestrator that spawns processes is the wrong
  shape.
- **The Aspire dashboard** is a standalone image
  (`mcr.microsoft.com/dotnet/aspire-dashboard`) that simply renders OTLP it
  receives. *That* is what runs in the stack, fed through the collector. This is
  the supported production-style topology: apps → collector → dashboard.

So: use `dotnet run --project src/Weather.AppHost` for the rich local dev loop,
and the container stack for a deployable, tunnel-exposed environment.

## Telemetry path

Both apps set `OTEL_EXPORTER_OTLP_ENDPOINT=http://otelcol:4317`, so (per the
exporter precedence in `ServiceDefaults`) they send to the collector. The
collector fans every signal out to the Aspire dashboard **and** Uptrace. See
[`OBSERVABILITY.md`](OBSERVABILITY.md) for the full wiring and how to point it at
your own Uptrace project via `UPTRACE_DSN`.

## Cloudflare quick tunnels

Each `cloudflared` container runs `tunnel --no-autoupdate --url http://<svc>:<port>`,
which creates an **ad-hoc quick tunnel** — no Cloudflare account, login, or
pre-agreed terms required — and prints a `https://<random>.trycloudflare.com`
URL in its logs. These are throwaway URLs intended for demos and sharing a local
run; they change on every restart. For anything persistent, use a named tunnel
with a Cloudflare account instead.

## Stopping and cleaning up

```bash
podman compose -f compose.yaml down            # stop and remove containers
podman compose -f compose.yaml down -v         # also remove the cache volume
```

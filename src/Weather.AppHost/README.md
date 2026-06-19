# Weather.AppHost

The **.NET Aspire** orchestrator for local development. Running it starts the API
and the Blazor dashboard together, wires service discovery and telemetry between
them, and opens the Aspire dashboard for live logs, traces, and metrics:

```bash
dotnet run --project src/Weather.AppHost
```

It declares the app model (the API and web projects, with the web project
referencing the API by name so discovery resolves `https+http://api`).

## A developer tool, not a container

The AppHost is an **orchestrator that launches processes itself** (via the
Developer Control Plane). It is intentionally **not** part of the container
stack — containerizing a process-spawning orchestrator is the wrong shape. In
containers the solution instead runs the **standalone Aspire dashboard** image,
fed through an OpenTelemetry Collector (apps → collector → dashboard + Uptrace).
See [`../../docs/CONTAINERS.md`](../../docs/CONTAINERS.md) for that topology and
[`../../docs/OBSERVABILITY.md`](../../docs/OBSERVABILITY.md) for the telemetry
wiring.

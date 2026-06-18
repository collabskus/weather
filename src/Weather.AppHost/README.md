# Weather.AppHost

The [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) orchestrator. It's
the single entry point for running the whole system locally: it launches the
API and the Blazor front end together, wires service discovery between them, and
opens the Aspire dashboard for logs, traces, and metrics.

## Run it

```bash
dotnet run --project src/Weather.AppHost
```

That starts:

- **api** — the ASP.NET Core Weather API.
- **web** — the Blazor Server dashboard, with a reference to `api`.

The console prints a URL for the **Aspire dashboard**. Open it to see both
services, follow their logs, and inspect distributed traces and metrics —
including this app's custom cache hit/miss and NWS-latency instruments.

## How the wiring works

```csharp
var api = builder.AddProject<Projects.Weather_Api>("api");

builder.AddProject<Projects.Weather_Web>("web")
    .WithReference(api)
    .WaitFor(api);
```

- `AddProject<Projects.Weather_Api>("api")` registers the API under the logical
  name `api`. The `Projects.*` types are generated at build time by the Aspire
  app-host SDK from this project's `ProjectReference`s.
- `.WithReference(api)` injects the API's address into the web app's config so
  its typed `HttpClient` (base address `https+http://api`) resolves through
  service discovery.
- `.WaitFor(api)` holds the web app's start until the API is up.

## Observability out of the box

Both services call `AddServiceDefaults()` from
[`Weather.ServiceDefaults`](../Weather.ServiceDefaults/README.md), which
configures OpenTelemetry to export over OTLP. When run under this host, that
telemetry flows straight into the Aspire dashboard — no extra configuration.

## Notes

- The `Aspire.AppHost.Sdk` version in the `.csproj` must match the
  `Aspire.Hosting.AppHost` package version. The package version is managed
  centrally in `Directory.Packages.props`; the SDK version is pinned in the
  project file because Central Package Management can't manage SDK references.
- For a setup that doesn't use Aspire, run the API and Web projects directly and
  set `WeatherApi:BaseUrl` on the web app — see the
  [root README](../../README.md).

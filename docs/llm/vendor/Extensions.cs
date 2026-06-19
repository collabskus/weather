using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Weather.ServiceDefaults;

/// <summary>
/// The canonical .NET Aspire "service defaults". Every service in the solution
/// calls <see cref="AddServiceDefaults"/> to get consistent OpenTelemetry,
/// health checks, service discovery and resilient HTTP out of the box. This
/// project is deliberately app-agnostic — app-specific meters and trace sources
/// are registered by the host (see <c>Weather.Api/Program.cs</c>).
///
/// Telemetry export is vendor-neutral OTLP and resolves in priority order:
///   1. If <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (Aspire injects it during
///      local orchestration; the container compose points it at the OpenTelemetry
///      Collector), export there. This is the path that fans out to both the
///      Aspire dashboard and Uptrace via the collector.
///   2. Otherwise, unless explicitly disabled (<c>Uptrace:Enabled=false</c>),
///      export straight to Uptrace over OTLP/gRPC using a DSN. This is what makes
///      a bare <c>dotnet run</c> (no collector) light up in Uptrace out of the box.
///   3. Otherwise, register no exporter (used by the integration tests, which set
///      <c>Uptrace:Enabled=false</c> so nothing touches the network).
/// No Uptrace SDK or NuGet package is referenced; this is plain OTLP, so the same
/// wiring works for any OTLP-compatible backend by changing configuration alone.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // Uptrace's public demo DSN, used only as the last-resort default so the app
    // exports somewhere sensible with zero configuration. Override it with the
    // UPTRACE_DSN environment variable or the "Uptrace:Dsn" configuration key.
    private const string DefaultUptraceDsn = "https://DEvhsB46kbZQ5yVRxz9mdZ@api.uptrace.dev?grpc=4317";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default for every outbound HttpClient...
            http.AddStandardResilienceHandler();

            // ...and let service discovery resolve logical service names.
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // (1) An explicit OTLP endpoint always wins. Aspire injects this during
        // local orchestration; the container compose sets it to the collector.
        var endpointConfigured =
            !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (endpointConfigured)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
            return builder;
        }

        // (3) Allow a hard opt-out (the integration tests set this) so nothing
        // is exported and no network connection is attempted.
        var uptraceEnabled = builder.Configuration.GetValue("Uptrace:Enabled", true);
        if (!uptraceEnabled)
        {
            return builder;
        }

        // (2) Fall back to shipping directly to Uptrace over OTLP.
        var dsn = builder.Configuration["Uptrace:Dsn"];
        if (string.IsNullOrWhiteSpace(dsn))
        {
            dsn = Environment.GetEnvironmentVariable("UPTRACE_DSN");
        }

        if (string.IsNullOrWhiteSpace(dsn))
        {
            dsn = DefaultUptraceDsn;
        }

        if (string.IsNullOrWhiteSpace(dsn))
        {
            return builder;
        }

        ConfigureOtlpEnvironmentForUptrace(dsn);
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
        return builder;
    }

    /// <summary>
    /// Translate an Uptrace DSN into the standard <c>OTEL_EXPORTER_OTLP_*</c>
    /// environment variables that <see cref="OtlpExporterExtensions"/> reads.
    /// Only sets a variable if the operator has not already set it, so explicit
    /// configuration is never overridden. Uptrace authenticates via the
    /// <c>uptrace-dsn</c> header and recommends gzip + delta metric temporality.
    /// </summary>
    private static void ConfigureOtlpEnvironmentForUptrace(string dsn)
    {
        var endpoint = BuildOtlpEndpointFromDsn(dsn);

        SetIfUnset("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint);
        SetIfUnset("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
        SetIfUnset("OTEL_EXPORTER_OTLP_HEADERS", $"uptrace-dsn={dsn}");
        SetIfUnset("OTEL_EXPORTER_OTLP_COMPRESSION", "gzip");
        SetIfUnset("OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE", "delta");

        static void SetIfUnset(string key, string value)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    /// <summary>
    /// Derive the OTLP/gRPC endpoint from a DSN such as
    /// <c>https://TOKEN@api.uptrace.dev?grpc=4317</c>. Falls back to the public
    /// Uptrace gRPC endpoint if the DSN cannot be parsed.
    /// </summary>
    private static string BuildOtlpEndpointFromDsn(string dsn)
    {
        const int defaultGrpcPort = 4317;

        if (!Uri.TryCreate(dsn, UriKind.Absolute, out var uri))
        {
            return $"https://api.uptrace.dev:{defaultGrpcPort}";
        }

        var port = defaultGrpcPort;
        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 &&
                parts[0].Equals("grpc", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                port = parsed;
                break;
            }
        }

        var scheme = string.IsNullOrEmpty(uri.Scheme) ? "https" : uri.Scheme;
        return $"{scheme}://{uri.Host}:{port}";
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // A self-check tagged "live" backs the liveness endpoint.
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps <c>/health</c> (are we ready?) and <c>/alive</c> (are we running?).
    /// These are intended for the development/orchestration environment; lock
    /// them down before exposing them publicly.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);

            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("live"),
            });
        }

        return app;
    }
}

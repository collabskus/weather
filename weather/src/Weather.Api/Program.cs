using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Weather.Api.Endpoints;
using Weather.Core.Telemetry;
using Weather.Infrastructure;
using Weather.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery,
// resilient HttpClient defaults.
builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// NWS client, SQLite caches, weather orchestration, background warmer.
builder.Services.AddWeatherInfrastructure(builder.Configuration);

// Register the application's custom meter and trace source with OpenTelemetry.
// Kept here (not in ServiceDefaults) so ServiceDefaults stays app-agnostic.
builder.Services.ConfigureOpenTelemetryMeterProvider(metrics =>
    metrics.AddMeter(WeatherTelemetry.MeterName));
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
    tracing.AddSource(WeatherTelemetry.ActivitySourceName));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // OpenAPI document at /openapi/v1.json in Development.
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapWeatherEndpoints();

app.Run();

// Exposed so Weather.Api.Tests can drive the app with WebApplicationFactory<Program>.
public partial class Program;

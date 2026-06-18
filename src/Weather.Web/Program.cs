using Weather.ServiceDefaults;
using Weather.Web.Components;
using Weather.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Browser geolocation is reached through an interface so the dashboard can be
// unit-tested without a real browser (see Weather.Web.Tests).
builder.Services.AddScoped<IGeolocationService, GeolocationService>();

// Typed client to the backend. Under Aspire the "https+http://api" scheme is
// resolved by service discovery (added in ServiceDefaults). To run the Web app
// standalone, set WeatherApi:BaseUrl (or the services__api__https__0 env var).
var apiBaseUrl = builder.Configuration["WeatherApi:BaseUrl"] ?? "https+http://api";
builder.Services.AddHttpClient<IWeatherApiClient, WeatherApiClient>(client =>
    client.BaseAddress = new Uri(apiBaseUrl));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapDefaultEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

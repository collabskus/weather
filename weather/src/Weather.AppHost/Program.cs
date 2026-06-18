// .NET Aspire orchestration. The dashboard depends on the API, so the API is
// declared first and the web front end references it — that reference is what
// makes service discovery resolve "https+http://api" inside Weather.Web, and it
// also injects the API's URL into the web app's configuration automatically.
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Weather_Api>("api");

builder.AddProject<Projects.Weather_Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();

using JeffyAspire.Hosting;
using Microsoft.Extensions.Hosting;


var builder = DistributedApplication.CreateBuilder(args);

// Disable dashboard authentication in development
if (builder.Environment.IsDevelopment())
{
    const string AspireDashboardUnsecuredAllowAnonymous = "ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS";
    Environment.SetEnvironmentVariable(AspireDashboardUnsecuredAllowAnonymous, "true");
}


// JeffyMCP HTTP is intentional under Aspire so demos can show Api → MCP traces in the dashboard.
// Endpoint stays non-external (loopback) — still unauthenticated; stdio remains the non-Aspire default.
var jeffymcp = builder.AddJeffyMcp();
var api = builder.AddJeffyApi(jeffymcp);
builder.AddJeffyWeb(api);

builder.Build().Run();

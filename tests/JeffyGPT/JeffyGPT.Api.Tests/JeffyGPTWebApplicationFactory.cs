using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Infrastructure.Mcp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JeffyGPT.Api.Tests;

public sealed class JeffyGPTWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("JeffyGPT:Assistant:Provider", "Fake");
        builder.UseSetting("JeffyGPT:Mcp:Servers:0:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IMcpToolBootstrapper, TestMcpToolBootstrapper>();
        });
    }
}

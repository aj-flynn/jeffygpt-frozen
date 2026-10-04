using JeffyMCP.Configuration;
using JeffyMCP.FileSystem;
using JeffyMCP.Http;
using Microsoft.AspNetCore.Hosting;


var useHttp = args.Contains("--http", StringComparer.OrdinalIgnoreCase);

if (useHttp)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();
    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

    if (!HasConfiguredUrls(builder))
    {
        builder.WebHost.UseUrls(JeffyMcpHttpBindRules.DefaultLoopbackUrl);
    }

    var allowNonLoopback = builder.Configuration.GetValue(
        JeffyMcpHttpBindRules.AllowNonLoopbackConfigKey,
        defaultValue: false);
    var allowUnauthenticatedNonLoopback = builder.Configuration.GetValue(
        JeffyMcpHttpBindRules.AllowUnauthenticatedNonLoopbackConfigKey,
        defaultValue: false);
    var configuredUrls = ResolveConfiguredUrls(builder);
    JeffyMcpHttpBindRules.EnsureLoopbackOnly(
        configuredUrls,
        allowNonLoopback,
        allowUnauthenticatedNonLoopback);

    AddJeffyMcpServices(
        builder.Services,
        builder.Configuration,
        mcp => mcp.WithHttpTransport(options => options.Stateless = true));

    var app = builder.Build();
    app.MapDefaultEndpoints();
    app.MapMcp();
    await app.RunAsync();
}
else
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.AddConsole(consoleLogOptions =>
    {
        consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    AddJeffyMcpServices(
        builder.Services,
        builder.Configuration,
        mcp => mcp.WithStdioServerTransport());

    await builder.Build().RunAsync();
}

static void AddJeffyMcpServices(
    IServiceCollection services,
    IConfiguration configuration,
    Func<IMcpServerBuilder, IMcpServerBuilder> configureTransport)
{
    services.Configure<ToolOptions>(configuration.GetSection(ToolOptions.SectionName));
    services.AddSingleton<FileSystemSandbox>();
    configureTransport(services.AddMcpServer()).WithToolsFromAssembly();
}

static bool HasConfiguredUrls(WebApplicationBuilder builder)
{
    return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
        || !string.IsNullOrWhiteSpace(builder.Configuration["urls"])
        || !string.IsNullOrWhiteSpace(builder.WebHost.GetSetting(WebHostDefaults.ServerUrlsKey));
}

static IEnumerable<string> ResolveConfiguredUrls(WebApplicationBuilder builder)
{
    var fromEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (!string.IsNullOrWhiteSpace(fromEnv))
    {
        return JeffyMcpHttpBindRules.SplitUrls(fromEnv);
    }

    var fromConfig = builder.Configuration["urls"]
        ?? builder.WebHost.GetSetting(WebHostDefaults.ServerUrlsKey);
    if (!string.IsNullOrWhiteSpace(fromConfig))
    {
        return JeffyMcpHttpBindRules.SplitUrls(fromConfig);
    }

    return [JeffyMcpHttpBindRules.DefaultLoopbackUrl];
}

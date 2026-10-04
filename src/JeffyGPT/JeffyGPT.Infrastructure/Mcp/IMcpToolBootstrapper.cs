namespace JeffyGPT.Infrastructure.Mcp;

public interface IMcpToolBootstrapper
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

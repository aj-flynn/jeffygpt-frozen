using JeffyMCP.Http;

namespace JeffyMCP.Tests.Http;

public class JeffyMcpHttpBindRulesTests
{
    [Theory]
    [InlineData("http://127.0.0.1:3001")]
    [InlineData("http://localhost:3001")]
    [InlineData("http://[::1]:3001")]
    public void EnsureLoopbackOnly_AllowsLoopback(string url)
    {
        JeffyMcpHttpBindRules.EnsureLoopbackOnly([url], allowNonLoopback: false);
    }

    [Fact]
    public void EnsureLoopbackOnly_RejectsNonLoopback()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JeffyMcpHttpBindRules.EnsureLoopbackOnly(["http://0.0.0.0:3001"], allowNonLoopback: false));

        Assert.Contains("loopback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureLoopbackOnly_AllowsNonLoopbackWhenBothOptInsSet()
    {
        JeffyMcpHttpBindRules.EnsureLoopbackOnly(
            ["http://0.0.0.0:3001"],
            allowNonLoopback: true,
            allowUnauthenticatedNonLoopback: true);
    }

    [Fact]
    public void EnsureLoopbackOnly_RejectsNonLoopbackWhenOnlyAllowNonLoopbackSet()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JeffyMcpHttpBindRules.EnsureLoopbackOnly(
                ["http://0.0.0.0:3001"],
                allowNonLoopback: true,
                allowUnauthenticatedNonLoopback: false));

        Assert.Contains("Unauthenticated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

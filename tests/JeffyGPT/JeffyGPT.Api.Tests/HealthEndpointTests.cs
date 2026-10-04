using System.Net;
using System.Net.Http.Json;
using JeffyGPT.Contracts.Health;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JeffyGPT.Api.Tests;

public class HealthEndpointTests : IClassFixture<JeffyGPTWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(JeffyGPTWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
    }
}

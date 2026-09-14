using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenRouterRoutingDemo.Core;

namespace OpenRouterRoutingDemo.Api.IntegrationTests;

public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RootReportsSafeSimulatedMode()
    {
        var response = await _client.GetAsync("/");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"mode\":\"simulated\"", json, StringComparison.Ordinal);
        Assert.Contains("economy", json, StringComparison.Ordinal);
        Assert.Contains("resilient-private", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatReturnsSimulatedResponseWithoutCredentials()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/chat/economy",
            new ChatGatewayRequest("Explain dependency injection."));
        var payload = await response.Content.ReadFromJsonAsync<ChatGatewayResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload.Simulated);
        Assert.Equal("simulator", payload.Provider);
    }

    [Fact]
    public async Task ChatRejectsUnknownPolicy()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/chat/unknown",
            new ChatGatewayRequest("Hello"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChatRejectsBlankPrompt()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/chat/economy",
            new ChatGatewayRequest(" "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompatibleEndpointIsUnavailableInSimulatedMode()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/chat-compatible",
            new ChatGatewayRequest("Hello"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ChatMapsUpstreamFailureWithoutLeakingItsBody()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOpenRouterGateway>();
                services.AddSingleton<IOpenRouterGateway, FailingGateway>();
            })).CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/chat/economy",
            new ChatGatewayRequest("Hello"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("HTTP 429", body, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive upstream detail", body, StringComparison.Ordinal);
    }

    private sealed class FailingGateway : IOpenRouterGateway
    {
        public Task<ChatGatewayResponse> SendAsync(
            RoutingPolicy policy,
            string prompt,
            CancellationToken cancellationToken = default) =>
            throw new OpenRouterRequestException(
                HttpStatusCode.TooManyRequests,
                "sensitive upstream detail");
    }
}

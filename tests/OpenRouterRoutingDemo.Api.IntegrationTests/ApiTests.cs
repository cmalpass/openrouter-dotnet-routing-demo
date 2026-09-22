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
        Assert.Contains("free", json, StringComparison.Ordinal);
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

    [Fact]
    public async Task LiveModeRejectsAnonymousChatRequestsBeforeCallingTheGateway()
    {
        using var environment = new LiveModeEnvironment();
        var gateway = new CountingGateway();
        using var client = CreateLiveModeClient(gateway);

        var response = await client.PostAsJsonAsync(
            "/api/chat/economy",
            new ChatGatewayRequest("Hello"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, gateway.RequestCount);
    }

    [Fact]
    public async Task LiveModeAllowsAnAuthenticatedChatRequest()
    {
        using var environment = new LiveModeEnvironment();
        using var client = CreateLiveModeClient();
        client.DefaultRequestHeaders.Add("X-Demo-Access-Key", "test-demo-access-key");

        var response = await client.PostAsJsonAsync(
            "/api/chat/economy",
            new ChatGatewayRequest("Hello"));
        var payload = await response.Content.ReadFromJsonAsync<ChatGatewayResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("fake", payload.Provider);
    }

    [Fact]
    public async Task LiveModeRateLimitsAuthenticatedChatRequests()
    {
        using var environment = new LiveModeEnvironment();
        using var client = CreateLiveModeClient();
        client.DefaultRequestHeaders.Add("X-Demo-Access-Key", "test-demo-access-key");

        HttpResponseMessage? lastResponse = null;
        for (var requestNumber = 0; requestNumber <= 60; requestNumber++)
        {
            lastResponse?.Dispose();
            lastResponse = await client.PostAsJsonAsync(
                "/api/chat/economy",
                new ChatGatewayRequest("Hello"));
        }

        var response = Assert.IsType<HttpResponseMessage>(lastResponse);
        using (response)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task CompatibleEndpointMapsUpstreamFailuresToBadGateway()
    {
        using var environment = new LiveModeEnvironment();
        using var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICompatibleChatGateway>();
                services.AddSingleton<ICompatibleChatGateway, ThrowingCompatibleChatGateway>();
            })).CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-Access-Key", "test-demo-access-key");

        var response = await client.PostAsJsonAsync(
            "/api/chat-compatible",
            new ChatGatewayRequest("Hello"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private HttpClient CreateLiveModeClient(IOpenRouterGateway? gateway = null) => _factory.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOpenRouterGateway>();
            services.AddSingleton(gateway ?? new SuccessfulGateway());
        })).CreateClient();

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

    private sealed class SuccessfulGateway : IOpenRouterGateway
    {
        public Task<ChatGatewayResponse> SendAsync(
            RoutingPolicy policy,
            string prompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatGatewayResponse(
                "Fake response",
                policy.Models[0],
                "fake",
                0,
                0,
                0,
                0m,
                false));
    }

    private sealed class CountingGateway : IOpenRouterGateway
    {
        public int RequestCount { get; private set; }

        public Task<ChatGatewayResponse> SendAsync(
            RoutingPolicy policy,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return Task.FromResult(new ChatGatewayResponse(
                "Fake response",
                policy.Models[0],
                "fake",
                0,
                0,
                0,
                0m,
                false));
        }
    }

    private sealed class ThrowingCompatibleChatGateway : ICompatibleChatGateway
    {
        public Task<CompatibleChatGatewayResponse> SendAsync(
            string prompt,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("The fake upstream is unavailable.");
    }

    private sealed class LiveModeEnvironment : IDisposable
    {
        private readonly string? _apiKey;
        private readonly string? _accessKey;
        private readonly string? _liveMode;

        public LiveModeEnvironment()
        {
            _apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            _accessKey = Environment.GetEnvironmentVariable("OPENROUTER_DEMO_ACCESS_KEY");
            _liveMode = Environment.GetEnvironmentVariable("OpenRouter__UseLiveApi");
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-openrouter-key");
            Environment.SetEnvironmentVariable("OPENROUTER_DEMO_ACCESS_KEY", "test-demo-access-key");
            Environment.SetEnvironmentVariable("OpenRouter__UseLiveApi", "true");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", _apiKey);
            Environment.SetEnvironmentVariable("OPENROUTER_DEMO_ACCESS_KEY", _accessKey);
            Environment.SetEnvironmentVariable("OpenRouter__UseLiveApi", _liveMode);
        }
    }
}

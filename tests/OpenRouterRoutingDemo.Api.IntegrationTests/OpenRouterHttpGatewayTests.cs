using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenRouterRoutingDemo.Api;
using OpenRouterRoutingDemo.Core;

namespace OpenRouterRoutingDemo.Api.IntegrationTests;

public sealed class OpenRouterHttpGatewayTests
{
    [Fact]
    public async Task SendAsyncMapsPolicyHeadersAndUsage()
    {
        string? requestBody = null;
        string? authorization = null;
        string? applicationTitle = null;
        string? applicationUrl = null;
        string? routerMetadata = null;

        var handler = new StubHttpMessageHandler(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            authorization = request.Headers.Authorization?.ToString();
            applicationTitle = request.Headers.GetValues("X-OpenRouter-Title").Single();
            applicationUrl = request.Headers.GetValues("HTTP-Referer").Single();
            routerMetadata = request.Headers.GetValues("X-OpenRouter-Metadata").Single();

            const string responseJson = """
                {
                  "model": "openai/gpt-5-mini",
                  "choices": [
                    { "message": { "content": "A routed response." } }
                  ],
                  "usage": {
                    "prompt_tokens": 12,
                    "completion_tokens": 4,
                    "total_tokens": 16,
                    "cost": 0.00042
                  },
                  "openrouter_metadata": {
                    "requested": "openai/gpt-5-mini",
                    "strategy": "direct",
                    "endpoints": {
                      "total": 2,
                      "available": [
                        {
                          "provider": "Other Provider",
                          "model": "openai/gpt-5-mini",
                          "selected": false
                        },
                        {
                          "provider": "Example Provider",
                          "model": "openai/gpt-5-mini",
                          "selected": true
                        }
                      ]
                    }
                  }
                }
                """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        var options = Options.Create(new OpenRouterOptions
        {
            ApplicationTitle = "Gateway tests",
            ApplicationUrl = "https://example.test"
        });
        var gateway = new OpenRouterHttpGateway(
            httpClient,
            options,
            new TestCredentials(),
            NullLogger<OpenRouterHttpGateway>.Instance);
        var catalog = RoutingPolicyCatalog.CreateDefault();
        Assert.True(catalog.TryGet("economy", out var policy));

        var response = await gateway.SendAsync(policy!, "Hello");

        Assert.Equal("Bearer test-key", authorization);
        Assert.Equal("Gateway tests", applicationTitle);
        Assert.Equal("https://example.test", applicationUrl);
        Assert.Equal("enabled", routerMetadata);
        Assert.Contains("\"sort\":{\"by\":\"price\",\"partition\":\"none\"}", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"max_price\"", requestBody, StringComparison.Ordinal);
        Assert.Equal("Example Provider", response.Provider);
        Assert.Equal(0.00042m, response.Cost);
        Assert.Equal(16, response.TotalTokens);
        Assert.False(response.Simulated);
    }

    [Fact]
    public async Task SendAsyncSurfacesUpstreamStatusWithoutLeakingBodyIntoMessage()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("sensitive upstream detail")
            }));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        var gateway = new OpenRouterHttpGateway(
            httpClient,
            Options.Create(new OpenRouterOptions()),
            new TestCredentials(),
            NullLogger<OpenRouterHttpGateway>.Instance);
        var catalog = RoutingPolicyCatalog.CreateDefault();
        Assert.True(catalog.TryGet("economy", out var policy));

        var exception = await Assert.ThrowsAsync<OpenRouterRequestException>(
            () => gateway.SendAsync(policy!, "Hello"));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.DoesNotContain("sensitive", exception.Message, StringComparison.Ordinal);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return responseFactory(request);
        }
    }

    private sealed class TestCredentials : IOpenRouterCredentials
    {
        public string? ApiKey => "test-key";

        public string? DemoAccessKey => "test-access-key";
    }
}

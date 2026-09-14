using System.Net;
using System.Text;

namespace OpenRouterRoutingDemo.Api.IntegrationTests;

public sealed class OpenRouterCatalogClientTests
{
    [Fact]
    public async Task GetModelsAsyncFiltersToolsAndMapsCurrentMetadata()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            const string responseJson = """
                {
                  "data": [
                    {
                      "id": "provider/model",
                      "context_length": 131072,
                      "pricing": {
                        "prompt": "0.000001",
                        "completion": "0.000002"
                      },
                      "supported_parameters": ["tools", "structured_outputs"],
                      "expiration_date": null
                    }
                  ]
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        var catalog = new OpenRouterCatalogClient(httpClient);

        var models = await catalog.GetModelsAsync(
            requiresTools: true,
            sort: "pricing-low-to-high",
            take: 10);

        Assert.Contains("supported_parameters=tools", requestedUri!.Query, StringComparison.Ordinal);
        var model = Assert.Single(models);
        Assert.Equal("provider/model", model.Id);
        Assert.Equal(131072, model.ContextLength);
        Assert.Equal("0.000001", model.PromptPricePerToken);
        Assert.Contains("structured_outputs", model.SupportedParameters);
    }

    [Fact]
    public async Task GetModelsAsyncRejectsUnapprovedSort()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called.")))
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        var catalog = new OpenRouterCatalogClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(
            () => catalog.GetModelsAsync(false, "user-controlled-sort", 10));
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
}

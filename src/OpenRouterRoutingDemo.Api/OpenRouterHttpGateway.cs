using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenRouterRoutingDemo.Core;

namespace OpenRouterRoutingDemo.Api;

public sealed class OpenRouterHttpGateway(
    HttpClient httpClient,
    IOptions<OpenRouterOptions> options) : IOpenRouterGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly OpenRouterOptions _options = options.Value;

    public async Task<ChatGatewayResponse> SendAsync(
        RoutingPolicy policy,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Live mode requires the OPENROUTER_API_KEY environment variable.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(policy.CreateRequest(prompt), options: SerializerOptions)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("X-OpenRouter-Metadata", "enabled");

        if (!string.IsNullOrWhiteSpace(_options.ApplicationUrl))
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", _options.ApplicationUrl);
        }

        if (!string.IsNullOrWhiteSpace(_options.ApplicationTitle))
        {
            request.Headers.TryAddWithoutValidation("X-OpenRouter-Title", _options.ApplicationTitle);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new OpenRouterRequestException(response.StatusCode, responseBody);
        }

        var completion = JsonSerializer.Deserialize<OpenRouterChatResponse>(responseBody, SerializerOptions)
            ?? throw new InvalidOperationException("OpenRouter returned an empty response.");

        var content = completion.Choices.Count > 0
            ? completion.Choices[0].Message?.Content
            : null;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenRouter returned no assistant text.");
        }

        return new ChatGatewayResponse(
            Content: content,
            Model: completion.Model,
            Provider: completion.OpenRouterMetadata?.Endpoints?.Available
                .FirstOrDefault(endpoint => endpoint.Selected)
                ?.Provider,
            PromptTokens: completion.Usage?.PromptTokens ?? 0,
            CompletionTokens: completion.Usage?.CompletionTokens ?? 0,
            TotalTokens: completion.Usage?.TotalTokens ?? 0,
            Cost: completion.Usage?.Cost,
            Simulated: false);
    }
}

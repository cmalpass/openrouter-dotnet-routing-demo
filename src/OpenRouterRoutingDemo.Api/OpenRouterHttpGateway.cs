using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenRouterRoutingDemo.Core;

namespace OpenRouterRoutingDemo.Api;

public sealed partial class OpenRouterHttpGateway(
    HttpClient httpClient,
    IOptions<OpenRouterOptions> options,
    IOpenRouterCredentials credentials,
    ILogger<OpenRouterHttpGateway> logger) : IOpenRouterGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly OpenRouterOptions _options = options.Value;

    public async Task<ChatGatewayResponse> SendAsync(
        RoutingPolicy policy,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new InvalidOperationException(
                "Live mode requires the OPENROUTER_API_KEY environment variable.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(policy.CreateRequest(prompt), options: SerializerOptions)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);
        request.Headers.TryAddWithoutValidation("X-OpenRouter-Metadata", "enabled");

        if (!string.IsNullOrWhiteSpace(_options.ApplicationUrl))
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", _options.ApplicationUrl);
        }

        if (!string.IsNullOrWhiteSpace(_options.ApplicationTitle))
        {
            request.Headers.TryAddWithoutValidation("X-OpenRouter-Title", _options.ApplicationTitle);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                LogRequestFailure(
                    logger,
                    policy.Name,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
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

            var gatewayResponse = new ChatGatewayResponse(
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

            LogRequestCompleted(
                logger,
                policy.Name,
                gatewayResponse.Model,
                gatewayResponse.Provider,
                gatewayResponse.PromptTokens,
                gatewayResponse.CompletionTokens,
                gatewayResponse.Cost,
                stopwatch.ElapsedMilliseconds);

            return gatewayResponse;
        }
        catch (HttpRequestException)
        {
            LogTransportFailure(logger, policy.Name, "transport", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (JsonException)
        {
            LogTransportFailure(logger, policy.Name, "invalid response", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTransportFailure(logger, policy.Name, "timeout", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (InvalidOperationException)
        {
            LogTransportFailure(logger, policy.Name, "invalid response", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "OpenRouter request failed for policy {PolicyName} with HTTP {StatusCode} after {ElapsedMilliseconds} ms.")]
    private static partial void LogRequestFailure(
        ILogger logger,
        string policyName,
        int statusCode,
        long elapsedMilliseconds);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "OpenRouter request failed for policy {PolicyName} because of {FailureKind} after {ElapsedMilliseconds} ms.")]
    private static partial void LogTransportFailure(
        ILogger logger,
        string policyName,
        string failureKind,
        long elapsedMilliseconds);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "OpenRouter request completed for policy {PolicyName}. Model {Model}, provider {Provider}, prompt tokens {PromptTokens}, completion tokens {CompletionTokens}, cost {Cost}, elapsed {ElapsedMilliseconds} ms.")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string policyName,
        string model,
        string? provider,
        int promptTokens,
        int completionTokens,
        decimal? cost,
        long elapsedMilliseconds);
}

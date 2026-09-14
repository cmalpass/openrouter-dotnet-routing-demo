using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OpenRouterRoutingDemo.Api;

public sealed class OpenRouterCatalogClient(HttpClient httpClient)
{
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.Ordinal)
    {
        "pricing-low-to-high",
        "context-high-to-low",
        "throughput-high-to-low",
        "latency-low-to-high",
        "most-popular",
        "newest"
    };

    public async Task<IReadOnlyList<OpenRouterModelSummary>> GetModelsAsync(
        bool requiresTools,
        string sort,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedSorts.Contains(sort))
        {
            throw new ArgumentException("Unsupported model sort.", nameof(sort));
        }

        if (take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 100.");
        }

        var path = $"models?output_modalities=text&sort={Uri.EscapeDataString(sort)}";
        if (requiresTools)
        {
            path += "&supported_parameters=tools";
        }

        var result = await httpClient.GetFromJsonAsync<OpenRouterModelCatalogResponse>(
            path,
            cancellationToken);

        return result?.Data
            .Take(take)
            .Select(model => new OpenRouterModelSummary(
                model.Id,
                model.ContextLength,
                model.Pricing?.Prompt,
                model.Pricing?.Completion,
                model.SupportedParameters,
                model.ExpirationDate))
            .ToArray() ?? [];
    }

    private sealed class OpenRouterModelCatalogResponse
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<OpenRouterModel> Data { get; init; } = [];
    }

    private sealed class OpenRouterModel
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("context_length")]
        public int? ContextLength { get; init; }

        [JsonPropertyName("pricing")]
        public OpenRouterModelPricing? Pricing { get; init; }

        [JsonPropertyName("supported_parameters")]
        public IReadOnlyList<string> SupportedParameters { get; init; } = [];

        [JsonPropertyName("expiration_date")]
        public string? ExpirationDate { get; init; }
    }

    private sealed class OpenRouterModelPricing
    {
        [JsonPropertyName("prompt")]
        public string? Prompt { get; init; }

        [JsonPropertyName("completion")]
        public string? Completion { get; init; }
    }
}

public sealed record OpenRouterModelSummary(
    string Id,
    int? ContextLength,
    string? PromptPricePerToken,
    string? CompletionPricePerToken,
    IReadOnlyList<string> SupportedParameters,
    string? ExpirationDate);

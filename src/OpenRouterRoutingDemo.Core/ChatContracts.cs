using System.Text.Json.Serialization;

namespace OpenRouterRoutingDemo.Core;

public sealed record ChatGatewayRequest(string Prompt);

public sealed record ChatGatewayResponse(
    string Content,
    string Model,
    string? Provider,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal? Cost,
    bool Simulated);

public sealed record OpenRouterMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed class OpenRouterChatRequest
{
    [JsonPropertyName("models")]
    public required IReadOnlyList<string> Models { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<OpenRouterMessage> Messages { get; init; }

    [JsonPropertyName("max_completion_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxCompletionTokens { get; init; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("provider")]
    public required ProviderRoutingOptions Provider { get; init; }
}

public sealed class ProviderRoutingOptions
{
    [JsonPropertyName("sort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProviderSortOptions? Sort { get; init; }

    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Order { get; init; }

    [JsonPropertyName("allow_fallbacks")]
    public bool AllowFallbacks { get; init; } = true;

    [JsonPropertyName("require_parameters")]
    public bool RequireParameters { get; init; } = true;

    [JsonPropertyName("data_collection")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataCollection { get; init; }

    [JsonPropertyName("zdr")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ZeroDataRetention { get; init; }

    [JsonPropertyName("max_price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PriceCeiling? MaxPrice { get; init; }
}

public sealed record PriceCeiling(
    [property: JsonPropertyName("prompt")] decimal Prompt,
    [property: JsonPropertyName("completion")] decimal Completion);

public sealed record ProviderSortOptions(
    [property: JsonPropertyName("by")] string By,
    [property: JsonPropertyName("partition")] string Partition);

public sealed class OpenRouterChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public IReadOnlyList<OpenRouterChoice> Choices { get; init; } = [];

    [JsonPropertyName("usage")]
    public OpenRouterUsage? Usage { get; init; }

    [JsonPropertyName("openrouter_metadata")]
    public OpenRouterMetadata? OpenRouterMetadata { get; init; }
}

public sealed class OpenRouterMetadata
{
    [JsonPropertyName("endpoints")]
    public OpenRouterEndpointsMetadata? Endpoints { get; init; }
}

public sealed class OpenRouterEndpointsMetadata
{
    [JsonPropertyName("available")]
    public IReadOnlyList<OpenRouterEndpointMetadata> Available { get; init; } = [];
}

public sealed class OpenRouterEndpointMetadata
{
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("selected")]
    public bool Selected { get; init; }
}

public sealed class OpenRouterChoice
{
    [JsonPropertyName("message")]
    public OpenRouterResponseMessage? Message { get; init; }
}

public sealed class OpenRouterResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

public sealed class OpenRouterUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("cost")]
    public decimal? Cost { get; init; }
}

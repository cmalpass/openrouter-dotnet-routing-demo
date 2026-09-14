namespace OpenRouterRoutingDemo.Api;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public bool UseLiveApi { get; set; }

    public string Endpoint { get; set; } = "https://openrouter.ai/api/v1/";

    public string? ApiKey { get; set; }

    public string CompatibleModel { get; set; } = "openai/gpt-5-mini";

    public string? ApplicationUrl { get; set; }

    public string? ApplicationTitle { get; set; }
}

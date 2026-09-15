namespace OpenRouterRoutingDemo.Core;

public sealed record RoutingPolicy
{
    public RoutingPolicy(
        string name,
        string description,
        IReadOnlyList<string> models,
        ProviderRoutingOptions provider,
        int maxCompletionTokens = 600)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(provider);

        if (models.Count == 0 || models.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "A routing policy must contain at least one non-empty model ID.",
                nameof(models));
        }

        if (maxCompletionTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCompletionTokens),
                "Max completion tokens must be greater than zero.");
        }

        Name = name;
        Description = description;
        Models = models;
        Provider = provider;
        MaxCompletionTokens = maxCompletionTokens;
    }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<string> Models { get; }

    public ProviderRoutingOptions Provider { get; }

    public int MaxCompletionTokens { get; }

    public OpenRouterChatRequest CreateRequest(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (prompt.Length > 8_000)
        {
            throw new ArgumentException("Prompt must be 8,000 characters or fewer.", nameof(prompt));
        }

        return new OpenRouterChatRequest
        {
            Models = Models,
            Messages = [new OpenRouterMessage("user", prompt)],
            MaxCompletionTokens = MaxCompletionTokens,
            Provider = Provider
        };
    }
}

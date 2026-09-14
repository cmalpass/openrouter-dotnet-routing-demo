namespace OpenRouterRoutingDemo.Core;

public sealed record RoutingPolicy(
    string Name,
    string Description,
    IReadOnlyList<string> Models,
    ProviderRoutingOptions Provider,
    int MaxCompletionTokens = 600)
{
    public OpenRouterChatRequest CreateRequest(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (prompt.Length > 8_000)
        {
            throw new ArgumentException("Prompt must be 8,000 characters or fewer.", nameof(prompt));
        }

        if (Models.Count == 0)
        {
            throw new InvalidOperationException($"Routing policy '{Name}' has no configured models.");
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

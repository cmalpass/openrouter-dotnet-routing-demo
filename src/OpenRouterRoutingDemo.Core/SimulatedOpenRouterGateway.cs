namespace OpenRouterRoutingDemo.Core;

public sealed class SimulatedOpenRouterGateway : IOpenRouterGateway
{
    public Task<ChatGatewayResponse> SendAsync(
        RoutingPolicy policy,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = policy.CreateRequest(prompt);

        return Task.FromResult(new ChatGatewayResponse(
            Content: $"Simulated response for the '{policy.Name}' policy.",
            Model: policy.Models[0],
            Provider: "simulator",
            PromptTokens: 0,
            CompletionTokens: 0,
            TotalTokens: 0,
            Cost: 0m,
            Simulated: true));
    }
}

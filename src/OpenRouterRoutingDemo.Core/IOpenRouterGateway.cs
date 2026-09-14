namespace OpenRouterRoutingDemo.Core;

public interface IOpenRouterGateway
{
    Task<ChatGatewayResponse> SendAsync(
        RoutingPolicy policy,
        string prompt,
        CancellationToken cancellationToken = default);
}

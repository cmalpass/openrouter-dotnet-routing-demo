using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace OpenRouterRoutingDemo.Api;

public interface ICompatibleChatGateway
{
    Task<CompatibleChatGatewayResponse> SendAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}

public sealed record CompatibleChatGatewayResponse(string Content, string Model);

public sealed class CompatibleChatGateway(
    CompatibleChatClientFactory factory,
    IOptions<OpenRouterOptions> options) : ICompatibleChatGateway
{
    public async Task<CompatibleChatGatewayResponse> SendAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        using IChatClient chatClient = factory.Create();
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);

        return new CompatibleChatGatewayResponse(response.Text, options.Value.CompatibleModel);
    }
}

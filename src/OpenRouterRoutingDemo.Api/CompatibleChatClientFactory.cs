using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace OpenRouterRoutingDemo.Api;

public sealed class CompatibleChatClientFactory(IOptions<OpenRouterOptions> options)
{
    private readonly OpenRouterOptions _options = options.Value;

    public IChatClient Create()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "The OpenAI-compatible path requires the OPENROUTER_API_KEY environment variable.");
        }

        var client = new ChatClient(
            model: _options.CompatibleModel,
            credential: new ApiKeyCredential(_options.ApiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = new Uri(_options.Endpoint, UriKind.Absolute)
            });

        return client.AsIChatClient();
    }
}

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace OpenRouterRoutingDemo.Api;

public sealed class CompatibleChatClientFactory
{
    private readonly OpenRouterOptions _options;
    private readonly IOpenRouterCredentials _credentials;

    public CompatibleChatClientFactory(
        IOptions<OpenRouterOptions> options,
        IOpenRouterCredentials credentials)
    {
        _options = options.Value;
        _credentials = credentials;
    }

    public IChatClient Create()
    {
        if (string.IsNullOrWhiteSpace(_credentials.ApiKey))
        {
            throw new InvalidOperationException(
                "The OpenAI-compatible path requires the OPENROUTER_API_KEY environment variable.");
        }

        var client = new ChatClient(
            model: _options.CompatibleModel,
            credential: new ApiKeyCredential(_credentials.ApiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = new Uri(_options.Endpoint, UriKind.Absolute)
            });

        return client.AsIChatClient();
    }
}

using System.Net;

namespace OpenRouterRoutingDemo.Api;

public sealed class OpenRouterRequestException(HttpStatusCode statusCode, string responseBody)
    : Exception($"OpenRouter returned HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string ResponseBody { get; } = responseBody;
}

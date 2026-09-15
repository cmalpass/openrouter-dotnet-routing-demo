using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace OpenRouterRoutingDemo.Api;

public sealed class LiveModeAccessFilter(
    IOpenRouterCredentials credentials,
    IOptions<OpenRouterOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!options.Value.UseLiveApi)
        {
            return await next(context);
        }

        var expectedKey = credentials.DemoAccessKey;
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Live mode is not fully configured.");
        }

        var suppliedKey = context.HttpContext.Request.Headers["X-Demo-Access-Key"].ToString();
        if (!FixedTimeEquals(expectedKey, suppliedKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "A valid live-mode access key is required.");
        }

        return await next(context);
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}

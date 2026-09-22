using System.ClientModel;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OpenRouterRoutingDemo.Api;
using OpenRouterRoutingDemo.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<OpenRouterOptions>()
    .Bind(builder.Configuration.GetSection(OpenRouterOptions.SectionName))
    .Validate(
        options => string.Equals(
            options.Endpoint,
            "https://openrouter.ai/api/v1/",
            StringComparison.Ordinal),
        "OpenRouter:Endpoint must be https://openrouter.ai/api/v1/.")
    .Validate(
        options => !options.UseLiveApi ||
                   !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")),
        "Live mode requires the OPENROUTER_API_KEY environment variable.")
    .Validate(
        options => !options.UseLiveApi ||
                   !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENROUTER_DEMO_ACCESS_KEY")),
        "Live mode requires the OPENROUTER_DEMO_ACCESS_KEY environment variable.")
    .ValidateOnStart();

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IOpenRouterCredentials, EnvironmentOpenRouterCredentials>();
builder.Services.AddSingleton(RoutingPolicyCatalog.CreateDefault(
    builder.Configuration[$"{OpenRouterOptions.SectionName}:FreeModel"]));
builder.Services.AddSingleton<CompatibleChatClientFactory>();
builder.Services.AddSingleton<ICompatibleChatGateway, CompatibleChatGateway>();
builder.Services.AddHttpClient<OpenRouterCatalogClient>(client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var useLiveApi = builder.Configuration.GetValue<bool>($"{OpenRouterOptions.SectionName}:UseLiveApi");
if (useLiveApi)
{
    builder.Services
        .AddHttpClient<IOpenRouterGateway, OpenRouterHttpGateway>((services, client) =>
        {
            var options = services.GetRequiredService<IOptions<OpenRouterOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(90);
        });
}
else
{
    builder.Services.AddSingleton<IOpenRouterGateway, SimulatedOpenRouterGateway>();
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("live-api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();

app.MapGet("/", (IOptions<OpenRouterOptions> options, RoutingPolicyCatalog policies) =>
    Results.Ok(new
    {
        service = "OpenRouter .NET Routing Demo",
        mode = options.Value.UseLiveApi ? "live" : "simulated",
        policies = policies.All.Select(policy => policy.Name)
    }));

app.MapGet("/api/policies", (RoutingPolicyCatalog policies) =>
    Results.Ok(policies.All.Select(policy => new
    {
        policy.Name,
        policy.Description,
        policy.Models,
        policy.MaxCompletionTokens,
        policy.Provider
    })));

app.MapGet("/api/models", async (
    bool? requiresTools,
    string? sort,
    int? take,
    OpenRouterCatalogClient catalog,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await catalog.GetModelsAsync(
            requiresTools ?? false,
            sort ?? "pricing-low-to-high",
            take ?? 10,
            cancellationToken));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "query"] = [exception.Message]
        });
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "query"] = [exception.Message]
        });
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return UpstreamProblem(httpContext, StatusCodes.Status504GatewayTimeout, "The model catalogue request timed out.");
    }
    catch (HttpRequestException)
    {
        return UpstreamProblem(httpContext, StatusCodes.Status502BadGateway, "The model catalogue request failed.");
    }
    catch (JsonException)
    {
        return UpstreamProblem(httpContext, StatusCodes.Status502BadGateway, "The model catalogue returned an invalid response.");
    }
});

var policyChatEndpoint = app.MapPost("/api/chat/{policyName}", async (
    string policyName,
    ChatGatewayRequest request,
    RoutingPolicyCatalog policies,
    IOpenRouterGateway gateway,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!policies.TryGet(policyName, out var policy) || policy is null)
    {
        return Results.NotFound(new { error = $"Unknown routing policy '{policyName}'." });
    }

    if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 8_000)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Prompt)] = ["Prompt is required and must be 8,000 characters or fewer."]
        });
    }

    try
    {
        return Results.Ok(await gateway.SendAsync(policy, request.Prompt, cancellationToken));
    }
    catch (OpenRouterRequestException exception)
    {
        return UpstreamProblem(
            httpContext,
            StatusCodes.Status502BadGateway,
            $"The upstream model request failed with HTTP {(int)exception.StatusCode}.");
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return UpstreamProblem(httpContext, StatusCodes.Status504GatewayTimeout, "The upstream model request timed out.");
    }
    catch (Exception exception) when (
        exception is HttpRequestException or JsonException or InvalidOperationException)
    {
        return UpstreamProblem(httpContext, StatusCodes.Status502BadGateway, "The upstream model request failed.");
    }
});
policyChatEndpoint.AddEndpointFilter<LiveModeAccessFilter>().RequireRateLimiting("live-api");

var compatibleChatEndpoint = app.MapPost("/api/chat-compatible", async (
    ChatGatewayRequest request,
    ICompatibleChatGateway gateway,
    IOptions<OpenRouterOptions> options,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!options.Value.UseLiveApi)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The compatibility endpoint is disabled in simulated mode.");
    }

    if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 8_000)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Prompt)] = ["Prompt is required and must be 8,000 characters or fewer."]
        });
    }

    try
    {
        var response = await gateway.SendAsync(request.Prompt, cancellationToken);
        return Results.Ok(new
        {
            content = response.Content,
            model = response.Model,
            path = "OpenAI-compatible IChatClient"
        });
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return UpstreamProblem(httpContext, StatusCodes.Status504GatewayTimeout, "The compatible model request timed out.");
    }
    catch (Exception exception) when (
        exception is HttpRequestException or ClientResultException or JsonException or InvalidOperationException)
    {
        return UpstreamProblem(httpContext, StatusCodes.Status502BadGateway, "The compatible model request failed.");
    }
});
compatibleChatEndpoint.AddEndpointFilter<LiveModeAccessFilter>().RequireRateLimiting("live-api");

app.Run();

static IResult UpstreamProblem(HttpContext context, int statusCode, string title) =>
    Results.Problem(
        statusCode: statusCode,
        title: title,
        extensions: new Dictionary<string, object?>
        {
            ["traceId"] = context.TraceIdentifier
        });

public partial class Program;

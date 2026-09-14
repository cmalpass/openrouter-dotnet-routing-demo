using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenRouterRoutingDemo.Api;
using OpenRouterRoutingDemo.Core;

var builder = WebApplication.CreateBuilder(args);

var environmentApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
if (!string.IsNullOrWhiteSpace(environmentApiKey))
{
    builder.Configuration[$"{OpenRouterOptions.SectionName}:ApiKey"] = environmentApiKey;
}

builder.Services
    .AddOptions<OpenRouterOptions>()
    .Bind(builder.Configuration.GetSection(OpenRouterOptions.SectionName))
    .Validate(
        options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) &&
                   endpoint.Scheme == Uri.UriSchemeHttps,
        "OpenRouter:Endpoint must be an absolute HTTPS URL.")
    .Validate(
        options => !options.UseLiveApi || !string.IsNullOrWhiteSpace(options.ApiKey),
        "Live mode requires the OPENROUTER_API_KEY environment variable.")
    .ValidateOnStart();

builder.Services.AddSingleton(RoutingPolicyCatalog.CreateDefault());
builder.Services.AddSingleton<CompatibleChatClientFactory>();
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

var app = builder.Build();

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
});

app.MapPost("/api/chat/{policyName}", async (
    string policyName,
    ChatGatewayRequest request,
    RoutingPolicyCatalog policies,
    IOpenRouterGateway gateway,
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
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "The upstream model request failed.",
            detail: $"OpenRouter returned HTTP {(int)exception.StatusCode}.");
    }
});

app.MapPost("/api/chat-compatible", async (
    ChatGatewayRequest request,
    CompatibleChatClientFactory factory,
    IOptions<OpenRouterOptions> options,
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

    using IChatClient chatClient = factory.Create();
    var response = await chatClient.GetResponseAsync(request.Prompt, cancellationToken: cancellationToken);

    return Results.Ok(new
    {
        content = response.Text,
        model = options.Value.CompatibleModel,
        path = "OpenAI-compatible IChatClient"
    });
});

app.Run();

public partial class Program;

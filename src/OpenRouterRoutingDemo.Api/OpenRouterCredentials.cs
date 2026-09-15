namespace OpenRouterRoutingDemo.Api;

public interface IOpenRouterCredentials
{
    string? ApiKey { get; }

    string? DemoAccessKey { get; }
}

public sealed class EnvironmentOpenRouterCredentials : IOpenRouterCredentials
{
    public string? ApiKey => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

    public string? DemoAccessKey => Environment.GetEnvironmentVariable("OPENROUTER_DEMO_ACCESS_KEY");
}

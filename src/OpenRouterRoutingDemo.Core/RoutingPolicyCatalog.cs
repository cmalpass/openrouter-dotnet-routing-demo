namespace OpenRouterRoutingDemo.Core;

public sealed class RoutingPolicyCatalog
{
    private readonly IReadOnlyDictionary<string, RoutingPolicy> _policies;

    public RoutingPolicyCatalog(IEnumerable<RoutingPolicy> policies)
    {
        _policies = policies.ToDictionary(policy => policy.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<RoutingPolicy> All => _policies.Values;

    public bool TryGet(string name, out RoutingPolicy? policy) => _policies.TryGetValue(name, out policy);

    public static RoutingPolicyCatalog CreateDefault() => new(
    [
        new RoutingPolicy(
            name: "economy",
            description: "Prefer the least-expensive eligible endpoint and stop above an explicit price ceiling.",
            models: ["openai/gpt-5-mini", "openai/gpt-4.1-mini"],
            provider: new ProviderRoutingOptions
            {
                Sort = new ProviderSortOptions(By: "price", Partition: "none"),
                AllowFallbacks = true,
                RequireParameters = true,
                DataCollection = "deny",
                MaxPrice = new PriceCeiling(Prompt: 0.50m, Completion: 2.00m)
            }),
        new RoutingPolicy(
            name: "resilient-private",
            description: "Try several capable models while requiring no provider retention or data collection.",
            models:
            [
                "openai/gpt-5-mini",
                "deepseek/deepseek-v4-pro",
                "tencent/hy4-preview"
            ],
            provider: new ProviderRoutingOptions
            {
                Sort = new ProviderSortOptions(By: "throughput", Partition: "model"),
                AllowFallbacks = true,
                RequireParameters = true,
                DataCollection = "deny",
                ZeroDataRetention = true
            })
    ]);
}

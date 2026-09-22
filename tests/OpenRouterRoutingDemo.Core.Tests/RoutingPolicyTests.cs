using System.Text.Json;
using OpenRouterRoutingDemo.Core;

namespace OpenRouterRoutingDemo.Core.Tests;

public sealed class RoutingPolicyTests
{
    [Fact]
    public void EconomyPolicySerializesOpenRouterSpecificFields()
    {
        var catalog = RoutingPolicyCatalog.CreateDefault();
        Assert.True(catalog.TryGet("economy", out var policy));

        var request = policy!.CreateRequest("Summarize this pull request.");
        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"models\"", json, StringComparison.Ordinal);
        Assert.Contains("\"allow_fallbacks\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"require_parameters\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"data_collection\":\"deny\"", json, StringComparison.Ordinal);
        Assert.Contains("\"max_price\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"zdr\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ResilientPrivatePolicyRequiresZeroDataRetention()
    {
        var catalog = RoutingPolicyCatalog.CreateDefault();
        Assert.True(catalog.TryGet("resilient-private", out var policy));

        var request = policy!.CreateRequest("Explain the failure.");

        Assert.True(request.Provider.ZeroDataRetention);
        Assert.Equal("deny", request.Provider.DataCollection);
        Assert.True(request.Provider.AllowFallbacks);
        Assert.Equal(
            [
                "openai/gpt-5-mini",
                "deepseek/deepseek-v4-pro",
                "tencent/hy4-preview"
            ],
            request.Models);
    }

    [Fact]
    public void FreePolicyUsesConfiguredFreeModelAndZeroPriceCeiling()
    {
        var catalog = RoutingPolicyCatalog.CreateDefault("provider/example:free");
        Assert.True(catalog.TryGet("free", out var policy));

        var request = policy!.CreateRequest("Check the live smoke-test path.");

        Assert.Equal(["provider/example:free"], request.Models);
        Assert.True(request.Provider.AllowFallbacks);
        Assert.True(request.Provider.RequireParameters);
        Assert.Equal(0m, request.Provider.MaxPrice!.Prompt);
        Assert.Equal(0m, request.Provider.MaxPrice.Completion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRequestRejectsBlankPrompt(string prompt)
    {
        var catalog = RoutingPolicyCatalog.CreateDefault();
        Assert.True(catalog.TryGet("economy", out var policy));

        Assert.Throws<ArgumentException>(() => policy!.CreateRequest(prompt));
    }

    [Fact]
    public void CreateRequestRejectsOversizedPrompt()
    {
        var catalog = RoutingPolicyCatalog.CreateDefault();
        Assert.True(catalog.TryGet("economy", out var policy));

        var oversizedPrompt = new string('x', 8_001);

        var exception = Assert.Throws<ArgumentException>(
            () => policy!.CreateRequest(oversizedPrompt));
        Assert.Equal("prompt", exception.ParamName);
    }

    [Fact]
    public void CatalogIsCaseInsensitive()
    {
        var catalog = RoutingPolicyCatalog.CreateDefault();

        Assert.True(catalog.TryGet("RESILIENT-PRIVATE", out var policy));
        Assert.Equal("resilient-private", policy!.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsInvalidMaximumCompletionTokens(int maxCompletionTokens)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoutingPolicy(
            "test",
            "Test policy",
            ["provider/model"],
            new ProviderRoutingOptions(),
            maxCompletionTokens));
    }

    [Fact]
    public void ConstructorRejectsEmptyModelIds()
    {
        Assert.Throws<ArgumentException>(() => new RoutingPolicy(
            "test",
            "Test policy",
            [""],
            new ProviderRoutingOptions()));
    }
}

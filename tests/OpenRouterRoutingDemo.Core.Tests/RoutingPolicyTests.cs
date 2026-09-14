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
        Assert.Equal(3, request.Models.Count);
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
    public void CatalogIsCaseInsensitive()
    {
        var catalog = RoutingPolicyCatalog.CreateDefault();

        Assert.True(catalog.TryGet("RESILIENT-PRIVATE", out var policy));
        Assert.Equal("resilient-private", policy!.Name);
    }
}

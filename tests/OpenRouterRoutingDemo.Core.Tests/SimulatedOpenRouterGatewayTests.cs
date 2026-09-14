using OpenRouterRoutingDemo.Core;

namespace OpenRouterRoutingDemo.Core.Tests;

public sealed class SimulatedOpenRouterGatewayTests
{
    [Fact]
    public async Task SendAsyncReturnsDeterministicZeroCostResponse()
    {
        var catalog = RoutingPolicyCatalog.CreateDefault();
        Assert.True(catalog.TryGet("economy", out var policy));
        var gateway = new SimulatedOpenRouterGateway();

        var response = await gateway.SendAsync(policy!, "Hello");

        Assert.True(response.Simulated);
        Assert.Equal("simulator", response.Provider);
        Assert.Equal(0m, response.Cost);
        Assert.Equal(policy!.Models[0], response.Model);
    }
}

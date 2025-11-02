// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using IndustrieLite.Core.Domain;
using IndustrieLite.Core.Ports;
using IndustrieLite.Core.Resources;
using Xunit;

public class ResourceCoreServiceTests
{
    private sealed class Sink : IResourceEvents
    {
        public IReadOnlyDictionary<string, ResourceInfo>? Last { get; private set; }
        public void OnResourceInfoChanged(IReadOnlyDictionary<string, ResourceInfo> snapshot) => Last = snapshot;
    }

    [Fact]
    public void ResetTick_SetsAvailable_ToProduction()
    {
        var svc = new ResourceCoreService();
        svc.EnsureResourceExists("power");
        svc.SetProduction("power", 10);
        svc.ResetTick();
        Assert.Equal(10, svc.GetAvailable("power"));
        var info = svc.GetInfo("power");
        Assert.Equal(0, info.Consumption);
    }

    [Fact]
    public void ConsumeResource_RespectsAvailability()
    {
        var svc = new ResourceCoreService();
        svc.SetProduction("water", 5);
        svc.ResetTick();
        Assert.True(svc.ConsumeResource("water", 3));
        Assert.False(svc.ConsumeResource("water", 4));
        Assert.Equal(2, svc.GetAvailable("water"));
    }

    [Fact]
    public void EmitResourceInfoChanged_CallsSink()
    {
        var sink = new Sink();
        var svc = new ResourceCoreService(sink);
        svc.SetProduction("power", 7);
        svc.ResetTick();
        svc.EmitResourceInfoChanged();
        Assert.NotNull(sink.Last);
        Assert.True(sink.Last!.ContainsKey("power"));
        Assert.Equal(7, sink.Last!["power"].Available);
    }
}


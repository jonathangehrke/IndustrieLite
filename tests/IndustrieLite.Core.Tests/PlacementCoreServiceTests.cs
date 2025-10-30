// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using IndustrieLite.Core.Domain;
using IndustrieLite.Core.Placement;
using IndustrieLite.Core.Ports;
using IndustrieLite.Core.Primitives;
using Xunit;

public class PlacementCoreServiceTests
{
    private sealed class Land : ILandGrid
    {
        private readonly int w, h; private readonly bool owned;
        public Land(int w, int h, bool owned = true) { this.w = w; this.h = h; this.owned = owned; }
        public bool IsOwned(Int2 cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < w && cell.Y < h && owned;
        public int GetWidth() => w; public int GetHeight() => h;
    }

    private sealed class Economy : IEconomyCore
    {
        private readonly bool ok; public Economy(bool ok) { this.ok = ok; }
        public bool CanAfford(int amount) => ok;
    }

    private sealed class Roads : IRoadGrid { public bool IsRoad(Int2 cell) => false; }

    private sealed class NoDefs : IBuildingDefinitions { public BuildingDefinition? GetById(string id) => null; }

    [Fact]
    public void CanPlace_HappyPath_Defaults()
    {
        var svc = new PlacementCoreService(new Land(20, 20), new Economy(true), new NoDefs(), new Roads());
        var ok = svc.CanPlace("house", new Int2(5, 5), new List<Rect2i>(), out var size, out var cost);
        Assert.True(ok);
        Assert.Equal(new Int2(2, 2), size);
        Assert.Equal(200, cost);
    }

    [Fact]
    public void CanPlace_Collision_Fails()
    {
        var svc = new PlacementCoreService(new Land(20, 20), new Economy(true), new NoDefs(), new Roads());
        var existing = new List<Rect2i> { new Rect2i(new Int2(4,4), new Int2(2,2)) };
        var ok = svc.CanPlace("house", new Int2(5, 5), existing, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void CanPlace_InsufficientFunds_Fails()
    {
        var svc = new PlacementCoreService(new Land(20, 20), new Economy(false), new NoDefs(), new Roads());
        var ok = svc.CanPlace("house", new Int2(1, 1), new List<Rect2i>(), out _, out _);
        Assert.False(ok);
    }

    private sealed class RoadsAt : IRoadGrid
    {
        private readonly Int2 roadCell; public RoadsAt(Int2 c) { roadCell = c; }
        public bool IsRoad(Int2 cell) => cell.Equals(roadCell);
    }

    private sealed class LandNotOwnedAt : ILandGrid
    {
        private readonly int w, h; private readonly Int2 denied;
        public LandNotOwnedAt(int w, int h, Int2 denied) { this.w = w; this.h = h; this.denied = denied; }
        public bool IsOwned(Int2 cell) => !(cell.Equals(denied)) && cell.X >= 0 && cell.Y >= 0 && cell.X < w && cell.Y < h;
        public int GetWidth() => w; public int GetHeight() => h;
    }

    [Fact]
    public void TryPlan_OutOfBounds_Fails_WithCode()
    {
        var svc = new PlacementCoreService(new Land(4, 4), new Economy(true), new NoDefs(), new Roads());
        var res = svc.TryPlan("house", new Int2(3, 3), 32, new List<Rect2i>());
        Assert.False(res.Ok);
        Assert.Equal("land.out_of_bounds", res.Error!.Code);
    }

    [Fact]
    public void TryPlan_NotOwned_Fails_WithCode()
    {
        var svc = new PlacementCoreService(new LandNotOwnedAt(10, 10, new Int2(2,2)), new Economy(true), new NoDefs(), new Roads());
        var res = svc.TryPlan("house", new Int2(2, 2), 32, new List<Rect2i>());
        Assert.False(res.Ok);
        Assert.Equal("land.not_owned", res.Error!.Code);
    }

    [Fact]
    public void TryPlan_RoadCollision_Fails_WithCode()
    {
        var svc = new PlacementCoreService(new Land(10, 10), new Economy(true), new NoDefs(), new RoadsAt(new Int2(1,1)));
        var res = svc.TryPlan("house", new Int2(1, 1), 32, new List<Rect2i>());
        Assert.False(res.Ok);
        Assert.Equal("road.collision", res.Error!.Code);
    }

    [Fact]
    public void TryPlan_BuildingCollision_Fails_WithCode()
    {
        var svc = new PlacementCoreService(new Land(10, 10), new Economy(true), new NoDefs(), new Roads());
        var existing = new List<Rect2i> { new Rect2i(new Int2(4,4), new Int2(2,2)) };
        var res = svc.TryPlan("house", new Int2(5, 5), 32, existing);
        Assert.False(res.Ok);
        Assert.Equal("building.invalid_placement", res.Error!.Code);
    }

    [Fact]
    public void TryPlan_InsufficientFunds_Fails_WithCode()
    {
        var svc = new PlacementCoreService(new Land(10, 10), new Economy(false), new NoDefs(), new Roads());
        var res = svc.TryPlan("house", new Int2(2, 2), 32, new List<Rect2i>());
        Assert.False(res.Ok);
        Assert.Equal("economy.insufficient_funds", res.Error!.Code);
    }

    [Fact]
    public void TryPlan_Succeeds_ReturnsSpec()
    {
        var svc = new PlacementCoreService(new Land(10, 10), new Economy(true), new NoDefs(), new Roads());
        var res = svc.TryPlan("house", new Int2(3, 3), 32, new List<Rect2i>());
        Assert.True(res.Ok);
        Assert.NotNull(res.Value);
        Assert.Equal(new Int2(2,2), res.Value!.Size);
        Assert.Equal(new Int2(3,3), res.Value!.GridPos);
        Assert.Equal(32, res.Value!.TileSize);
    }
}

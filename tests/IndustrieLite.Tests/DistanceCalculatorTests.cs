// SPDX-License-Identifier: MIT
using Godot;
using Xunit;

public class DistanceCalculatorTests
{
    [Fact]
    public void TileDistance_Manhattan_CalculatesCorrectly()
    {
        var a = new Vector2I(0, 0);
        var b = new Vector2I(3, 4);
        var d = DistanceCalculator.GetTileDistance(a, b);
        Assert.Equal(7, d);
    }

    [Fact]
    public void WorldDistance_Euclidean_CalculatesCorrectly()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(3, 4);
        var d = DistanceCalculator.GetWorldDistance(a, b);
        Assert.Equal(5f, d, 3);
    }

    [Fact]
    public void TransportCost_FromWorld_Matches_TileCost()
    {
        var start = new Vector2(32 * 2 + 1, 32 * 3 + 5);
        var end = new Vector2(32 * 5, 32 * 7);
        var costWorld = DistanceCalculator.GetTransportCost(start, end, baseCostPerTile: 0.5, tileSize: 32);
        var costTiles = DistanceCalculator.GetTransportCostTiles(new Vector2I(2, 3), new Vector2I(5, 7), 0.5);
        Assert.Equal(costTiles, costWorld, 6);
    }
}


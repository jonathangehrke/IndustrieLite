// SPDX-License-Identifier: MIT
using Godot;
using Xunit;

public class RoadGridTests
{
    [Fact]
    public void AddRemoveRoad_Works_And_Raises_Events()
    {
        var grid = new RoadGrid(10, 10);
        var added = false;
        var removed = false;
        grid.RoadAdded += c => { if (c == new Vector2I(2, 3)) added = true; };
        grid.RoadRemoved += c => { if (c == new Vector2I(2, 3)) removed = true; };

        Assert.True(grid.AddRoad(new Vector2I(2, 3)));
        Assert.True(grid.IsRoad(new Vector2I(2, 3)));
        Assert.True(added);

        Assert.True(grid.RemoveRoad(new Vector2I(2, 3)));
        Assert.False(grid.IsRoad(new Vector2I(2, 3)));
        Assert.True(removed);
    }

    [Fact]
    public void InBounds_Works()
    {
        var grid = new RoadGrid(3, 2);
        Assert.True(grid.InBounds(new Vector2I(0, 0)));
        Assert.True(grid.InBounds(new Vector2I(2, 1)));
        Assert.False(grid.InBounds(new Vector2I(-1, 0)));
        Assert.False(grid.InBounds(new Vector2I(3, 0)));
        Assert.False(grid.InBounds(new Vector2I(0, 2)));
    }
}


// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Placement;
using IndustrieLite.Core.Primitives;
using Xunit;

public class BuildingSpecTests
{
    [Fact]
    public void Ctor_Sets_All_Properties()
    {
        var spec = new BuildingSpec("house", new Int2(3, 4), new Int2(2, 2), 32);
        Assert.Equal("house", spec.DefinitionId);
        Assert.Equal(new Int2(3, 4), spec.GridPos);
        Assert.Equal(new Int2(2, 2), spec.Size);
        Assert.Equal(32, spec.TileSize);
    }
}


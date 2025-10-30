// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Primitives;
using Xunit;

public class Rect2iTests
{
    [Fact]
    public void Intersects_ReturnsTrue_When_Overlapping()
    {
        var a = new Rect2i(new Int2(0, 0), new Int2(2, 2));
        var b = new Rect2i(new Int2(1, 1), new Int2(2, 2));
        Assert.True(a.Intersects(b));
        Assert.True(b.Intersects(a));
    }

    [Fact]
    public void Intersects_ReturnsFalse_When_Only_Touching_Edge()
    {
        var a = new Rect2i(new Int2(0, 0), new Int2(2, 2));
        var b = new Rect2i(new Int2(2, 0), new Int2(2, 2)); // Touches right edge
        Assert.False(a.Intersects(b));
        Assert.False(b.Intersects(a));
    }

    [Fact]
    public void Intersects_ReturnsTrue_When_Contained()
    {
        var outer = new Rect2i(new Int2(0, 0), new Int2(10, 10));
        var inner = new Rect2i(new Int2(2, 2), new Int2(3, 3));
        Assert.True(outer.Intersects(inner));
        Assert.True(inner.Intersects(outer));
    }

    [Fact]
    public void Intersects_ReturnsFalse_When_Separated()
    {
        var a = new Rect2i(new Int2(0, 0), new Int2(2, 2));
        var b = new Rect2i(new Int2(10, 10), new Int2(1, 1));
        Assert.False(a.Intersects(b));
        Assert.False(b.Intersects(a));
    }
}


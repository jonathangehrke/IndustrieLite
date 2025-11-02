// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Primitives;
using Xunit;

public class Int2Tests
{
    [Fact]
    public void Zero_Is_00()
    {
        Assert.Equal(0, Int2.Zero.X);
        Assert.Equal(0, Int2.Zero.Y);
        Assert.Equal(new Int2(0, 0), Int2.Zero);
    }

    [Fact]
    public void Equality_Works_For_Same_Values()
    {
        var a = new Int2(3, 7);
        var b = new Int2(3, 7);
        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Inequality_For_Different_Values()
    {
        var a = new Int2(3, 7);
        var b = new Int2(3, 8);
        Assert.NotEqual(a, b);
    }
}


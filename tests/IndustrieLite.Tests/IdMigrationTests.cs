// SPDX-License-Identifier: MIT
using Xunit;

public class IdMigrationTests
{
    [Theory]
    [InlineData("Solar", "solar_plant")]
    [InlineData("Water", "water_pump")]
    [InlineData("ChickenFarm", "chicken_farm")]
    [InlineData("City", "city")]
    [InlineData("Road", "road")]
    [InlineData("house", "house")]
    [InlineData("unknown_type", "unknown_type")]
    public void ToCanonical_Maps_Legacy_To_Canonical(string input, string expected)
    {
        var result = IdMigration.ToCanonical(input);
        Assert.Equal(expected, result);
    }
}


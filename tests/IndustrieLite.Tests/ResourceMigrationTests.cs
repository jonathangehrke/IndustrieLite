// SPDX-License-Identifier: MIT
using Godot;
using Xunit;

public class ResourceMigrationTests
{
    [Theory(Skip="Requires Godot StringName runtime (engine)")]
    [InlineData("power")]
    [InlineData("water")]
    [InlineData("workers")]
    [InlineData("chickens")]
    public void Registry_Can_Register_Default_Ids(string id)
    {
        var registry = new ResourceRegistry();
        var sn = new StringName(id);
        registry.RegisterResource(sn);
        Assert.True(registry.HasResource(sn));
    }
}

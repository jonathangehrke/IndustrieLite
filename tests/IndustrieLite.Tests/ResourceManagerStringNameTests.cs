// SPDX-License-Identifier: MIT
using Godot;
using Xunit;

public class ResourceManagerStringNameTests
{
    [Fact(Skip="Requires Godot StringName runtime (engine)")]
    public void StringName_Apis_Work_Without_Ready()
    {
        var rm = new ResourceManager();

        var power = new StringName("power");
        rm.SetProduction(power, 10);
        Assert.Equal(10, rm.GetResourceInfo(power).Production);

        rm.ResetTick();
        Assert.Equal(10, rm.GetAvailable(power));

        var ok = rm.ConsumeResource(power, 3);
        Assert.True(ok);
        Assert.Equal(7, rm.GetAvailable(power));
        Assert.Equal(3, rm.GetResourceInfo(power).Consumption);
    }

    [Fact(Skip="Requires Godot StringName runtime (engine)")]
    public void StringName_SetProduction_And_Getters_Work()
    {
        var rm = new ResourceManager();
        var power = new StringName("power");

        rm.SetProduction(power, 7);
        Assert.Equal(7, rm.GetResourceInfo(power).Production);

        // Cross-check via ID-based getter
        Assert.Equal(7, rm.GetResourceInfo(new StringName("power")).Production);
    }
}

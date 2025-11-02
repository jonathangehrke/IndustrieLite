// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Legacy type aliases for backward compatibility.
/// These classes are deprecated and will be removed in a future version.
/// Use GenericProductionBuilding instead.
/// </summary>

[System.Obsolete("ChickenFarm has been replaced by GenericProductionBuilding. Update your code to use GenericProductionBuilding or IProductionBuilding interface.")]
public partial class ChickenFarm : GenericProductionBuilding
{
    public static readonly StringName MainResourceId = new("chickens");

    public int Stock => Mathf.FloorToInt(this.GetInventory().TryGetValue(MainResourceId, out var val) ? val : 0f);
}

[System.Obsolete("GrainFarm has been replaced by GenericProductionBuilding. Update your code to use GenericProductionBuilding or IProductionBuilding interface.")]
public partial class GrainFarm : GenericProductionBuilding
{
    public static readonly StringName MainResourceId = new("grain");

    public int Stock => Mathf.FloorToInt(this.GetInventory().TryGetValue(MainResourceId, out var val) ? val : 0f);
}

[System.Obsolete("PigFarm has been replaced by GenericProductionBuilding. Update your code to use GenericProductionBuilding or IProductionBuilding interface.")]
public partial class PigFarm : GenericProductionBuilding
{
    public static readonly StringName MainResourceId = new("pig");

    public int Stock => Mathf.FloorToInt(this.GetInventory().TryGetValue(MainResourceId, out var val) ? val : 0f);
}

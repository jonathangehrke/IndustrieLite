// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Interface for the Resource Manager - handles resource production, consumption, and availability.
/// </summary>
public interface IResourceManager
{
    /// <summary>
    /// Resets available amounts to current production and clears consumption.
    /// </summary>
    void ResetTick();

    /// <summary>
    /// Complete reset of all resource values (for NewGame/Load).
    /// </summary>
    void ClearAllData();

    /// <summary>
    /// Increases production capacity for a resource by the specified amount.
    /// </summary>
    void AddProduction(StringName resourceId, int amount);

    /// <summary>
    /// Sets production capacity for a resource absolutely.
    /// </summary>
    void SetProduction(StringName resourceId, int amount);

    /// <summary>
    /// Consumes an available amount of a resource if sufficient.
    /// </summary>
    bool ConsumeResource(StringName resourceId, int amount);

    /// <summary>
    /// Returns the currently available amount of a resource.
    /// </summary>
    int GetAvailable(StringName resourceId);

    /// <summary>
    /// Returns structured info (Production/Available/Consumption) for a resource.
    /// </summary>
    ResourceInfo GetResourceInfo(StringName resourceId);

    /// <summary>
    /// Gets power production.
    /// </summary>
    int GetPowerProduction();

    /// <summary>
    /// Gets power consumption.
    /// </summary>
    int GetPowerConsumption();

    /// <summary>
    /// Gets water production.
    /// </summary>
    int GetWaterProduction();

    /// <summary>
    /// Gets water consumption.
    /// </summary>
    int GetWaterConsumption();

    /// <summary>
    /// Gets potential power consumption (all buildings, even if they can't produce).
    /// </summary>
    int GetPotentialPowerConsumption();

    /// <summary>
    /// Gets potential water consumption (all buildings, even if they can't produce).
    /// </summary>
    int GetPotentialWaterConsumption();

    /// <summary>
    /// Logs resource status to debug output.
    /// </summary>
    void LogResourceStatus();

    /// <summary>
    /// Emits ResourceInfoChanged event when resources change.
    /// </summary>
    void EmitResourceInfoChanged();

    /// <summary>
    /// Gets total amount of a resource (Manager + building inventories).
    /// </summary>
    [System.Obsolete("Use resource aggregation service instead")]
    int GetTotalOfResource(StringName resourceId);
}

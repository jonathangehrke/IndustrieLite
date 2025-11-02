// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of IResourceManager for testing.
/// Provides minimal implementation with public state for assertions.
/// </summary>
public class MockResourceManager : IResourceManager
{
    private readonly Dictionary<StringName, int> availableResources = new();
    private readonly Dictionary<StringName, int> production = new();

    public bool ResetTickWasCalled { get; set; }
    public bool ClearAllDataWasCalled { get; set; }
    public bool ConsumeResourceWasCalled { get; set; }
    public bool AddProductionWasCalled { get; set; }

    public void ResetTick()
    {
        ResetTickWasCalled = true;
    }

    public void ClearAllData()
    {
        availableResources.Clear();
        production.Clear();
        ClearAllDataWasCalled = true;
    }

    public void AddProduction(StringName resourceId, int amount)
    {
        if (!production.ContainsKey(resourceId))
            production[resourceId] = 0;
        production[resourceId] += amount;
        AddProductionWasCalled = true;
    }

    public void SetProduction(StringName resourceId, int amount)
    {
        production[resourceId] = amount;
    }

    public bool ConsumeResource(StringName resourceId, int amount)
    {
        ConsumeResourceWasCalled = true;
        if (!availableResources.ContainsKey(resourceId))
            return false;

        if (availableResources[resourceId] >= amount)
        {
            availableResources[resourceId] -= amount;
            return true;
        }
        return false;
    }

    public int GetAvailable(StringName resourceId)
    {
        return availableResources.TryGetValue(resourceId, out var value) ? value : 0;
    }

    public ResourceInfo GetResourceInfo(StringName resourceId)
    {
        return new ResourceInfo
        {
            Production = production.TryGetValue(resourceId, out var p) ? p : 0,
            Available = GetAvailable(resourceId),
            Consumption = 0
        };
    }

    public int GetPowerProduction() => 100;
    public int GetPowerConsumption() => 50;
    public int GetWaterProduction() => 100;
    public int GetWaterConsumption() => 50;
    public int GetPotentialPowerConsumption() => 50;
    public int GetPotentialWaterConsumption() => 50;
    public void LogResourceStatus() { }
    public void EmitResourceInfoChanged() { }

    [System.Obsolete("Use resource aggregation service instead")]
    public int GetTotalOfResource(StringName resourceId) => GetAvailable(resourceId);
}

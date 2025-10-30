// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of IBuildingManager for testing.
/// Provides minimal implementation with public state for assertions.
/// </summary>
public class MockBuildingManager : IBuildingManager
{
    public List<Building> Buildings { get; set; } = new();
    public List<City> Cities { get; set; } = new();
    public bool CanPlaceResult { get; set; } = true;
    public bool PlaceBuildingWasCalled { get; set; }
    public bool RemoveBuildingWasCalled { get; set; }
    public bool ClearAllDataWasCalled { get; set; }

    public bool CanPlace(string type, Vector2I cell, out Vector2I size, out int cost)
    {
        size = new Vector2I(2, 2);
        cost = 100;
        return CanPlaceResult;
    }

    public Result<bool> CanPlaceEx(string type, Vector2I cell)
    {
        return Result<bool>.Ok(CanPlaceResult);
    }

    public Building? PlaceBuilding(string type, Vector2I cell)
    {
        PlaceBuildingWasCalled = true;
        return null; // Mock doesn't create real buildings
    }

    public Result<Building> TryPlaceBuilding(string type, Vector2I cell, string? correlationId = null)
    {
        PlaceBuildingWasCalled = true;
        return Result<Building>.Err("Mock implementation");
    }

    public List<IProductionBuilding> GetProductionBuildings() => new();
    public Godot.Collections.Array<Building> GetProductionBuildingsForUI() => new();
    public List<SolarPlant> GetSolarPlants() => new();
    public List<WaterPump> GetWaterPumps() => new();
    public List<House> GetHouses() => new();

    public bool RemoveBuildingAt(Vector2I cell)
    {
        RemoveBuildingWasCalled = true;
        return true;
    }

    public bool RemoveBuilding(Building b)
    {
        RemoveBuildingWasCalled = true;
        Buildings.Remove(b);
        return true;
    }

    public Result TryRemoveBuilding(Building b, string? correlationId = null)
    {
        RemoveBuildingWasCalled = true;
        Buildings.Remove(b);
        return Result.Ok();
    }

    public Building? GetBuildingAt(Vector2I cell) => null;
    public Building? GetBuildingByGuid(Guid id) => null;
    public void RegisterBuildingGuid(Building building) { }
    public void UnregisterBuildingGuid(Building building) { }

    public void ClearAllData()
    {
        Buildings.Clear();
        Cities.Clear();
        ClearAllDataWasCalled = true;
    }

    public Godot.Collections.Array<Building> GetAllBuildings() => new();
    public Godot.Collections.Array<City> GetCitiesForUI() => new();

    [Obsolete("Use resource aggregation service instead")]
    public int GetTotalInventoryOfResource(StringName resourceId) => 0;
}

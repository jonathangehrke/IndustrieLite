// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Interface for the Building Manager - handles building placement, removal, and queries.
/// </summary>
public interface IBuildingManager
{
    /// <summary>
    /// Gets all registered buildings in the scene.
    /// </summary>
    List<Building> Buildings { get; }

    /// <summary>
    /// Gets all registered cities in the scene.
    /// </summary>
    List<City> Cities { get; }

    /// <summary>
    /// Checks if a building can be placed at the specified cell.
    /// </summary>
    bool CanPlace(string type, Vector2I cell, out Vector2I size, out int cost);

    /// <summary>
    /// Extended check with error reason. Only checks placability, does NOT create a building.
    /// </summary>
    Result<bool> CanPlaceEx(string type, Vector2I cell);

    /// <summary>
    /// Places a building (without cost deduction). Factory creates the instance.
    /// </summary>
    Building? PlaceBuilding(string type, Vector2I cell);

    /// <summary>
    /// Structured, fault-tolerant placement using Result pattern and structured logging.
    /// </summary>
    Result<Building> TryPlaceBuilding(string type, Vector2I cell, string? correlationId = null);

    /// <summary>
    /// Gets all production buildings.
    /// </summary>
    List<IProductionBuilding> GetProductionBuildings();

    /// <summary>
    /// Gets all production buildings as Godot array (GDScript-compatible).
    /// </summary>
    Godot.Collections.Array<Building> GetProductionBuildingsForUI();

    /// <summary>
    /// Gets all solar plants.
    /// </summary>
    List<SolarPlant> GetSolarPlants();

    /// <summary>
    /// Gets all water pumps.
    /// </summary>
    List<WaterPump> GetWaterPumps();

    /// <summary>
    /// Gets all houses.
    /// </summary>
    List<House> GetHouses();

    /// <summary>
    /// Removes a building at the specified cell.
    /// </summary>
    bool RemoveBuildingAt(Vector2I cell);

    /// <summary>
    /// Removes a building from the scene, deregisters it, and sends events.
    /// </summary>
    bool RemoveBuilding(Building b);

    /// <summary>
    /// Result variant: Removes a building including deregistration and events.
    /// </summary>
    Result TryRemoveBuilding(Building b, string? correlationId = null);

    /// <summary>
    /// Finds a building at the specified cell position.
    /// </summary>
    Building? GetBuildingAt(Vector2I cell);

    /// <summary>
    /// Gets a building by GUID (BuildingId) or null.
    /// </summary>
    Building? GetBuildingByGuid(Guid id);

    /// <summary>
    /// Registers the BuildingId of a building as GUID for fast lookups.
    /// </summary>
    void RegisterBuildingGuid(Building building);

    /// <summary>
    /// Removes the registration of a building's BuildingId.
    /// </summary>
    void UnregisterBuildingGuid(Building building);

    /// <summary>
    /// Clears all building data (lifecycle management).
    /// </summary>
    void ClearAllData();

    /// <summary>
    /// Gets all buildings as Godot array (UI-compatible).
    /// </summary>
    Godot.Collections.Array<Building> GetAllBuildings();

    /// <summary>
    /// Gets all cities as Godot array (UI-compatible).
    /// </summary>
    Godot.Collections.Array<City> GetCitiesForUI();

    /// <summary>
    /// Collects the total amount of a resource from all building inventories and stock values.
    /// </summary>
    [Obsolete("Use resource aggregation service instead")]
    int GetTotalInventoryOfResource(StringName resourceId);
}

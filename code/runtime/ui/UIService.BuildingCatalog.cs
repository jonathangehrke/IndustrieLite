// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService.BuildingCatalog: Database-Katalog, Buildables/Resources
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Get all buildable buildings from Database (filtered by current level)
    /// </summary>
    public Godot.Collections.Array<BuildingDef> GetBuildableBuildings()
    {
        if (database == null) InitializeServices();
        var all = database?.GetBuildableBuildings() ?? new Godot.Collections.Array<BuildingDef>();
        return FilterBuildingsByLevel(all);
    }

    /// <summary>
    /// Filters buildings by required level
    /// </summary>
    private Godot.Collections.Array<BuildingDef> FilterBuildingsByLevel(Godot.Collections.Array<BuildingDef> buildings)
    {
        var levelManager = ServiceContainer.Instance?.GetNamedService<LevelManager>("LevelManager");
        int currentLevel = levelManager?.CurrentLevel ?? 1;

        GD.Print($"UIService.FilterBuildingsByLevel: Current Level = {currentLevel}, LevelManager found = {levelManager != null}");

        var filtered = new Godot.Collections.Array<BuildingDef>();
        foreach (var building in buildings)
        {
            bool included = building.RequiredLevel <= currentLevel;
            GD.Print($"  - {building.Id} (RequiredLevel={building.RequiredLevel}): {(included ? "INCLUDED" : "EXCLUDED")}");
            if (included)
            {
                filtered.Add(building);
            }
        }
        return filtered;
    }

    /// <summary>
    /// Get buildable buildings organized by category for UI (filtered by current level)
    /// </summary>
    public Godot.Collections.Dictionary GetBuildableCatalog()
    {
        if (database == null) InitializeServices();
        var catalog = database?.GetBuildableCatalog() ?? new Godot.Collections.Dictionary();

        // Filter each category by level
        var filtered = new Godot.Collections.Dictionary();
        foreach (var key in catalog.Keys)
        {
            var category = key.AsString();
            var buildings = catalog[key].AsGodotArray<BuildingDef>();
            var filteredBuildings = FilterBuildingsByLevel(buildings);

            // Only include categories that have buildings after filtering
            if (filteredBuildings.Count > 0)
            {
                filtered[category] = filteredBuildings;
            }
        }
        return filtered;
    }

    /// <summary>
    /// Get building definition by ID
    /// </summary>
    public BuildingDef? GetBuildingDef(string buildingId)
    {
        if (database == null) InitializeServices();
        return database?.GetBuilding(buildingId);
    }

    /// <summary>
    /// Check if a building ID exists and is buildable
    /// </summary>
    public bool IsBuildingBuildable(string buildingId)
    {
        var building = GetBuildingDef(buildingId);
        return building != null && building.Cost > 0 && !building.Tags.Contains("non-buildable");
    }

    /// <summary>
    /// Get resources by ID dictionary (filtered by current level)
    /// </summary>
    public Godot.Collections.Dictionary GetResourcesById()
    {
        if (database == null) InitializeServices();
        if (database?.ResourcesById == null) return new Godot.Collections.Dictionary();

        var levelManager = ServiceContainer.Instance?.GetNamedService<LevelManager>("LevelManager");
        int currentLevel = levelManager?.CurrentLevel ?? 1;

        var result = new Godot.Collections.Dictionary();
        foreach (var kvp in database.ResourcesById)
        {
            var resource = kvp.Value;
            // Only include resources that are unlocked at current level
            if (resource.RequiredLevel <= currentLevel)
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// Get buildable buildings by category (filtered by current level)
    /// </summary>
    public Godot.Collections.Array<Godot.Collections.Dictionary> GetBuildablesByCategory(string category = "buildable")
    {
        if (database == null) InitializeServices();
        var all = database?.GetBuildablesByCategory(category) ?? new Godot.Collections.Array<Godot.Collections.Dictionary>();

        var levelManager = ServiceContainer.Instance?.GetNamedService<LevelManager>("LevelManager");
        int currentLevel = levelManager?.CurrentLevel ?? 1;

        var filtered = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var item in all)
        {
            // Check if building has RequiredLevel field
            int requiredLevel = 1;
            if (item.TryGetValue("required_level", out var reqLevel))
            {
                requiredLevel = reqLevel.AsInt32();
            }
            else
            {
                // Fallback: Try to get from BuildingDef
                var id = item.TryGetValue("id", out var idVariant) ? idVariant.AsString() : "";
                var building = GetBuildingDef(id);
                if (building != null)
                {
                    requiredLevel = building.RequiredLevel;
                }
            }

            if (requiredLevel <= currentLevel)
            {
                filtered.Add(item);
            }
        }
        return filtered;
    }
}

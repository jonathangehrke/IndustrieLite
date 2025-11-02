// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService.BuildingCatalog: Database-Katalog, Buildables/Resources.
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Get all buildable buildings from Database (filtered by current level).
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<BuildingDef> GetBuildableBuildings()
    {
        if (this.database == null)
        {
            this.InitializeServices();
        }

        var all = this.database?.GetBuildableBuildings() ?? new Godot.Collections.Array<BuildingDef>();
        return this.FilterBuildingsByLevel(all);
    }

    /// <summary>
    /// Filters buildings by required level.
    /// </summary>
    private Godot.Collections.Array<BuildingDef> FilterBuildingsByLevel(Godot.Collections.Array<BuildingDef> buildings)
    {
        int currentLevel = this.levelManager?.CurrentLevel ?? 1;

        var filtered = new Godot.Collections.Array<BuildingDef>();
        foreach (var building in buildings)
        {
            bool included = building.RequiredLevel <= currentLevel;
            if (included)
            {
                filtered.Add(building);
            }
        }
        return filtered;
    }

    /// <summary>
    /// Get buildable buildings organized by category for UI (filtered by current level).
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Dictionary GetBuildableCatalog()
    {
        if (this.database == null)
        {
            this.InitializeServices();
        }

        var catalog = this.database?.GetBuildableCatalog() ?? new Godot.Collections.Dictionary();

        // Filter each category by level
        var filtered = new Godot.Collections.Dictionary();
        foreach (var key in catalog.Keys)
        {
            var category = key.AsString();
            var buildings = catalog[key].AsGodotArray<BuildingDef>();
            var filteredBuildings = this.FilterBuildingsByLevel(buildings);

            // Only include categories that have buildings after filtering
            if (filteredBuildings.Count > 0)
            {
                filtered[category] = filteredBuildings;
            }
        }
        return filtered;
    }

    /// <summary>
    /// Get building definition by ID.
    /// </summary>
    /// <returns></returns>
    public BuildingDef? GetBuildingDef(string buildingId)
    {
        if (this.database == null)
        {
            this.InitializeServices();
        }

        // 1) Database (falls bereit)
        var def = this.database?.GetBuilding(buildingId);
        if (def != null)
        {
            return def;
        }

        // 2) Export-Fallback: DataIndex durchsuchen
        try
        {
            var di = this.dataIndex;
            if (di == null)
            {
                var sc = ServiceContainer.Instance;
                di = sc?.GetNamedService<Node>("DataIndex");
                di ??= this.GetTree()?.Root?.GetNodeOrNull("/root/DataIndex");
            }
            if (di != null && di.HasMethod("get_buildings"))
            {
                var arrVar = di.Call("get_buildings");
                if (arrVar.VariantType != Variant.Type.Nil)
                {
                    foreach (var v in (Godot.Collections.Array)arrVar)
                    {
                        var res = v.AsGodotObject();
                        if (res is BuildingDef bd && !string.IsNullOrEmpty(bd.Id) && string.Equals(bd.Id, buildingId, System.StringComparison.Ordinal))
                        {
                            return bd;
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>
    /// Check if a building ID exists and is buildable.
    /// </summary>
    /// <returns></returns>
    public bool IsBuildingBuildable(string buildingId)
    {
        var building = this.GetBuildingDef(buildingId);
        return building != null && building.Cost > 0 && !building.Tags.Contains("non-buildable");
    }

    /// <summary>
    /// Get resources by ID dictionary (filtered by current level).
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Dictionary GetResourcesById()
    {
        if (this.database == null)
        {
            this.InitializeServices();
        }

        int currentLevel = this.levelManager?.CurrentLevel ?? 1;

        // Primary: Use Database map when available and populated
        if (this.database?.ResourcesById != null && this.database.ResourcesById.Count > 0)
        {
            var resultDb = new Godot.Collections.Dictionary();
            foreach (var kvp in this.database.ResourcesById)
            {
                var resource = kvp.Value;
                if (resource != null && resource.RequiredLevel <= currentLevel)
                {
                    resultDb[kvp.Key] = resource;
                }
            }
            return resultDb;
        }

        // Export-safe fallback: query DataIndex (preloaded) directly
        var result = new Godot.Collections.Dictionary();
        try
        {
            var di = this.dataIndex;
            if (di == null)
            {
                var sc = ServiceContainer.Instance;
                di = sc?.GetNamedService<Node>("DataIndex");
                di ??= this.GetTree()?.Root?.GetNodeOrNull("/root/DataIndex");
            }
            if (di != null && di.HasMethod("get_resources"))
            {
                var arrVar = di.Call("get_resources");
                if (arrVar.VariantType != Variant.Type.Nil)
                {
                    foreach (var v in (Godot.Collections.Array)arrVar)
                    {
                        var res = v.AsGodotObject();
                        if (res is GameResourceDef def && !string.IsNullOrEmpty(def.Id))
                        {
                            if (def.RequiredLevel <= currentLevel)
                            {
                                result[def.Id] = def;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return result;
    }

    /// <summary>
    /// Get buildable buildings by category (filtered by current level).
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<Godot.Collections.Dictionary> GetBuildablesByCategory(string category = "buildable")
    {
        if (this.database == null)
        {
            this.InitializeServices();
        }

        var all = this.database?.GetBuildablesByCategory(category) ?? new Godot.Collections.Array<Godot.Collections.Dictionary>();

        int currentLevel = this.levelManager?.CurrentLevel ?? 1;

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
                var building = this.GetBuildingDef(id);
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

// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Query helpers for common building lookups and UI-friendly arrays.
/// Stateless utility that operates on provided snapshots.
/// </summary>
public class BuildingQueries
{
    public List<IProductionBuilding> GetProductionBuildings(IEnumerable<Building> buildings)
    {
        return buildings?.OfType<IProductionBuilding>().ToList() ?? new List<IProductionBuilding>();
    }

    public Godot.Collections.Array<Building> GetProductionBuildingsForUI(IEnumerable<Building> buildings)
    {
        var arr = new Godot.Collections.Array<Building>();
        if (buildings == null) return arr;
        foreach (var b in buildings)
        {
            if (b is IProductionBuilding && b != null && GodotObject.IsInstanceValid(b) && !b.IsQueuedForDeletion())
            {
                arr.Add(b);
            }
        }
        return arr;
    }

    public List<Building> GetByDefinitionId(IEnumerable<Building> buildings, string definitionId)
    {
        if (buildings == null || string.IsNullOrEmpty(definitionId)) return new List<Building>();
        return buildings.Where(b => b != null && GodotObject.IsInstanceValid(b) && b.DefinitionId == definitionId).ToList();
    }

    public Godot.Collections.Array<Building> GetByDefinitionIdForUI(IEnumerable<Building> buildings, string definitionId)
    {
        var arr = new Godot.Collections.Array<Building>();
        if (buildings == null || string.IsNullOrEmpty(definitionId)) return arr;
        foreach (var b in buildings)
        {
            if (b != null && GodotObject.IsInstanceValid(b) && !b.IsQueuedForDeletion() && b.DefinitionId == definitionId)
            {
                arr.Add(b);
            }
        }
        return arr;
    }

    public List<T> GetByType<T>(IEnumerable<Building> buildings) where T : Building
    {
        return buildings?.OfType<T>().ToList() ?? new List<T>();
    }

    public Godot.Collections.Array<T> GetByTypeForUI<[MustBeVariant] T>(IEnumerable<Building> buildings) where T : Building
    {
        var arr = new Godot.Collections.Array<T>();
        if (buildings == null) return arr;
        foreach (var b in buildings.OfType<T>())
        {
            if (b != null && GodotObject.IsInstanceValid(b) && !b.IsQueuedForDeletion())
            {
                arr.Add(b);
            }
        }
        return arr;
    }

    public Godot.Collections.Array<Building> GetAllBuildingsForUI(IEnumerable<Building> buildings)
    {
        var arr = new Godot.Collections.Array<Building>();
        if (buildings == null) return arr;
        foreach (var b in buildings)
        {
            if (b != null && GodotObject.IsInstanceValid(b) && !b.IsQueuedForDeletion())
            {
                arr.Add(b);
            }
        }
        return arr;
    }

    public Godot.Collections.Array<City> GetCitiesForUI(IEnumerable<City> cities)
    {
        var arr = new Godot.Collections.Array<City>();
        if (cities == null) return arr;
        foreach (var c in cities)
        {
            if (c != null && GodotObject.IsInstanceValid(c) && !c.IsQueuedForDeletion())
            {
                arr.Add(c);
            }
        }
        return arr;
    }
}

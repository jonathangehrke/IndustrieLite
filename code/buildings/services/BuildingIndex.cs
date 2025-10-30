// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Combines cell-based and GUID-based building indices.
/// Wraps the existing BuildingRegistry for cell lookups and
/// maintains a fast GUID map for id-based access.
/// </summary>
public class BuildingIndex
{
    private readonly BuildingRegistry cellIndex = new BuildingRegistry();
    private readonly Dictionary<Guid, Building> byGuid = new();

    public void Add(Building b)
    {
        if (b == null) return;
        this.cellIndex.Add(b);
        this.RegisterGuid(b);
    }

    public void Remove(Building b)
    {
        if (b == null) return;
        this.cellIndex.Remove(b);
        this.UnregisterGuid(b);
    }

    public Building? GetAt(Vector2I cell)
    {
        return this.cellIndex.GetAt(cell);
    }

    public Building? GetByGuid(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }
        if (this.byGuid.TryGetValue(id, out var b))
        {
            if (b != null && GodotObject.IsInstanceValid(b))
            {
                return b;
            }
            this.byGuid.Remove(id);
        }
        return null;
    }

    public void RegisterGuid(Building building)
    {
        if (building == null || string.IsNullOrEmpty(building.BuildingId))
        {
            return;
        }
        if (Guid.TryParse(building.BuildingId, out var guid))
        {
            this.byGuid[guid] = building;
        }
    }

    public void UnregisterGuid(Building building)
    {
        if (building == null || string.IsNullOrEmpty(building.BuildingId))
        {
            return;
        }
        if (Guid.TryParse(building.BuildingId, out var guid))
        {
            this.byGuid.Remove(guid);
        }
    }

    public void Clear()
    {
        this.byGuid.Clear();
        this.cellIndex.Clear();
    }
}


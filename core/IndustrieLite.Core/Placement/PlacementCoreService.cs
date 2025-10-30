// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using IndustrieLite.Core.Domain;
using IndustrieLite.Core.Ports;
using IndustrieLite.Core.Primitives;
using IndustrieLite.Core.Util;

namespace IndustrieLite.Core.Placement;

/// <summary>
/// Engine-freie Platzierungslogik.
/// </summary>
public sealed class PlacementCoreService
{
    private readonly ILandGrid land;
    private readonly IEconomyCore economy;
    private readonly IBuildingDefinitions? defs;
    private readonly IRoadGrid? roads;

    public PlacementCoreService(ILandGrid land, IEconomyCore economy, IBuildingDefinitions? defs = null, IRoadGrid? roads = null)
    {
        this.land = land;
        this.economy = economy;
        this.defs = defs;
        this.roads = roads;
    }

    public bool CanPlace(string id, Int2 cell, IReadOnlyList<Rect2i> existing, out Int2 size, out int cost)
    {
        var def = GetDef(id);
        size = def != null ? new Int2(def.Width, def.Height) : new Int2(2, 2);
        cost = def?.Cost ?? 200;

        // Bounds & Besitz & Straßen
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                var c = new Int2(cell.X + x, cell.Y + y);
                if (c.X < 0 || c.Y < 0 || c.X >= this.land.GetWidth() || c.Y >= this.land.GetHeight())
                {
                    return false;
                }
                if (!this.land.IsOwned(c))
                {
                    return false;
                }
                if (this.roads != null && this.roads.IsRoad(c))
                {
                    return false;
                }
            }
        }

        // Kollisionen
        var rect = new Rect2i(cell, size);
        foreach (var r in existing)
        {
            if (rect.Intersects(r))
            {
                return false;
            }
        }

        // Geld
        if (!this.economy.CanAfford(cost))
        {
            return false;
        }

        return true;
    }

    public CoreResult<BuildingSpec> TryPlan(string id, Int2 cell, int tileSize, IReadOnlyList<Rect2i> existing)
    {
        var def = GetDef(id);
        var size = def != null ? new Int2(def.Width, def.Height) : new Int2(2, 2);
        var cost = def?.Cost ?? 200;

        // Bounds/Ownership/Road checks with explicit errors
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                var c = new Int2(cell.X + x, cell.Y + y);
                if (c.X < 0 || c.Y < 0 || c.X >= this.land.GetWidth() || c.Y >= this.land.GetHeight())
                {
                    return CoreResult<BuildingSpec>.Fail("land.out_of_bounds", "Ein Teil des Gebaeudes liegt ausserhalb des Spielfelds");
                }
                if (!this.land.IsOwned(c))
                {
                    return CoreResult<BuildingSpec>.Fail("land.not_owned", "Ein Teil des Gebaeudes liegt auf nicht gekauftem Land");
                }
                if (this.roads != null && this.roads.IsRoad(c))
                {
                    return CoreResult<BuildingSpec>.Fail("road.collision", "Kollision mit Strasse");
                }
            }
        }

        var rect = new Rect2i(cell, size);
        foreach (var r in existing)
        {
            if (rect.Intersects(r))
            {
                return CoreResult<BuildingSpec>.Fail("building.invalid_placement", "Kollision mit bestehendem Gebaeude");
            }
        }

        if (!this.economy.CanAfford(cost))
        {
            return CoreResult<BuildingSpec>.Fail("economy.insufficient_funds", "Unzureichende Mittel");
        }

        var defId = def?.Id ?? id;
        return CoreResult<BuildingSpec>.Success(new BuildingSpec(defId, cell, size, tileSize));
    }

    private BuildingDefinition? GetDef(string id)
    {
        if (this.defs == null)
        {
            return null;
        }
        var def = this.defs.GetById(id);
        if (def != null)
        {
            return def;
        }
        var canon = ToCanonical(id);
        if (!string.Equals(canon, id, StringComparison.Ordinal))
        {
            return this.defs.GetById(canon);
        }
        return null;
    }

    private static string ToCanonical(string id)
    {
        // Minimaler Kanonisierer: trim + lowercase + spaces -> underscore
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        var s = id.Trim().ToLowerInvariant();
        s = s.Replace(' ', '_');
        return s;
    }
}

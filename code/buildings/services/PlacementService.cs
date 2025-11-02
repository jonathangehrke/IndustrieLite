// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;
using IndustrieLite.Core.Placement;
using IndustrieLite.Core.Primitives;
using IndustrieLite.Core.Ports;

/// <summary>
/// PlacementService prÃ¼ft Platzierungsregeln (Bounds, Besitz, Kollision, Kosten).
/// Delegiert die Kernlogik an den engine-freien PlacementCoreService.
/// </summary>
public class PlacementService
{
    private readonly ILandReadModel land;
    private readonly IEconomy economy;
    private readonly IBuildingDefinitionProvider? defs; // optional
    private readonly IRoadReadModel? roads; // optional
    private readonly PlacementCoreService core; // Core-Logik (Godot-frei)

    /// <summary>
    /// Port-basierter Konstruktor (testfreundlich, enginefrei im Kern).
    /// </summary>
    public PlacementService(ILandReadModel land, IEconomy economy, IBuildingDefinitionProvider? defs = null, IRoadReadModel? roads = null)
    {
        this.land = land;
        this.economy = economy;
        this.defs = defs;
        this.roads = roads;
        var landAdapter = new LandGridCoreAdapter(land);
        var econAdapter = new EconomyCoreAdapter(economy);
        var defsAdapter = defs != null ? new BuildingDefinitionsCoreAdapter(defs) : null;
        var roadAdapter = roads != null ? new RoadGridCoreAdapter(roads) : null;
        this.core = new PlacementCoreService(landAdapter, econAdapter, defsAdapter, roadAdapter);
    }

    /// <summary>
    /// Legacy-Konstruktor (KompatibilitÃ¤t): wrapped die Manager Ã¼ber Port-Adapter.
    /// </summary>
    public PlacementService(LandManager land, EconomyManager economy, Database? database = null, RoadManager? roadManager = null)
        : this(land, new EconomyPort(economy), database != null ? new DatabaseBuildingDefinitionProvider(database) : null, roadManager != null ? new RoadReadModelPort(roadManager) : null)
    {
    }

    public bool CanPlace(string type, Vector2I cell, List<Building> existing, out Vector2I size, out int cost)
    {
        var rects = new List<Rect2i>(existing.Count);
        foreach (var b in existing)
        {
            rects.Add(new Rect2i(new Int2(b.GridPos.X, b.GridPos.Y), new Int2(b.Size.X, b.Size.Y)));
        }

        var ok = this.core.CanPlace(type, new Int2(cell.X, cell.Y), rects, out var coreSize, out cost);
        size = new Vector2I(coreSize.X, coreSize.Y);
        return ok;
    }

    /// <summary>
    /// Strukturierte, fehlertolerante Platzierung mit Result-Pattern.
    /// </summary>
    public Result<Building> TryPlace(string buildingId, Vector2I cell, int tileSize, List<Building> existing, BuildingFactory factory)
    {
        if (string.IsNullOrWhiteSpace(buildingId))
        {
            return Result<Building>.Fail(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Leere Building-ID",
                new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "type", buildingId } }));
        }

        var rects = new List<Rect2i>(existing.Count);
        foreach (var b in existing)
        {
            rects.Add(new Rect2i(new Int2(b.GridPos.X, b.GridPos.Y), new Int2(b.Size.X, b.Size.Y)));
        }
        var planRes = this.core.TryPlan(buildingId, new Int2(cell.X, cell.Y), tileSize, rects);
        if (!planRes.Ok || planRes.Value == null)
        {
            var code = planRes.Error?.Code ?? "building.invalid_placement";
            var msg = planRes.Error?.Message ?? "Platzierung nicht moeglich";
            var mapped = PlacementErrorMapping.MapCoreCodeToRuntime(code);
            return Result<Building>.Fail(new ErrorInfo(mapped, msg));
        }

        var created = factory?.Create(planRes.Value.DefinitionId, cell, tileSize);
        if (created == null)
        {
            return Result<Building>.Fail(new ErrorInfo(ErrorIds.BuildingFactoryUnknownTypeName, $"Gebaude-Typ '{buildingId}' unbekannt"));
        }
        return Result<Building>.Success(created);
    }
}

// Core-Adaptertypen (Godot -> Core Ports)
internal sealed class LandGridCoreAdapter : ILandGrid
{
    private readonly ILandReadModel inner;
    public LandGridCoreAdapter(ILandReadModel inner) { this.inner = inner; }
    public bool IsOwned(Int2 cell) => this.inner.IsOwned(new Vector2I(cell.X, cell.Y));
    public int GetWidth() => this.inner.GetGridW();
    public int GetHeight() => this.inner.GetGridH();
}

internal sealed class RoadGridCoreAdapter : IRoadGrid
{
    private readonly IRoadReadModel inner;
    public RoadGridCoreAdapter(IRoadReadModel inner) { this.inner = inner; }
    public bool IsRoad(Int2 cell) => this.inner.IsRoad(new Vector2I(cell.X, cell.Y));
}

internal sealed class EconomyCoreAdapter : IEconomyCore
{
    private readonly IEconomy inner;
    public EconomyCoreAdapter(IEconomy inner) { this.inner = inner; }
    // PlacementCore should not enforce funds; BuildTool handles debit. Always allow.
    public bool CanAfford(int amount) => true;
}

internal sealed class BuildingDefinitionsCoreAdapter : IBuildingDefinitionProvider, IBuildingDefinitions
{
    private readonly IBuildingDefinitionProvider inner;
    public BuildingDefinitionsCoreAdapter(IBuildingDefinitionProvider inner) { this.inner = inner; }
    // Godot-Port bleibt verfÃ¼gbar
    public BuildingDef? GetBuilding(string id) => inner.GetBuilding(id);
    // Core-Port
    IndustrieLite.Core.Domain.BuildingDefinition? IBuildingDefinitions.GetById(string id)
    {
        var def = this.inner.GetBuilding(id);
        if (def == null) return null;
        return new IndustrieLite.Core.Domain.BuildingDefinition(def.Id, def.Width, def.Height, (int)def.Cost);
    }
}

internal static class PlacementErrorMapping
{
    public static StringName MapCoreCodeToRuntime(string code)
    {
        return code switch
        {
            "land.out_of_bounds" => ErrorIds.LandOutOfBoundsName,
            "land.not_owned" => ErrorIds.LandNotOwnedName,
            "economy.insufficient_funds" => ErrorIds.EconomyInsufficientFundsName,
            "building.invalid_placement" => ErrorIds.BuildingInvalidPlacementName,
            "road.collision" => ErrorIds.TransportInvalidArgumentName,
            _ => ErrorIds.BuildingInvalidPlacementName,
        };
    }
}

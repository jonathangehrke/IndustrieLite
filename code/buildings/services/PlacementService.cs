// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// PlacementService prueft Platzierungsregeln (Bounds, Besitz, Kollision, Kosten).
/// Spricht mit LandManager, EconomyManager und existierenden Gebaeuden.
/// </summary>
public class PlacementService
{
    private readonly LandManager land;
    private readonly EconomyManager economy;
    private readonly Database? database; // optional
    private readonly RoadManager? roadManager; // optional: fuer Kollision mit Strassen

    public PlacementService(LandManager land, EconomyManager economy, Database? database = null, RoadManager? roadManager = null)
    {
        this.land = land;
        this.economy = economy;
        this.database = database;
        this.roadManager = roadManager;
    }

    public bool CanPlace(string type, Vector2I cell, List<Building> existing, out Vector2I size, out int cost)
    {
        // Defaults (fallback bei fehlenden Daten)
        size = new Vector2I(2, 2);
        cost = 200;

        // Daten aus Database lesen (akzeptiert auch LegacyIds)
        BuildingDef? def = null;
        if (this.database != null)
        {
            def = this.database.GetBuilding(type);
            // Falls nicht gefunden, kanonische ID probieren
            if (def == null)
            {
                var canon = IdMigration.ToCanonical(type);
                if (!string.IsNullOrEmpty(canon) && !string.Equals(canon, type, System.StringComparison.Ordinal))
                {
                    def = this.database.GetBuilding(canon);
                }
            }
        }
        // Export-Fallback: DataIndex verwenden, wenn Database (noch) leer ist
        if (def == null)
        {
            try
            {
                var tree = Engine.GetMainLoop() as SceneTree;
                var di = tree?.Root?.GetNodeOrNull("/root/DataIndex");
                if (di != null)
                {
                    var canon = IdMigration.ToCanonical(type);
                    var arrVar = di.Call("get_buildings");
                    if (arrVar.VariantType != Variant.Type.Nil)
                    {
                        foreach (var v in (Godot.Collections.Array)arrVar)
                        {
                            var res = v.AsGodotObject();
                            if (res is BuildingDef bd && !string.IsNullOrEmpty(bd.Id))
                            {
                                bool legacyMatch = false;
                                if (bd.LegacyIds != null)
                                {
                                    foreach (var legacy in bd.LegacyIds)
                                    {
                                        if (string.Equals(legacy, type, StringComparison.Ordinal))
                                        {
                                            legacyMatch = true;
                                            break;
                                        }
                                    }
                                }
                                if (bd.Id == canon || legacyMatch)
                                {
                                    def = bd;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
        if (def != null)
        {
            size = new Vector2I(def.Width, def.Height);
            cost = (int)def.Cost;
        }

        // Bounds & Besitz
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                Vector2I c = new Vector2I(cell.X + x, cell.Y + y);
                if (c.X < 0 || c.Y < 0 || c.X >= this.land.GridW || c.Y >= this.land.GridH)
                {
                    return false;
                }

                if (!this.land.IsOwned(c))
                {
                    return false;
                }
                // Kollision mit Strassen: Gebaeude duerfen nicht ueber vorhandene Strassen gebaut werden
                if (this.roadManager != null && this.roadManager.IsRoad(c))
                {
                    return false;
                }
            }
        }

        // Kollision
        Rect2I rect = new Rect2I(cell, size);
        foreach (var b in existing)
        {
            if (rect.Intersects(new Rect2I(b.GridPos, b.Size)))
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

    // Neues, fehlertolerantes TryPlace basierend auf Database und Factory
    public Result<Building> TryPlace(string buildingId, Vector2I cell, int tileSize, List<Building> existing, BuildingFactory factory)
    {
        // Validierung
        if (string.IsNullOrWhiteSpace(buildingId))
        {
            return Result<Building>.Fail(new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Leere Building-ID",
                new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "type", buildingId } }));
        }

        // Size/Cost bestimmen (mit Database-Fallbacks)
        Vector2I size = new Vector2I(2, 2);
        int cost = 200;
        BuildingDef? def = null;
        if (this.database != null)
        {
            def = this.database.GetBuilding(buildingId);
            if (def == null)
            {
                var canon = IdMigration.ToCanonical(buildingId);
                if (!string.IsNullOrEmpty(canon) && !string.Equals(canon, buildingId, System.StringComparison.Ordinal))
                {
                    def = this.database.GetBuilding(canon);
                }
            }
        }
        // Export-Fallback auf DataIndex
        if (def == null)
        {
            try
            {
                var tree = Engine.GetMainLoop() as SceneTree;
                var di = tree?.Root?.GetNodeOrNull("/root/DataIndex");
                if (di != null)
                {
                    var canon = IdMigration.ToCanonical(buildingId);
                    var arrVar = di.Call("get_buildings");
                    if (arrVar.VariantType != Variant.Type.Nil)
                    {
                        foreach (var v in (Godot.Collections.Array)arrVar)
                        {
                            var res = v.AsGodotObject();
                            if (res is BuildingDef bd && !string.IsNullOrEmpty(bd.Id))
                            {
                                bool legacyMatch = false;
                                if (bd.LegacyIds != null)
                                {
                                    foreach (var legacy in bd.LegacyIds)
                                    {
                                        if (string.Equals(legacy, buildingId, StringComparison.Ordinal))
                                        {
                                            legacyMatch = true;
                                            break;
                                        }
                                    }
                                }
                                if (bd.Id == canon || legacyMatch)
                                {
                                    def = bd;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
        if (def != null)
        {
            size = new Vector2I(def.Width, def.Height);
            cost = (int)def.Cost;
        }

        // Schrittweise, detailierte Validierung fuer klares Feedback
        // 1) Bounds & Besitz fuer jede Zelle des Gebaeudes
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                Vector2I c = new Vector2I(cell.X + x, cell.Y + y);
                if (c.X < 0 || c.Y < 0 || c.X >= this.land.GridW || c.Y >= this.land.GridH)
                {
                    return Result<Building>.Fail(new ErrorInfo(
                        ErrorIds.LandOutOfBoundsName,
                        "Ein Teil des Gebaeudes liegt ausserhalb des Spielfelds",
                        new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "cell", cell }, { "size", size } }));
                }
                if (!this.land.IsOwned(c))
                {
                    return Result<Building>.Fail(new ErrorInfo(
                        ErrorIds.LandNotOwnedName,
                        "Ein Teil des Gebaeudes liegt auf nicht gekauftem Land",
                        new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "cell", cell }, { "size", size }, { "missingCell", c } }));
                }
                if (this.roadManager != null && this.roadManager.IsRoad(c))
                {
                    return Result<Building>.Fail(new ErrorInfo(
                        ErrorIds.TransportInvalidArgumentName,
                        "Kollision mit Strasse: Entferne die Strasse oder waehle eine andere Position",
                        new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "cell", cell }, { "size", size }, { "roadCell", c } }));
                }
            }
        }

        // 2) Gebaeude-Kollisionen
        Rect2I rect = new Rect2I(cell, size);
        foreach (var bExist in existing)
        {
            if (rect.Intersects(new Rect2I(bExist.GridPos, bExist.Size)))
            {
                return Result<Building>.Fail(new ErrorInfo(
                    ErrorIds.BuildingInvalidPlacementName,
                    "Kollision mit bestehendem Gebaeude",
                    new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "cell", cell }, { "size", size }, { "other", bExist.DefinitionId ?? bExist.Name } }));
            }
        }

        // Erzeugen
        var neu = factory?.Create(buildingId, cell, tileSize);
        if (neu == null)
        {
            return Result<Building>.Fail(new ErrorInfo(
                ErrorIds.BuildingFactoryUnknownTypeName,
                $"Gebaude-Typ '{buildingId}' unbekannt"));
        }

        // DETERMINISMUS: SimTick-only - Ergebnis wird im BuildingManager im SimTick verarbeitet
        return Result<Building>.Success(neu);
    }
}

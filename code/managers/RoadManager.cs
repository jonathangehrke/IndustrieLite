// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

public partial class RoadManager : Node2D, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    [Export] public int RoadCost = GameConstants.Road.DefaultRoadCost;
    // Tuning-Parameter für Phase 1 (BFS-Suche)
    [Export] public int MaxNearestRoadRadius { get; set; } = GameConstants.Road.MaxNearestRoadRadius;
    [Export] public bool EnablePathDebug { get; set; } = false;
    // Optional: Quadtree-Nearest aktivieren (Phase 2)
    [Export] public bool UseQuadtreeNearest { get; set; } = false;

    // ServiceContainer-DI: keine NodePaths mehr
    [Export] public bool SignaleAktiv { get; set; } = true;

    private LandManager landManager = default!;
    private BuildingManager buildingManager = default!;
    private EconomyManager economyManager = default!;

    private RoadGrid grid = default!;
    private RoadPathfinder pathfinder = default!;
    private RoadRenderer renderer = default!;
    private EventHub? eventHub;

    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(RoadManager), this);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_road", "RoadManagerRegisterFailed", ex.Message);
            }
        }
    }

    public override void _ExitTree()
    {
        // Sicherstellen, dass Event-Abos sauber geloest werden
        try { pathfinder?.Dispose(); } catch { }
        DebugLogger.LogRoad(() => "RoadManager: Cleanup abgeschlossen (Pathfinder disposed)");
        base._ExitTree();
    }

    public bool IsRoad(Vector2I cell) => grid.IsRoad(cell);

    public bool CanPlaceRoad(Vector2I cell)
    {
        if (!grid.InBounds(cell)) return false;
        if (!landManager.IsOwned(cell)) return false;
        if (grid.IsRoad(cell)) return false;
        // nicht unter Gebaeuden
        foreach (var b in buildingManager.Buildings)
        {
            var rect = new Rect2I(b.GridPos, b.Size);
            if (rect.HasPoint(cell))
                return false;
        }
        return true;
    }

    public bool PlaceRoad(Vector2I cell)
    {
        var res = TryPlaceRoad(cell);
        return res.Ok;
    }

    public bool RemoveRoad(Vector2I cell)
    {
        var res = TryRemoveRoad(cell);
        return res.Ok;
    }

    /// <summary>
    /// Result-Variante: Platziert eine Straße an der angegebenen Zelle mit Validierung und Logging.
    /// </summary>
    public Result TryPlaceRoad(Vector2I cell, string? correlationId = null)
    {
        try
        {
            if (!grid.InBounds(cell))
            {
                var info = new ErrorInfo(ErrorIds.RoadOutOfBoundsName, "Zelle außerhalb des Rasters",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_road", "PlaceRoadOutOfBounds", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }
            if (!landManager.IsOwned(cell))
            {
                var info = new ErrorInfo(ErrorIds.LandNotOwnedName, "Zelle nicht im Besitz",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_road", "PlaceRoadNotOwned", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }
            if (grid.IsRoad(cell))
            {
                var info = new ErrorInfo(ErrorIds.RoadAlreadyExistsName, "Dort ist bereits eine Straße",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_road", "PlaceRoadAlreadyExists", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }
            foreach (var b in buildingManager.Buildings)
            {
                var rect = new Rect2I(b.GridPos, b.Size);
                if (rect.HasPoint(cell))
                {
                    var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "Kollision mit Gebäude",
                        new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell }, { "building", b.Name } });
                    DebugLogger.Warn("debug_road", "PlaceRoadCollision", info.Message, info.Details, correlationId);
                    return Result.Fail(info);
                }
            }

            if (!economyManager.CanAffordEx(RoadCost).Ok || !economyManager.CanAfford(RoadCost))
            {
                var info = new ErrorInfo(ErrorIds.EconomyInsufficientFundsName, "Nicht genug Geld",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cost", RoadCost } });
                DebugLogger.Warn("debug_road", "PlaceRoadInsufficientFunds", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }
            var debit = economyManager.TryDebit(RoadCost, correlationId);
            if (!debit.Ok) return debit;

            if (!grid.AddRoad(cell))
            {
                var info = new ErrorInfo(ErrorIds.TransportPlanningFailedName, "Straße konnte nicht gesetzt werden");
                DebugLogger.Warn("debug_road", "PlaceRoadFailed", info.Message, null, correlationId);
                return Result.Fail(info);
            }

            renderer.QueueRedraw();
            DebugLogger.Info("debug_road", "PlaceRoadSucceeded", $"Straße platziert: {cell}",
                new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell }, { "cost", RoadCost } }, correlationId);
            if (SignaleAktiv && eventHub != null) eventHub.EmitSignal(EventHub.SignalName.RoadGraphChanged);
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_road", "PlaceRoadException", ex.Message, new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei PlaceRoad",
                new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
        }
    }

    /// <summary>
    /// Result-Variante: Entfernt eine Straße an der angegebenen Zelle mit Validierung und Logging.
    /// </summary>
    public Result TryRemoveRoad(Vector2I cell, string? correlationId = null)
    {
        try
        {
            if (!grid.InBounds(cell))
            {
                var info = new ErrorInfo(ErrorIds.RoadOutOfBoundsName, "Zelle außerhalb des Rasters",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_road", "RemoveRoadOutOfBounds", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }
            if (!grid.IsRoad(cell))
            {
                var info = new ErrorInfo(ErrorIds.RoadNotFoundName, "Keine Straße an dieser Zelle",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_road", "RemoveRoadNotFound", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }

            if (!grid.RemoveRoad(cell))
            {
                var info = new ErrorInfo(ErrorIds.TransportPlanningFailedName, "Straße konnte nicht entfernt werden",
                    new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
                DebugLogger.Warn("debug_road", "RemoveRoadFailed", info.Message, info.Details, correlationId);
                return Result.Fail(info);
            }

            renderer.QueueRedraw();
            DebugLogger.Info("debug_road", "RemoveRoadSucceeded", $"Straße entfernt: {cell}",
                new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } }, correlationId);
            if (SignaleAktiv && eventHub != null) eventHub.EmitSignal(EventHub.SignalName.RoadGraphChanged);
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_road", "RemoveRoadException", ex.Message, new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwartete Ausnahme bei RemoveRoad",
                new System.Collections.Generic.Dictionary<string, object?> { { "cell", cell } });
        }
    }

    public List<Vector2> GetPath(Vector2 fromWorld, Vector2 toWorld)
    {
        return pathfinder.GetPathWorld(fromWorld, toWorld);
    }

    /// <summary>
    /// Clear all roads (for NewGame)
    /// </summary>
    public void ClearAllRoads()
    {
        grid.Clear();
        renderer.QueueRedraw();
        DebugLogger.LogRoad(() => "RoadManager: All roads cleared");

        // RoadGraphChanged-Event emittieren
        if (SignaleAktiv && eventHub != null)
            eventHub.EmitSignal(EventHub.SignalName.RoadGraphChanged);
    }

    /// <summary>
    /// Platziert eine Straße ohne Kostenabzug (für Load/Restore)
    /// </summary>
    public bool PlaceRoadWithoutCost(Vector2I cell)
    {
        if (!grid.InBounds(cell))
        {
            DebugLogger.LogRoad(() => $"PlaceRoadWithoutCost: Zelle außerhalb des Rasters: {cell}");
            return false;
        }
        if (grid.IsRoad(cell))
        {
            // Beim Laden kann das vorkommen - einfach ignorieren
            return true;
        }

        if (!grid.AddRoad(cell))
        {
            DebugLogger.LogRoad(() => $"PlaceRoadWithoutCost: Konnte Straße nicht setzen: {cell}");
            return false;
        }

        renderer.QueueRedraw();
        if (SignaleAktiv && eventHub != null) eventHub.EmitSignal(EventHub.SignalName.RoadGraphChanged);
        return true;
    }
}

// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Verwalter fr Geb4ude: Platzierung, Entfernung, Abfragen und Registrierungen.
/// </summary>
public partial class BuildingManager : Node, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    /// <summary>
    /// Kachelgr6e in Pixeln fr die Platzierung.
    /// </summary>
    [Export]
    public int TileSize = 32;

    /// <summary>
    /// Gets alle registrierten Geb4ude in der Szene.
    /// </summary>
    public List<Building> Buildings { get; private set; } = new List<Building>();

    /// <summary>
    /// Gets alle registrierten St4dte in der Szene.
    /// </summary>
    public List<City> Cities { get; private set; } = new List<City>();

    private readonly Dictionary<Guid, Building> buildingsByGuid = new();

    [Export]
    public bool SignaleAktiv { get; set; } = true;

    private bool initialized;

    // DI über ServiceContainer (SC-only)
    private LandManager landManager = default!;
    private EconomyManager economyManager = default!;
    private Database? database; // optional
    private EventHub? eventHub;
    private ISceneGraph sceneGraph = default!;

    // Neue Services
    private PlacementService placementService = default!;
    private BuildingFactory buildingFactory = default!;
    private BuildingRegistry registry = new BuildingRegistry();

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(BuildingManager), this);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("debug_building", "RegisterWithServiceContainerFailed", ex.Message);
            }
        }
    }

    /// <summary>
    /// Prueft, ob ein Gebaeude an der Zelle platziert werden darf.
    /// Delegiert an PlacementService (Guard-Clauses, Besitz/Kollision/Kosten).
    /// </summary>
    /// <returns></returns>
    public bool CanPlace(string type, Vector2I cell, out Vector2I size, out int cost)
    {
        size = Vector2I.Zero;
        cost = 0;

        if (string.IsNullOrWhiteSpace(type))
        {
            DebugLogger.Error("debug_building", "CanPlaceInvalidType", "CanPlace called with null or empty type");
            return false;
        }

        if (this.placementService == null)
        {
            DebugLogger.Error("debug_building", "PlacementServiceNotInitialized", "PlacementService not initialized");
            return false;
        }

        try
        {
            var ok = this.placementService.CanPlace(type, cell, this.Buildings, out size, out cost);
            DebugLogger.LogServices("CanPlace check for " + type + " at " + cell + " => " + ok + ", size " + size + ", cost " + cost);
            return ok;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_building", "CanPlaceException", ex.Message,
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "type", type }, { "cell", cell } });
            return false;
        }
    }

    /// <summary>
    /// Platziert ein Gebaeude (ohne Geldabzug). Fabrik erzeugt Instanz.
    /// Registry wird aktualisiert, Events gesendet.
    /// </summary>
    /// <summary>
    /// Erzeugt und platziert ein Geb4ude des Typs an der Zelle (ohne Geldabzug).
    /// </summary>
    /// <returns></returns>
    public Building? PlaceBuilding(string type, Vector2I cell)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            DebugLogger.Error("debug_building", "PlaceBuildingInvalidType", "PlaceBuilding called with null or empty type");
            return null;
        }

        try
        {
            if (this.buildingFactory == null)
            {
                DebugLogger.Error("debug_building", "BuildingFactoryNotInitialized", "BuildingFactory not initialized via Initialize() - cannot place building");
                return null;
            }

            var b = this.buildingFactory.Create(type, cell, this.TileSize);
            if (b == null)
            {
                DebugLogger.Error("debug_building", "BuildingFactoryCreateFailed", $"Failed to create building of type {type}");
                return null;
            }

            Simulation.ValidateSimTickContext("BuildingManager: PlaceBuilding");
            // DETERMINISMUS: SimTick-only - Platzierung nur innerhalb des SimTick durchfuehren
            this.AddPlacedBuilding(b, cell);
            return b;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_building", "PlaceBuildingException", ex.Message,
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "type", type }, { "cell", cell } });
            return null;
        }
    }

    /// <summary>
    /// Erweiterte Pruefung mit Fehlergrund. Prueft nur Platzierbarkeit, erstellt KEIN Gebaeude.
    /// </summary>
    /// <returns></returns>
    public Result<bool> CanPlaceEx(string type, Vector2I cell)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(type) || this.placementService == null)
            {
                return Result<bool>.Fail(new ErrorInfo(ErrorIds.BuildingServiceUnavailableName, "BuildingManager nicht initialisiert"));
            }

            // Verwende CanPlace (prüft nur) statt TryPlace (erstellt Gebäude)
            if (!this.placementService.CanPlace(type, cell, this.Buildings, out var size, out var cost))
            {
                return Result<bool>.Fail(new ErrorInfo(ErrorIds.BuildingInvalidPlacementName, "Platzierung nicht moeglich"));
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Fehler bei CanPlaceEx");
        }
    }

    /// <summary>
    /// Strukturierte, fehlertolerante Platzierung mit Result-Pattern und Structured Logging.
    /// </summary>
    /// <returns></returns>
    public Result<Building> TryPlaceBuilding(string type, Vector2I cell, string? correlationId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return Result<Building>.Fail(new ErrorInfo(ErrorIds.ArgumentNullName, "Building-Typ fehlt", new Dictionary<string, object?>(StringComparer.Ordinal) { { "param", nameof(type) } }));
            }

            if (this.placementService == null || this.buildingFactory == null)
            {
                return Result<Building>.Fail(new ErrorInfo(ErrorIds.BuildingServiceUnavailableName, "Services fuer Platzierung nicht initialisiert"));
            }

            DebugLogger.Info("debug_building", "PlaceBuildingRequested", $"Platzierung angefragt: {type} @ {cell}",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "type", type }, { "cell", cell } }, correlationId);

            var res = this.placementService.TryPlace(type, cell, this.TileSize, this.Buildings, this.buildingFactory);
            if (!res.Ok)
            {
                var code = res.ErrorInfo?.Code ?? ErrorIds.BuildingInvalidPlacementName;
                var msg = res.ErrorInfo?.Message ?? res.Error;
                DebugLogger.Warn("debug_building", "PlaceBuildingFailed", msg,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { { "type", type }, { "cell", cell }, { "code", code } }, correlationId);
                return Result<Building>.Fail(res.ErrorInfo ?? new ErrorInfo(ErrorIds.BuildingInvalidPlacementName, msg,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { { "type", type }, { "cell", cell } }));
            }

            var b = res.Value;
            Simulation.ValidateSimTickContext("BuildingManager: TryPlaceBuilding");
            this.AddPlacedBuilding(b, cell);

            DebugLogger.Info("debug_building", "PlaceBuildingSucceeded", $"Platzierung ok: {type} @ {cell}",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "type", type }, { "cell", cell }, { "id", b.BuildingId } }, correlationId);
            return Result<Building>.Success(b);
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_building", "PlaceBuildingException", $"Unerwartete Ausnahme: {ex.Message}",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "type", type }, { "cell", cell } }, correlationId);
            return Result<Building>.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwarteter Fehler bei Gebaeude-Platzierung",
                new Dictionary<string, object?>(StringComparer.Ordinal) { { "type", type }, { "cell", cell } });
        }
    }

    private void AddPlacedBuilding(Building b, Vector2I cell)
    {
        if (string.IsNullOrEmpty(b.BuildingId))
        {
            b.BuildingId = Guid.NewGuid().ToString();
        }

        this.RegisterBuildingGuid(b);

        this.sceneGraph.AddChild(b);
        this.Buildings.Add(b);
        this.registry?.Add(b);

        // Gruppen fuer einfache UI-Queries
        b.AddToGroup("buildings");
        if (b is ChickenFarm)
        {
            b.AddToGroup("chicken_farms");
        }

        if (b is City city)
        {
            this.Cities.Add(city);
            DebugLogger.LogBuilding(() => $"BuildingManager: City '{city.CityName}' placed at {cell}, total cities: {this.Cities.Count}");
        }

        // EventHub Signal fuer neue Gebaeude (ohne DevFlag-Gating)
        if (this.SignaleAktiv && this.eventHub != null)
        {
            try
            {
                this.eventHub.EmitSignal(EventHub.SignalName.BuildingPlaced, b);
                if (b is ChickenFarm)
                {
                    this.eventHub.EmitSignal(EventHub.SignalName.FarmStatusChanged);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("debug_building", "EmitSignalFailed", ex.Message);
            }
        }
    }

    public List<ChickenFarm> GetChickenFarms()
    {
        return this.Buildings.OfType<ChickenFarm>().ToList();
    }

    // Interop-freundliche Variante fuer GDScript
    public Godot.Collections.Array<ChickenFarm> GetChickenFarmsForUI()
    {
        var arr = new Godot.Collections.Array<ChickenFarm>();
        foreach (var farm in this.Buildings.OfType<ChickenFarm>())
        {
            arr.Add(farm);
        }

        return arr;
    }

    public List<SolarPlant> GetSolarPlants()
    {
        return this.Buildings.OfType<SolarPlant>().ToList();
    }

    public List<WaterPump> GetWaterPumps()
    {
        return this.Buildings.OfType<WaterPump>().ToList();
    }

    public List<House> GetHouses()
    {
        return this.Buildings.OfType<House>().ToList();
    }

    public bool RemoveBuildingAt(Vector2I cell)
    {
        var b = this.GetBuildingAt(cell);
        if (b == null)
        {
            return false;
        }

        return this.RemoveBuilding(b);
    }

    /// <summary>
    /// Entfernt ein Gebaeude aus der Szene, deregistriert es und sendet Events.
    /// </summary>
    /// <returns></returns>
    public bool RemoveBuilding(Building b)
    {
        var res = this.TryRemoveBuilding(b);
        return res.Ok;
    }

    /// <summary>
    /// Result-Variante: Entfernt ein Gebäude inkl. Deregistrierung und Events.
    /// </summary>
    /// <returns></returns>
    public Result TryRemoveBuilding(Building b, string? correlationId = null)
    {
        if (b == null)
        {
            var info = new ErrorInfo(ErrorIds.TransportInvalidArgumentName, "RemoveBuilding: null uebergeben");
            DebugLogger.Warn("debug_building", "RemoveBuildingInvalidArg", info.Message, null, correlationId);
            return Result.Fail(info);
        }

        try
        {
            this.UnregisterBuildingGuid(b);
            this.Buildings.Remove(b);
            if (b is City city)
            {
                this.Cities.Remove(city);
            }

            this.registry?.Remove(b);

            if (this.SignaleAktiv && this.eventHub != null)
            {
                try
                {
                    this.eventHub.EmitSignal(EventHub.SignalName.BuildingDestroyed, b);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("debug_building", "RemoveBuildingSignalError", ex.Message,
                        new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "building", b.Name } }, correlationId);
                }
            }

            if (IsInstanceValid(b))
            {
                b.QueueFree();
            }

            DebugLogger.Info("debug_building", "RemoveBuildingSucceeded", $"Gebaeude entfernt: {b.Name}",
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "pos", b.GridPos }, { "type", b.GetType().Name } }, correlationId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_building", "RemoveBuildingException", ex.Message,
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "building", b?.Name } }, correlationId);
            return Result.FromException(ex, ErrorIds.SystemUnexpectedExceptionName, "Unerwarteter Fehler beim Entfernen des Gebaeudes",
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "building", b?.Name } });
        }
    }

    /// <summary>
    /// Findet ein Gebaeude an der angegebenen Zellen-Position.
    /// </summary>
    /// <summary>
    /// Liefert das Geb4ude an der angegebenen Rasterzelle oder null.
    /// </summary>
    /// <returns></returns>
    public Building? GetBuildingAt(Vector2I cell)
    {
        try
        {
            var byIdx = this.registry?.GetAt(cell);
            if (byIdx != null)
            {
                return byIdx;
            }

            // Fallback: lineare Suche (Kompatibilitaet)
            if (this.Buildings != null)
            {
                foreach (var building in this.Buildings)
                {
                    if (building == null || !IsInstanceValid(building))
                    {
                        continue;
                    }

                    if (cell.X >= building.GridPos.X && cell.X < building.GridPos.X + building.Size.X &&
                        cell.Y >= building.GridPos.Y && cell.Y < building.GridPos.Y + building.Size.Y)
                    {
                        return building;
                    }
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_building", "GetBuildingAtException", ex.Message,
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "cell", cell } });
            return null;
        }
    }

    /// <summary>
    /// Liefert ein Geb4ude per GUID (BuildingId) oder null.
    /// </summary>
    /// <returns></returns>
    public Building? GetBuildingByGuid(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        try
        {
            if (this.buildingsByGuid.TryGetValue(id, out var building))
            {
                if (building != null && IsInstanceValid(building))
                {
                    return building;
                }

                // Clean up invalid reference
                this.buildingsByGuid.Remove(id);
            }
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_building", "GetBuildingByGuidException", ex.Message,
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "guid", id } });
            return null;
        }
    }

    /// <summary>
    /// Registriert die BuildingId eines Geb4udes als GUID fr schnelle Nachschlagevorg4nge.
    /// </summary>
    public void RegisterBuildingGuid(Building building)
    {
        if (building == null)
        {
            DebugLogger.Error("debug_building", "RegisterBuildingGuidNull", "RegisterBuildingGuid called with null building");
            return;
        }

        if (string.IsNullOrEmpty(building.BuildingId))
        {
            return;
        }

        try
        {
            if (Guid.TryParse(building.BuildingId, out var guid))
            {
                this.buildingsByGuid[guid] = building;
            }
            else
            {
                DebugLogger.Error("debug_building", "RegisterBuildingGuidInvalid", $"Invalid GUID format: {building.BuildingId}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_building", "RegisterBuildingGuidException", ex.Message);
        }
    }

    /// <summary>
    /// Entfernt die Registrierung der BuildingId eines Geb4udes.
    /// </summary>
    public void UnregisterBuildingGuid(Building building)
    {
        if (building == null || string.IsNullOrEmpty(building.BuildingId))
        {
            return;
        }

        if (Guid.TryParse(building.BuildingId, out var guid))
        {
            this.buildingsByGuid.Remove(guid);
        }
    }

    /// <summary>
    /// Clears all building data - for lifecycle management.
    /// </summary>
    public void ClearAllData()
    {
        this.buildingsByGuid.Clear();
        foreach (Node child in this.GetChildren())
        {
            if (child is Building building)
            {
                building.QueueFree();
            }
        }
        DebugLogger.Log("debug_buildings", DebugLogger.LogLevel.Info,
            () => "BuildingManager: Cleared all data");
        // Interne Listen bereinigen (entsorgte Referenzen entfernen)
        this.Buildings.Clear();
        this.Cities.Clear();
    }

    // === UI‑Hilfsfunktionen fuer GDScript ===
    // Liefert alle Gebaeude als Godot-Array (GDScript-kompatibel)

    /// <summary>
    /// Liefert alle Geb4ude als Godot-Array (UI-kompatibel).
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<Building> GetAllBuildings()
    {
        var arr = new Godot.Collections.Array<Building>();
        var snapshot = this.Buildings.ToArray();
        foreach (var b in snapshot)
        {
            if (b != null && IsInstanceValid(b) && !b.IsQueuedForDeletion())
            {
                arr.Add(b);
            }
        }
        return arr;
    }

    // Liefert Staedte als Godot-Array (wird von Panels fuer Naechste‑Stadt gesucht)

    /// <summary>
    /// Liefert alle St4dte als Godot-Array (UI-kompatibel).
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<City> GetCitiesForUI()
    {
        var arr = new Godot.Collections.Array<City>();
        var snapshot = this.Cities.ToArray();
        foreach (var c in snapshot)
        {
            if (c != null && IsInstanceValid(c) && !c.IsQueuedForDeletion())
            {
                arr.Add(c);
            }
        }
        return arr;
    }

    /// <summary>
    /// Sammelt die Gesamtmenge einer Ressource aus allen Gebäude-Inventaren und Stock-Werten.
    /// </summary>
    /// <returns></returns>
    [Obsolete]
    public int GetTotalInventoryOfResource(StringName resourceId)
    {
        if (resourceId == null || resourceId.IsEmpty)
        {
            return 0;
        }

        int total = 0;

        try
        {
            if (this.Buildings == null)
            {
                return 0;
            }

            foreach (var building in this.Buildings)
            {
                if (building == null || !IsInstanceValid(building))
                {
                    continue;
                }

                bool countedFromInventory = false;

                // Neues Inventarsystem (bevorzugt)
                if (building is IHasInventory inventoryBuilding)
                {
                    try
                    {
                        var inventory = inventoryBuilding.GetInventory();
                        if (inventory?.TryGetValue(resourceId, out var amount) == true)
                        {
                            total += (int)amount;
                            countedFromInventory = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error("debug_building", "GetInventoryException", ex.Message,
                            new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "building", building.Name } });
                    }
                }

                // Legacy-Stock nur zählen, wenn kein Inventar-Eintrag vorhanden ist
                if (!countedFromInventory && building is IHasStock stockBuilding)
                {
                    try
                    {
                        StringName mainResourceId = this.GetMainResourceIdForBuilding(building);
                        if (mainResourceId == resourceId)
                        {
                            total += stockBuilding.Stock;
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error("debug_building", "GetStockException", ex.Message,
                            new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "building", building.Name } });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error("debug_building", "GetTotalInventoryException", ex.Message,
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "resource", resourceId } });
        }

        return total;
    }

    /// <summary>
    /// Gets the main resource ID for a building (what its Stock property represents).
    /// </summary>
    private StringName GetMainResourceIdForBuilding(Building building)
    {
        return building switch
        {
            ChickenFarm => ChickenFarm.MainResourceId,  // "chickens"
            PigFarm => PigFarm.MainResourceId,          // "pig"
            GrainFarm => GrainFarm.MainResourceId,      // "grain"
            _ => new StringName(""),
        };
    }
}














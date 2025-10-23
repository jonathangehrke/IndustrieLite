// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Service für Lieferanten-Management und Transport-Routing
/// Migriert aus SupplierDataService.gd - enthält alle Geschäftslogik für Lieferanten.
/// </summary>
public partial class SupplierService : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    private BuildingManager? buildingManager;
    private TransportManager? transportManager;
    private GameDatabase? gameDatabase;
    private EventHub? eventHub;

    // Fixed supplier routes: (ConsumerBuildingId::ResourceId) -> SupplierBuildingId
    private readonly Dictionary<string, string> fixedSupplierRoutes = new(StringComparer.Ordinal);

    public override void _Ready()
    {
        // No self-registration - managed by DIContainer (Clean Architecture)
        // Dependencies are injected via Initialize() method
    }

    /// <summary>
    /// Finds all potential suppliers for a resource for a given building.
    /// </summary>
    /// <returns></returns>
    public List<SupplierInfo> FindSuppliersForResource(Building consumer, string resourceId)
    {
        if (consumer == null || this.buildingManager == null)
        {
            return new List<SupplierInfo>();
        }

        var suppliers = new List<SupplierInfo>();
        var allBuildings = this.buildingManager.Buildings;
        DebugLogger.LogServices($"SupplierService: Suche Lieferanten fuer '{resourceId}' (Consumer: {this.GetBuildingDisplayName(consumer)}), Kandidaten: {allBuildings.Count}");

        foreach (var building in allBuildings)
        {
            if (building == null || building == consumer)
            {
                continue;
            }

            // Check if building produces or has stock of the resource
            bool hasResource = false;
            int availableStock = 0;
            float productionRate = 0f;

            // Check production
            if (building is IProductionBuilding producer)
            {
                var production = producer.GetProductionForUI();
                if (production.ContainsKey(resourceId) && (float)production[resourceId] > 0)
                {
                    hasResource = true;
                    productionRate = (float)production[resourceId];
                }
            }

            // Check stock/inventory (StringName keys)
            if (building is IHasInventory inventory)
            {
                var stock = inventory.GetInventory();
                var key = new StringName(resourceId);
                if (stock.TryGetValue(key, out var amount) && amount > 0)
                {
                    hasResource = true;
                    availableStock = Mathf.FloorToInt(amount);
                }
            }

            if (!hasResource)
            {
                continue;
            }

            // Calculate distance
            float distance = this.CalculateDistance(consumer, building);

            suppliers.Add(new SupplierInfo
            {
                Building = building,
                ResourceId = resourceId,
                AvailableStock = availableStock,
                ProductionRate = productionRate,
                Distance = distance,
                DisplayName = this.GetBuildingDisplayName(building),
            });
            DebugLogger.LogServices($"SupplierService: Kandidat -> {this.GetBuildingDisplayName(building)} dist={distance:F1} stock={availableStock} prod={productionRate:F1}");
        }

        // Sort by distance (closest first)
        var ordered = suppliers.OrderBy(s => s.Distance).ToList();
        DebugLogger.LogServices($"SupplierService: Ergebnis {ordered.Count} Lieferanten fuer '{resourceId}'");
        return ordered;
    }

    /// <summary>
    /// GDScript-kompatible Variante: Liefert eine Array von Dictionaries fuer die UI.
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<Godot.Collections.Dictionary> FindSuppliersForResourceUI(Building consumer, string resourceId)
    {
        var infos = this.FindSuppliersForResource(consumer, resourceId);
        var arr = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var s in infos)
        {
            var d = new Godot.Collections.Dictionary
            {
                { "building", s.Building },
                { "name", s.DisplayName },
                { "distance", s.Distance },
                { "available", s.AvailableStock },
                { "production", s.ProductionRate },
            };
            arr.Add(d);
        }
        return arr;
    }

    /// <summary>
    /// Sets a fixed supplier route for a consumer building and resource.
    /// </summary>
    public void SetFixedSupplierRoute(Building consumer, string resourceId, Building supplier)
    {
        if (consumer == null || supplier == null)
        {
            return;
        }

        var key = MakeRouteKey(consumer, resourceId);
        var supplierId = this.GetBuildingId(supplier);

        this.fixedSupplierRoutes[key] = supplierId;
        DebugLogger.LogServices($"SupplierService: Fixed route set -> {key} = {supplierId}");

        // Start periodic supply route
        this.StartPeriodicSupplyRoute(supplier, consumer, resourceId);
    }

    /// <summary>
    /// Clears a fixed supplier route.
    /// </summary>
    public void ClearFixedSupplierRoute(Building consumer, string resourceId)
    {
        if (consumer == null)
        {
            return;
        }

        var key = MakeRouteKey(consumer, resourceId);
        this.fixedSupplierRoutes.Remove(key);

        DebugLogger.LogServices($"SupplierService: Fixed route cleared -> {key}");

        // Stop periodic supply route
        this.StopPeriodicSupplyRoute(consumer, resourceId);
    }

    /// <summary>
    /// Gets the fixed supplier for a consumer building and resource.
    /// </summary>
    /// <returns></returns>
    public Building? GetFixedSupplierRoute(Building consumer, string resourceId)
    {
        if (consumer == null || this.buildingManager == null)
        {
            return null;
        }

        var key = MakeRouteKey(consumer, resourceId);
        if (!this.fixedSupplierRoutes.TryGetValue(key, out var supplierId))
        {
            return null;
        }

        return this.FindBuildingById(supplierId);
    }

    /// <summary>
    /// Starts a periodic supply route between supplier and consumer.
    /// </summary>
    private void StartPeriodicSupplyRoute(Building supplier, Building consumer, string resourceId)
    {
        if (this.transportManager == null)
        {
            return;
        }

        // Get logistics settings from supplier
        var logisticsSettings = this.GetLogisticsSettings(supplier);
        var capacity = logisticsSettings.Capacity;
        var speed = logisticsSettings.Speed;
        var period = 5.0; // 5 seconds default

        var res = this.transportManager.TryStartPeriodicSupplyRoute(supplier, consumer, new StringName(resourceId), capacity, period, speed);
        if (!res.Ok)
        {
            var code = res.ErrorInfo?.Code ?? ErrorIds.TransportInvalidArgumentName;
            var msg = res.ErrorInfo?.Message ?? res.Error;
            DebugLogger.Warn("debug_services", "SupplierRouteStartFailed", msg,
                new System.Collections.Generic.Dictionary<string, object?>
(StringComparer.Ordinal)
                {
                    { "supplier", this.GetBuildingDisplayName(supplier) },
                    { "consumer", this.GetBuildingDisplayName(consumer) },
                    { "resource", resourceId },
                    { "code", code },
                });
            this.EmitToast($"Lieferroute konnte nicht gestartet werden: {msg}", "warn");
            return;
        }
        DebugLogger.Info("debug_services", "SupplierRouteStarted",
            $"Periodic route active {resourceId} from {this.GetBuildingDisplayName(supplier)} to {this.GetBuildingDisplayName(consumer)}",
            new System.Collections.Generic.Dictionary<string, object?>
(StringComparer.Ordinal)
            {
                { "resource", resourceId },
                { "capacity", capacity },
                { "periodSec", period },
                { "speed", speed },
            });
        this.EmitToast($"Lieferroute aktiv: {resourceId} -> {this.GetBuildingDisplayName(consumer)}", "info");
    }

    /// <summary>
    /// Stops a periodic supply route.
    /// </summary>
    private void StopPeriodicSupplyRoute(Building consumer, string resourceId)
    {
        if (this.transportManager == null)
        {
            return;
        }

        var res = this.transportManager.TryStopPeriodicSupplyRoute(consumer, new StringName(resourceId));
        if (!res.Ok)
        {
            var code = res.ErrorInfo?.Code ?? ErrorIds.TransportInvalidArgumentName;
            var msg = res.ErrorInfo?.Message ?? res.Error;
            DebugLogger.Warn("debug_services", "SupplierRouteStopFailed", msg,
                new System.Collections.Generic.Dictionary<string, object?>
(StringComparer.Ordinal)
                {
                    { "consumer", this.GetBuildingDisplayName(consumer) },
                    { "resource", resourceId },
                    { "code", code },
                });
            this.EmitToast($"Lieferroute konnte nicht gestoppt werden: {msg}", "warn");
            return;
        }
        DebugLogger.Info("debug_services", "SupplierRouteStopped",
            $"Periodic route stopped {resourceId} for {this.GetBuildingDisplayName(consumer)}",
            new System.Collections.Generic.Dictionary<string, object?>
(StringComparer.Ordinal)
            {
                { "resource", resourceId },
            });
        this.EmitToast($"Lieferroute gestoppt: {resourceId} -> {this.GetBuildingDisplayName(consumer)}", "info");
    }

    private void EmitToast(string message, string level)
    {
        try
        {
            this.eventHub?.EmitSignal(EventHub.SignalName.ToastRequested, message, level);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Gets logistics settings for a building.
    /// </summary>
    /// <returns></returns>
    public LogisticsSettings GetLogisticsSettings(Building building)
    {
        if (building == null)
        {
            return new LogisticsSettings { Capacity = 5, Speed = 32.0f };
        }

        var capacity = 5; // default
        var speed = 32.0f; // default

        // Try to get from building properties
        try
        {
            capacity = building.LogisticsTruckCapacity;
            speed = building.LogisticsTruckSpeed;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"SupplierService: Error reading logistics settings from {this.GetBuildingDisplayName(building)}: {ex.Message}");
        }

        return new LogisticsSettings { Capacity = capacity, Speed = speed };
    }

    /// <summary>
    /// Calculates distance between two buildings in tile units.
    /// </summary>
    private float CalculateDistance(Building from, Building to)
    {
        if (from == null || to == null)
        {
            return float.MaxValue;
        }

        var tileSize = this.buildingManager?.TileSize ?? 32;
        var fromCenter = this.CalculateBuildingCenter(from, tileSize);
        var toCenter = this.CalculateBuildingCenter(to, tileSize);

        return fromCenter.DistanceTo(toCenter) / tileSize;
    }

    /// <summary>
    /// Calculates the center point of a building.
    /// </summary>
    private Vector2 CalculateBuildingCenter(Building building, int tileSize)
    {
        var basePos = building.GlobalPosition;
        var sizeOffset = new Vector2(building.Size.X * tileSize, building.Size.Y * tileSize) * 0.5f;
        return basePos + sizeOffset;
    }

    /// <summary>
    /// Gets a display name for a building, including stable instance numbering (e.g., "Bauernhof #1").
    /// </summary>
    private string GetBuildingDisplayName(Building? building)
    {
        if (building == null)
        {
            return "Unknown";
        }

        string baseName = string.Empty;

        // 1) Prefer BuildingDef from the building's own Database reference
        try
        {
            var def = building.GetBuildingDef();
            if (def != null && !string.IsNullOrEmpty(def.DisplayName))
            {
                baseName = def.DisplayName;
            }
        }
        catch
        {
        }

        // 2) Fallback: GameDatabase (if available)
        if (string.IsNullOrEmpty(baseName) && this.gameDatabase != null && this.gameDatabase.Buildings != null)
        {
            try
            {
                var id = !string.IsNullOrEmpty(building.DefinitionId)
                    ? building.DefinitionId.ToLower()
                    : building.GetType().Name.ToLower();
                var def = this.gameDatabase.Buildings.GetAll().FirstOrDefault(b => string.Equals(b.Id?.ToLower() ?? "", id, StringComparison.Ordinal));
                if (def != null && !string.IsNullOrEmpty(def.DisplayName))
                {
                    baseName = def.DisplayName;
                }
            }
            catch
            {
            }
        }

        // 3) Fallback: Node name or type name
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = !string.IsNullOrEmpty(building.Name) ? building.Name : building.GetType().Name;
        }

        // Append stable instance number among same-type buildings (based on DefinitionId or type)
        var number = this.GetBuildingOrdinal(building);
        if (number > 0)
        {
            return $"{baseName} #{number}";
        }

        return baseName;
    }

    private int GetBuildingOrdinal(Building building)
    {
        try
        {
            if (this.buildingManager == null)
            {
                return 0;
            }

            string defId = !string.IsNullOrEmpty(building.DefinitionId)
                ? building.DefinitionId
                : building.GetType().Name.ToLower();

            var sameType = new List<Building>();
            foreach (var b in this.buildingManager.Buildings)
            {
                if (b == null)
                {
                    continue;
                }

                var otherId = !string.IsNullOrEmpty(b.DefinitionId) ? b.DefinitionId : b.GetType().Name.ToLower();
                if (string.Equals(otherId, defId, StringComparison.Ordinal))
                {
                    sameType.Add(b);
                }
            }
            for (int i = 0; i < sameType.Count; i++)
            {
                if (ReferenceEquals(sameType[i], building))
                {
                    return i + 1;
                }
            }
            return sameType.Count > 0 ? sameType.Count : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets a unique ID for a building.
    /// </summary>
    private string GetBuildingId(Building building)
    {
        return !string.IsNullOrEmpty(building.BuildingId)
            ? building.BuildingId
            : building.GetInstanceId().ToString();
    }

    /// <summary>
    /// Finds a building by its ID.
    /// </summary>
    private Building? FindBuildingById(string buildingId)
    {
        if (this.buildingManager == null)
        {
            return null;
        }

        foreach (var building in this.buildingManager.Buildings)
        {
            var id = this.GetBuildingId(building);
            if (string.Equals(id, buildingId, StringComparison.Ordinal))
            {
                return building;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a route key for fixed supplier routes.
    /// </summary>
    private static string MakeRouteKey(Building consumer, string resourceId)
    {
        var consumerId = !string.IsNullOrEmpty(consumer.BuildingId)
            ? consumer.BuildingId
            : consumer.GetInstanceId().ToString();
        return $"{consumerId}::{resourceId}";
    }

    // === Save/Load API for Supplier Routes ===

    /// <summary>
    /// Exports all fixed supplier routes for persistence
    /// Returns list of (ConsumerBuildingId, ResourceId, SupplierBuildingId) tuples.
    /// </summary>
    /// <returns></returns>
    public List<(string ConsumerBuildingId, string ResourceId, string SupplierBuildingId)> ExportFixedRoutes()
    {
        var routes = new List<(string ConsumerBuildingId, string ResourceId, string SupplierBuildingId)>();
        foreach (var kvp in this.fixedSupplierRoutes)
        {
            // Key format: "ConsumerBuildingId::ResourceId"
            var parts = kvp.Key.Split(new[] { "::" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                routes.Add((ConsumerBuildingId: parts[0], ResourceId: parts[1], SupplierBuildingId: kvp.Value));
            }
        }
        return routes;
    }

    /// <summary>
    /// Imports fixed supplier routes from persistence and restarts them.
    /// </summary>
    public void ImportFixedRoutes(List<(string ConsumerBuildingId, string ResourceId, string SupplierBuildingId)> routes)
    {
        if (routes == null || this.buildingManager == null)
        {
            return;
        }

        int imported = 0;
        int failed = 0;

        foreach (var route in routes)
        {
            var consumer = this.FindBuildingById(route.ConsumerBuildingId);
            var supplier = this.FindBuildingById(route.SupplierBuildingId);

            if (consumer != null && supplier != null)
            {
                this.SetFixedSupplierRoute(consumer, route.ResourceId, supplier);
                imported++;
                DebugLogger.LogServices($"SupplierService: Restored route {route.ResourceId}: {this.GetBuildingDisplayName(supplier)} -> {this.GetBuildingDisplayName(consumer)}");
            }
            else
            {
                failed++;
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Warn, () =>
                    $"SupplierService: Failed to restore route - Consumer={route.ConsumerBuildingId} (found={consumer != null}), Supplier={route.SupplierBuildingId} (found={supplier != null})");
            }
        }

        if (imported > 0)
        {
            DebugLogger.LogLifecycle(() => $"SupplierService: Restored {imported} supplier routes ({failed} failed)");
        }
    }

    /// <summary>
    /// Clears all fixed supplier routes (for lifecycle management).
    /// </summary>
    public void ClearAllRoutes()
    {
        this.fixedSupplierRoutes.Clear();
        DebugLogger.LogServices("SupplierService: Cleared all fixed routes");
    }
}

/// <summary>
/// Information about a supplier for a specific resource.
/// </summary>
public class SupplierInfo
{
    public Building Building { get; set; } = default!;

    public string ResourceId { get; set; } = string.Empty;

    public int AvailableStock { get; set; }

    public float ProductionRate { get; set; }

    public float Distance { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Logistics settings for buildings.
/// </summary>
public class LogisticsSettings
{
    public int Capacity { get; set; } = 5;

    public float Speed { get; set; } = 32.0f;
}

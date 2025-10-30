// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using IndustrieLite.Transport.Core;

/// <summary>
/// TransportManager - Compatibility Wrapper for the new Transport Architecture
///
/// This class maintains backwards compatibility for all existing code that depends on TransportManager.
/// The actual implementation is delegated to TransportCoordinator and its sub-services.
///
/// Architecture:
/// - TransportManager (this wrapper) -> TransportCoordinator -> TruckManager, TransportOrderManager, TransportEconomyService.
/// </summary>
public partial class TransportManager : Node, ITransportManager, ITickable, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    private TransportCoordinator coordinator = default!;
    private BuildingManager? buildingManager; // Injected via Initialize (breaks circular dependency)

    // Zwischenspeicher fuer Export-Werte (ohne NodePath)
    private bool exportSignaleAktiv = true;
    private double exportKostenProKachel = 0.05;
    private double exportTruckFixkosten = 1.0;
    private double exportStandardpreis = 5.0;
    private int exportMaxMengeProTruck = 20;

    [Export]
    public bool SignaleAktiv
    {
        get => this.exportSignaleAktiv;
        set
        {
            this.exportSignaleAktiv = value;
            if (this.coordinator != null)
            {
                this.coordinator.SignaleAktiv = value;
            }
        }
    }

    [Export]
    public double CostPerUnitPerTile
    {
        get => this.exportKostenProKachel;
        set
        {
            this.exportKostenProKachel = value;
            if (this.coordinator != null)
            {
                this.coordinator.CostPerUnitPerTile = value;
            }
        }
    }

    [Export]
    public double TruckFixedCost
    {
        get => this.exportTruckFixkosten;
        set
        {
            this.exportTruckFixkosten = value;
            if (this.coordinator != null)
            {
                this.coordinator.TruckFixedCost = value;
            }
        }
    }

    [Export]
    public double DefaultPricePerUnit
    {
        get => this.exportStandardpreis;
        set
        {
            this.exportStandardpreis = value;
            if (this.coordinator != null)
            {
                this.coordinator.DefaultPricePerUnit = value;
            }
        }
    }

    [Export]
    public int MaxMengeProTruck
    {
        get => this.exportMaxMengeProTruck;
        set
        {
            this.exportMaxMengeProTruck = value;
            if (this.coordinator != null)
            {
                this.coordinator.MaxMengeProTruck = value;
            }
        }
    }

    // Legacy-API Properties (for SaveLoadService, UIService, etc.)
    public TransportCoreService? TransportCore => this.coordinator?.TransportCore;

    public List<Truck> Trucks => this.coordinator?.TruckManager?.Trucks ?? new List<Truck>();

    // Legacy-API Methods (for UI, Input, etc.)
    public void AcceptOrder(int id) => this.coordinator?.AcceptOrder(id);

    public Result TryAcceptOrder(int id, string? correlationId = null) => this.coordinator?.TryAcceptOrder(id, correlationId) ?? Result.Fail(new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Coordinator fehlt"));

    public void HandleTransportClick(Vector2I cell) => this.coordinator?.HandleTransportClick(cell);

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders() => this.coordinator?.GetOrders() ?? new Godot.Collections.Array<Godot.Collections.Dictionary>();

    public void StartManualTransport(Building source, Building target) => this.coordinator?.StartManualTransport(source, target);

    public Result TryStartManualTransport(Building source, Building target, string? correlationId = null)
        => this.coordinator?.TryStartManualTransport(source, target, correlationId) ?? Result.Fail(new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Coordinator fehlt"));

    public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec)
        => this.coordinator?.StartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec);

    public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed)
        => this.coordinator?.StartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed);

    public Result TryStartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f, string? correlationId = null)
        => this.coordinator?.TryStartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed, correlationId) ?? Result.Fail(new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Coordinator fehlt"));

    public void StopPeriodicSupplyRoute(Building consumer, StringName resourceId)
        => this.coordinator?.StopPeriodicSupplyRoute(consumer, resourceId);

    public Result TryStopPeriodicSupplyRoute(Building consumer, StringName resourceId, string? correlationId = null)
        => this.coordinator?.TryStopPeriodicSupplyRoute(consumer, resourceId, correlationId) ?? Result.Fail(new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Coordinator fehlt"));

    public void TruckArrived(Truck t) => this.coordinator?.TruckArrived(t);

    public void RestartPendingJobs() => this.coordinator?.RestartPendingJobs();

    public void RepathAllTrucks() => this.coordinator?.RepathAllTrucks();

    public void CancelOrdersFor(Node2D node) => this.coordinator?.CancelOrdersFor(node);

    // ITickable

    /// <inheritdoc/>
    string ITickable.Name => "TransportManager";

    public new string Name => "TransportManager";

    /// <inheritdoc/>
    public void Tick(double dt) => this.coordinator?.Tick(dt);

    /// <inheritdoc/>
    public override void _Ready()
    {
        this.coordinator = new TransportCoordinator();
        // Transfer Export-Properties BEFORE AddChild (damit _Ready Werte sieht)
        this.coordinator.SignaleAktiv = this.exportSignaleAktiv;
        this.coordinator.CostPerUnitPerTile = this.exportKostenProKachel;
        this.coordinator.TruckFixedCost = this.exportTruckFixkosten;
        this.coordinator.DefaultPricePerUnit = this.exportStandardpreis;
        this.coordinator.MaxMengeProTruck = this.exportMaxMengeProTruck;

        // ISceneGraph must be injected via Initialize - no direct Godot coupling allowed
        if (this.pendingSceneGraph == null)
        {
            throw new System.InvalidOperationException("ISceneGraph fehlt - DI-Konfiguration prüfen (Initialize muss vor _Ready aufgerufen werden)");
        }

        this.pendingSceneGraph.AddChild(this.coordinator);

        // Initialize coordinator with pending dependencies (if Initialize() was called before _Ready)
        this.InitializeCoordinatorIfPending();

        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(TransportManager), this);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_transport", "RegisterWithServiceContainerFailed", ex.Message);
            }
        }

        DebugLogger.LogTransport("TransportManager: Initialized as compatibility wrapper");
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        // Cleanup is managed by the Coordinator
        base._ExitTree();
    }

    // Additional Legacy Methods that might be called by other systems

    /// <summary>
    /// Legacy method for getting current market price.
    /// </summary>
    /// <returns></returns>
    public double GetCurrentMarketPrice(string product, City city)
    {
        return this.coordinator?.EconomyService?.GetCurrentMarketPrice(product, city) ?? 5.0;
    }

    /// <summary>
    /// Legacy method for updating order book.
    /// </summary>
    public void UpdateOrderBookFromCities()
    {
        this.coordinator?.OrderManager?.UpdateOrderBookFromCities();
    }

    /// <summary>
    /// Legacy method for updating supply index.
    /// </summary>
    public void UpdateSupplyIndexFromBuildings()
    {
        this.coordinator?.OrderManager?.UpdateSupplyIndexFromBuildings();
    }

    /// <summary>
    /// Legacy method - check if transport manager is ready.
    /// </summary>
    /// <returns></returns>
    public bool IsReady()
    {
        return this.coordinator != null && this.coordinator.TransportCore != null;
    }

    /// <summary>
    /// Legacy method - get transport core service (used by SaveLoadService).
    /// </summary>
    /// <returns></returns>
    public TransportCoreService? GetTransportCore()
    {
        return this.coordinator?.TransportCore;
    }

    /// <summary>
    /// Legacy method - access to truck manager.
    /// </summary>
    /// <returns></returns>
    public List<Truck> GetTrucks()
    {
        return this.coordinator?.TruckManager?.Trucks ?? new List<Truck>();
    }

    /// <summary>
    /// Gets legacy property access for old code.
    /// </summary>
    public Fleet? Fleet => this.coordinator?.TruckManager != null ? this.GetPrivateField<Fleet>(this.coordinator.TruckManager, "fleet") : null;

    private T? GetPrivateField<T>(object obj, string fieldName)
        where T : class
    {
        try
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(obj) as T;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears all transport data - for lifecycle management.
    /// </summary>
    public void ClearAllData()
    {
        this.coordinator?.ClearAllData();
    }

    // === Feste Lieferantenrouten (UI-Unterstützung) ===
    // Einfache Zuordnung: (BuildingId::ResourceId) -> Supplier-BuildingId
    private readonly System.Collections.Generic.Dictionary<string, string> fixedSupplierByKey = new(StringComparer.Ordinal);

    private static string MakeRouteKey(Building? consumer, string resourceId)
    {
        var cId = consumer != null && !string.IsNullOrEmpty(consumer.BuildingId) ? consumer.BuildingId : consumer?.GetInstanceId().ToString() ?? "";
        return $"{cId}::{resourceId}";
    }

    public void SetFixedSupplierRoute(Node consumer, string resourceId, Node supplier)
    {
        var c = consumer as Building;
        var s = supplier as Building;
        if (c == null || s == null)
        {
            return;
        }

        var key = MakeRouteKey(c, resourceId);
        var supId = !string.IsNullOrEmpty(s.BuildingId) ? s.BuildingId : s.GetInstanceId().ToString();
        this.fixedSupplierByKey[key] = supId;
        DebugLogger.LogTransport(() => $"TransportManager: Fixed supplier set -> {key} = {supId}");
    }

    public void ClearFixedSupplierRoute(Node consumer, string resourceId)
    {
        var c = consumer as Building;
        if (c == null)
        {
            return;
        }

        var key = MakeRouteKey(c, resourceId);
        this.fixedSupplierByKey.Remove(key);
        DebugLogger.LogTransport(() => $"TransportManager: Fixed supplier cleared -> {key}");
    }

    private BuildingManager? ResolveBuildingManager()
    {
        // Use injected field instead of ServiceContainer lookup (breaks circular dependency)
        return this.buildingManager;
    }

    public Building? GetFixedSupplierRoute(Node consumer, string resourceId)
    {
        var c = consumer as Building;
        if (c == null)
        {
            return null;
        }

        var key = MakeRouteKey(c, resourceId);
        if (!this.fixedSupplierByKey.TryGetValue(key, out var supId))
        {
            return null;
        }

        var bm = this.ResolveBuildingManager();
        if (bm == null)
        {
            return null;
        }

        foreach (var b in bm.Buildings)
        {
            if (!string.IsNullOrEmpty(b.BuildingId) && string.Equals(b.BuildingId, supId, StringComparison.Ordinal))
            {
                return b;
            }
        }
        return null;
    }
}


// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IndustrieLite.Transport.Core;


/// <summary>
/// TransportManager - Compatibility Wrapper for the new Transport Architecture
///
/// This class maintains backwards compatibility for all existing code that depends on TransportManager.
/// The actual implementation is delegated to TransportCoordinator and its sub-services.
///
/// Architecture:
/// - TransportManager (this wrapper) -> TransportCoordinator -> TruckManager, TransportOrderManager, TransportEconomyService
/// </summary>
public partial class TransportManager : Node, ITickable, ILifecycleScope
{
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
        get => exportSignaleAktiv;
        set
        {
            exportSignaleAktiv = value;
            if (coordinator != null) coordinator.SignaleAktiv = value;
        }
    }

    [Export]
    public double CostPerUnitPerTile
    {
        get => exportKostenProKachel;
        set
        {
            exportKostenProKachel = value;
            if (coordinator != null)
                coordinator.CostPerUnitPerTile = value;
        }
    }

    [Export]
    public double TruckFixedCost
    {
        get => exportTruckFixkosten;
        set
        {
            exportTruckFixkosten = value;
            if (coordinator != null)
                coordinator.TruckFixedCost = value;
        }
    }

    [Export]
    public double DefaultPricePerUnit
    {
        get => exportStandardpreis;
        set
        {
            exportStandardpreis = value;
            if (coordinator != null)
                coordinator.DefaultPricePerUnit = value;
        }
    }

    [Export]
    public int MaxMengeProTruck
    {
        get => exportMaxMengeProTruck;
        set
        {
            exportMaxMengeProTruck = value;
            if (coordinator != null)
                coordinator.MaxMengeProTruck = value;
        }
    }

    // Legacy-API Properties (for SaveLoadService, UIService, etc.)
    public TransportCoreService? TransportCore => coordinator?.TransportCore;
    public List<Truck> Trucks => coordinator?.TruckManager?.Trucks ?? new List<Truck>();

    // Legacy-API Methods (for UI, Input, etc.)
    public void AcceptOrder(int id) => coordinator?.AcceptOrder(id);
    public Result TryAcceptOrder(int id, string? correlationId = null) => coordinator?.TryAcceptOrder(id, correlationId) ?? Result.Fail(new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Coordinator fehlt"));
    public void HandleTransportClick(Vector2I cell) => coordinator?.HandleTransportClick(cell);
    public Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders() => coordinator?.GetOrders() ?? new Godot.Collections.Array<Godot.Collections.Dictionary>();
    public void StartManualTransport(Building source, Building target) => coordinator?.StartManualTransport(source, target);
    public Result TryStartManualTransport(Building source, Building target, string? correlationId = null)
        => coordinator?.TryStartManualTransport(source, target, correlationId) ?? Result.Fail(new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Coordinator fehlt"));
    public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec)
        => coordinator?.StartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec);
    public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed)
        => coordinator?.StartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed);
    public Result TryStartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f, string? correlationId = null)
        => coordinator?.TryStartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed, correlationId) ?? Result.Fail(new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Coordinator fehlt"));
    public void StopPeriodicSupplyRoute(Building consumer, StringName resourceId)
        => coordinator?.StopPeriodicSupplyRoute(consumer, resourceId);
    public Result TryStopPeriodicSupplyRoute(Building consumer, StringName resourceId, string? correlationId = null)
        => coordinator?.TryStopPeriodicSupplyRoute(consumer, resourceId, correlationId) ?? Result.Fail(new ErrorInfo(ErrorIds.TransportServiceUnavailableName, "Coordinator fehlt"));
    public void TruckArrived(Truck t) => coordinator?.TruckArrived(t);
    public void RestartPendingJobs() => coordinator?.RestartPendingJobs();
    public void RepathAllTrucks() => coordinator?.RepathAllTrucks();
    public void CancelOrdersFor(Node2D node) => coordinator?.CancelOrdersFor(node);

    // ITickable
    string ITickable.Name => "TransportManager";
    public new string Name => "TransportManager";
    public void Tick(double dt) => coordinator?.Tick(dt);

    public override void _Ready()
    {
        coordinator = new TransportCoordinator();
        // Transfer Export-Properties BEFORE AddChild (damit _Ready Werte sieht)
        coordinator.SignaleAktiv = exportSignaleAktiv;
        coordinator.CostPerUnitPerTile = exportKostenProKachel;
        coordinator.TruckFixedCost = exportTruckFixkosten;
        coordinator.DefaultPricePerUnit = exportStandardpreis;
        coordinator.MaxMengeProTruck = exportMaxMengeProTruck;

        AddChild(coordinator);

        // Initialize coordinator with pending dependencies (if Initialize() was called before _Ready)
        InitializeCoordinatorIfPending();

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

    public override void _ExitTree()
    {
        // Cleanup is managed by the Coordinator
        base._ExitTree();
    }

    // Additional Legacy Methods that might be called by other systems

    /// <summary>
    /// Legacy method for getting current market price
    /// </summary>
    public double GetCurrentMarketPrice(string product, City city)
    {
        return coordinator?.EconomyService?.GetCurrentMarketPrice(product, city) ?? 5.0;
    }

    /// <summary>
    /// Legacy method for updating order book
    /// </summary>
    public void UpdateOrderBookFromCities()
    {
        coordinator?.OrderManager?.UpdateOrderBookFromCities();
    }

    /// <summary>
    /// Legacy method for updating supply index
    /// </summary>
    public void UpdateSupplyIndexFromBuildings()
    {
        coordinator?.OrderManager?.UpdateSupplyIndexFromBuildings();
    }

    /// <summary>
    /// Legacy method - check if transport manager is ready
    /// </summary>
    public bool IsReady()
    {
        return coordinator != null && coordinator.TransportCore != null;
    }

    /// <summary>
    /// Legacy method - get transport core service (used by SaveLoadService)
    /// </summary>
    public TransportCoreService? GetTransportCore()
    {
        return coordinator?.TransportCore;
    }

    /// <summary>
    /// Legacy method - access to truck manager
    /// </summary>
    public List<Truck> GetTrucks()
    {
        return coordinator?.TruckManager?.Trucks ?? new List<Truck>();
    }

    /// <summary>
    /// Legacy property access for old code
    /// </summary>
    public Fleet? Fleet => coordinator?.TruckManager != null ? GetPrivateField<Fleet>(coordinator.TruckManager, "fleet") : null;

    private T? GetPrivateField<T>(object obj, string fieldName) where T : class
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
    /// Clears all transport data - for lifecycle management
    /// </summary>
    public void ClearAllData()
    {
        coordinator?.ClearAllData();
    }

    // === Feste Lieferantenrouten (UI-Unterstützung) ===
    // Einfache Zuordnung: (BuildingId::ResourceId) -> Supplier-BuildingId
    private readonly System.Collections.Generic.Dictionary<string, string> _fixedSupplierByKey = new();

    private static string MakeRouteKey(Building? consumer, string resourceId)
    {
        var cId = consumer != null && !string.IsNullOrEmpty(consumer.BuildingId) ? consumer.BuildingId : consumer?.GetInstanceId().ToString() ?? "";
        return $"{cId}::{resourceId}";
    }

    public void SetFixedSupplierRoute(Node consumer, string resourceId, Node supplier)
    {
        var c = consumer as Building;
        var s = supplier as Building;
        if (c == null || s == null) return;
        var key = MakeRouteKey(c, resourceId);
        var supId = !string.IsNullOrEmpty(s.BuildingId) ? s.BuildingId : s.GetInstanceId().ToString();
        _fixedSupplierByKey[key] = supId;
        DebugLogger.LogTransport(() => $"TransportManager: Fixed supplier set -> {key} = {supId}");
    }

    public void ClearFixedSupplierRoute(Node consumer, string resourceId)
    {
        var c = consumer as Building;
        if (c == null) return;
        var key = MakeRouteKey(c, resourceId);
        _fixedSupplierByKey.Remove(key);
        DebugLogger.LogTransport(() => $"TransportManager: Fixed supplier cleared -> {key}");
    }

    private BuildingManager? ResolveBuildingManager()
    {
        // Use injected field instead of ServiceContainer lookup (breaks circular dependency)
        return buildingManager;
    }

    public Building? GetFixedSupplierRoute(Node consumer, string resourceId)
    {
        var c = consumer as Building;
        if (c == null) return null;
        var key = MakeRouteKey(c, resourceId);
        if (!_fixedSupplierByKey.TryGetValue(key, out var supId))
            return null;
        var bm = ResolveBuildingManager();
        if (bm == null) return null;
        foreach (var b in bm.Buildings)
        {
            if (!string.IsNullOrEmpty(b.BuildingId) && b.BuildingId == supId)
                return b;
        }
        return null;
    }
}


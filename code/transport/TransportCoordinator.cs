// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using IndustrieLite.Transport.Core;
using IndustrieLite.Transport.Core.Interfaces;
using IndustrieLite.Transport.Core.Models;
using IndustrieLite.Transport.Interfaces;
using CoreEventInterface = IndustrieLite.Transport.Core.Interfaces.ITransportEventService;
using CoreJobManagerInterface = IndustrieLite.Transport.Core.Interfaces.ITransportJobManager;
using CoreOrderManagerInterface = IndustrieLite.Transport.Core.Interfaces.ITransportOrderManager;
using CorePersistenceInterface = IndustrieLite.Transport.Core.Interfaces.ITransportPersistenceService;
using CorePlanningInterface = IndustrieLite.Transport.Core.Interfaces.ITransportPlanningService;
using CoreSupplyInterface = IndustrieLite.Transport.Core.Interfaces.ITransportSupplyService;
using EventServiceCore = IndustrieLite.Transport.Core.Services.TransportEventService;
using JobService = IndustrieLite.Transport.Core.Services.TransportJobManager;
using OrderService = IndustrieLite.Transport.Core.Services.TransportOrderManager;
using PersistenceServiceCore = IndustrieLite.Transport.Core.Services.TransportPersistenceService;
using PlanningServiceCore = IndustrieLite.Transport.Core.Services.TransportPlanningService;
using SupplyServiceCore = IndustrieLite.Transport.Core.Services.TransportSupplyService;
using TransportJob = IndustrieLite.Transport.Core.Models.TransportJob;

public partial class TransportCoordinator : Node, ITickable
{
    // SC-only: keine NodePath-Exports mehr

    /// <summary>
    /// Gets or sets a value indicating whether aktiviert/Deaktiviert Transport-bezogene UI-Signale.
    /// </summary>
    [Export]
    public bool SignaleAktiv { get; set; } = true;

    /// <summary>
    /// Gets or sets kosten pro Einheit und Tile für Transportkostenberechnung.
    /// </summary>
    [Export]
    public double CostPerUnitPerTile { get; set; } = GameConstants.Transport.CostPerUnitPerTile;

    /// <summary>
    /// Gets or sets fixkosten pro Truck-Fahrt.
    /// </summary>
    [Export]
    public double TruckFixedCost { get; set; } = GameConstants.Transport.TruckFixedCost;

    /// <summary>
    /// Gets or sets standard-Verkaufspreis pro Einheit (Fallback).
    /// </summary>
    [Export]
    public double DefaultPricePerUnit { get; set; } = GameConstants.Transport.DefaultPricePerUnit;

    /// <summary>
    /// Gets or sets maximal transportierte Menge pro Truck (Fallback ohne Gebäude-Upgrade).
    /// </summary>
    [Export]
    public int MaxMengeProTruck { get; set; } = GameConstants.Transport.DefaultMaxPerTruck;

    // Core Services (Master-Ownership)
    private TransportCoreService? transportCore;
    private Router? router;
    private Fleet? fleet;

    // Sub-Services
    private TruckManager truckManager = default!;
    private TransportOrderManager orderManager = default!;
    private TransportEconomyService economyService = default!;

    // External Dependencies
    private RoadManager? roadManager;
    private BuildingManager buildingManager = default!;
    private EconomyManager economyManager = default!;
    private GameManager gameManager = default!;
    private EventHub? eventHub;
    private GameTimeManager? gameTimeManager;
    private readonly AboVerwalter abos = new();

    // Public Properties for Wrapper access

    /// <summary>Gets core-Service-Aggregat für Transport.</summary>
    public TransportCoreService? TransportCore => this.transportCore;

    /// <summary>Gets truck-Manager Instanz.</summary>
    public TruckManager TruckManager => this.truckManager;

    /// <summary>Gets order-Manager Instanz.</summary>
    public TransportOrderManager OrderManager => this.orderManager;

    /// <summary>Gets economy-Service für Transportkosten/Nettoerlöse.</summary>
    public TransportEconomyService EconomyService => this.economyService;

    // ITickable

    /// <inheritdoc/>
    string ITickable.Name => "TransportCoordinator";

    /// <summary>
    /// Gets anzeigename des Knotens.
    /// </summary>
    public new string Name => "TransportCoordinator";

    // Partial method für DI-Architektur (implementiert in .Initialize.cs)
    partial void OnReady_LegacyCheck();

    /// <inheritdoc/>
    public override async void _Ready()
    {
        // 1. External Dependencies - Neue Architektur via Initialize() oder Legacy Fallback
        this.OnReady_LegacyCheck();

        // 2. Core Services creation (Master-Instances)
        var eh = this.eventHub; // EventHub should be injected via Initialize()
        if (eh == null)
        {
            DebugLogger.LogTransport("TransportCoordinator: EventHub not yet available, will be set via Initialize()");
        }
        if (this.roadManager != null && eh != null)
        {
            this.router = new Router(this.roadManager, eh);
        }

        var schedulerCore = new Scheduler();
        var jobService = new JobService();
        var orderServiceCore = new OrderService();
        var supplyServiceCore = new SupplyServiceCore();
        var planningServiceCore = new PlanningServiceCore(schedulerCore, this.router, orderServiceCore, jobService, supplyServiceCore);
        var persistenceServiceCore = new PersistenceServiceCore();
        var eventServiceCore = new EventServiceCore();
        this.transportCore = new TransportCoreService(jobService, orderServiceCore, planningServiceCore, supplyServiceCore, persistenceServiceCore, eventServiceCore);

        // Core services are managed internally by TransportCoordinator
        // No longer registering with ServiceContainer (Clean Architecture)
        this.fleet = new Fleet();
        this.fleet.Name = "Fleet";
        this.AddChild(this.fleet);

        // 3. Economy Service creation
        this.economyService = new TransportEconomyService();
        if (this.economyManager != null && this.buildingManager != null)
        {
            this.economyService.Initialize(this.economyManager, this.eventHub, this.buildingManager, this.CostPerUnitPerTile, this.TruckFixedCost, this.DefaultPricePerUnit);
        }
        else
        {
            DebugLogger.LogTransport($"TransportCoordinator: EconomyService waiting for dependencies - economyManager={this.economyManager != null}, buildingManager={this.buildingManager != null}");
        }
        // Set up delegates
        this.economyService.GetSignaleAktivDelegate = () => this.SignaleAktiv;
        this.economyService.SetJobsNeuZuPlanenDelegate = () => this.orderManager?.MarkJobsForReplanning();
        this.economyService.GetTransportCoreDelegate = () => this.transportCore;
        this.AddChild(this.economyService);

        // 4. Truck Manager creation
        this.truckManager = new TruckManager();
        if (this.buildingManager != null && this.gameManager != null)
        {
            this.truckManager.Initialize(this.fleet, this.roadManager, this.buildingManager, this.gameManager, this.MaxMengeProTruck);
        }
        else
        {
            DebugLogger.LogTransport($"TransportCoordinator: TruckManager waiting for dependencies - BuildingManager={this.buildingManager != null}, GameManager={this.gameManager != null}");
        }
        // Set up delegates for cost calculation
        this.truckManager.CalculateCostDelegate = (start, target, amount) => this.economyService.CalculateTransportCost(start, target, amount);
        this.truckManager.CalculateCenterDelegate = (building) => this.CalculateCenter(building);
        this.AddChild(this.truckManager);

        // 5. Order Manager creation
        this.orderManager = new TransportOrderManager();
        if (this.buildingManager != null)
        {
            this.orderManager.Initialize(this.transportCore, this.buildingManager, this.truckManager, this.economyService, this, this.gameTimeManager);
            DebugLogger.LogTransport("TransportCoordinator: OrderManager initialized");
        }
        else
        {
            DebugLogger.LogTransport("TransportCoordinator: OrderManager waiting for BuildingManager dependency");
        }
        this.AddChild(this.orderManager);

        // 6. Event Subscriptions via AboVerwalter (sauberes Aufräumen)
        this.SubscribeToEvents();

        DebugLogger.LogTransport(() => $"TransportCoordinator: Initialization complete - EventHub={(this.eventHub != null ? "OK" : "NULL")}");

        // 7. Simulation Registration
        await this.RegistriereBeiSimulationAsync();

        // 8. Initial Data Updates
        try
        {
            this.orderManager.UpdateOrderBookFromCities();
        }
        catch
        {
        }
        try
        {
            this.orderManager.UpdateSupplyIndexFromBuildings();
        }
        catch
        {
        }
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        this.UnsubscribeFromEvents();
        try
        {
            this.transportCore?.Dispose();
        }
        catch
        {
        }
        try
        {
            this.router?.Dispose();
        }
        catch
        {
        }
        this.router = null;
        this.eventHub = null;
        base._ExitTree();
    }

    private void SubscribeToEvents()
    {
        // EventHub-Events über AboVerwalter abonnieren
        if (this.eventHub != null)
        {
            this.abos.Abonniere(
                () => this.eventHub.RoadGraphChanged += this.OnRoadGraphChanged,
                () =>
                {
                    try
                    {
                        this.eventHub.RoadGraphChanged -= this.OnRoadGraphChanged;
                    }
                    catch
                    {
                    }
                });
            this.abos.Abonniere(
                () => this.eventHub.BuildingDestroyed += this.OnBuildingDestroyed,
                () =>
                {
                    try
                    {
                        this.eventHub.BuildingDestroyed -= this.OnBuildingDestroyed;
                    }
                    catch
                    {
                    }
                });
            this.abos.Abonniere(
                () => this.eventHub.MarketOrdersChanged += this.OnOrdersChanged,
                () =>
                {
                    try
                    {
                        this.eventHub.MarketOrdersChanged -= this.OnOrdersChanged;
                    }
                    catch
                    {
                    }
                });
            this.abos.Abonniere(
                () => this.eventHub.OrdersChanged += this.OnOrdersChanged,
                () =>
                {
                    try
                    {
                        this.eventHub.OrdersChanged -= this.OnOrdersChanged;
                    }
                    catch
                    {
                    }
                });
            DebugLogger.LogTransport("TransportCoordinator: EventHub via AboVerwalter verbunden");
        }
        else
        {
            // Kein Hard-Error: EventHub wird in Initialize() injiziert und danach abonniert
            DebugLogger.LogTransport("TransportCoordinator: EventHub noch nicht verfügbar; abonniere bei Initialize()");
        }

        // TransportCore-Events ebenfalls über AboVerwalter
        if (this.transportCore != null)
        {
            this.abos.Abonniere(
                () => this.transportCore.JobGeplant += this.OnCoreJobGeplant,
                () =>
                {
                    try
                    {
                        this.transportCore.JobGeplant -= this.OnCoreJobGeplant;
                    }
                    catch
                    {
                    }
                });
            this.abos.Abonniere(
                () => this.transportCore.JobGestartet += this.OnCoreJobGestartet,
                () =>
                {
                    try
                    {
                        this.transportCore.JobGestartet -= this.OnCoreJobGestartet;
                    }
                    catch
                    {
                    }
                });
            this.abos.Abonniere(
                () => this.transportCore.JobAbgeschlossen += this.OnCoreJobAbgeschlossen,
                () =>
                {
                    try
                    {
                        this.transportCore.JobAbgeschlossen -= this.OnCoreJobAbgeschlossen;
                    }
                    catch
                    {
                    }
                });
            this.abos.Abonniere(
                () => this.transportCore.JobFehlgeschlagen += this.OnCoreJobFehlgeschlagen,
                () =>
                {
                    try
                    {
                        this.transportCore.JobFehlgeschlagen -= this.OnCoreJobFehlgeschlagen;
                    }
                    catch
                    {
                    }
                });
        }
    }

    private void UnsubscribeFromEvents()
    {
        // Einheitliches Aufräumen aller Abos
        this.abos.DisposeAll();
    }

    private async Task RegistriereBeiSimulationAsync()
    {
        var container = await this.WarteAufServiceContainerAsync();
        if (container == null)
        {
            DebugLogger.Log("debug_transport", DebugLogger.LogLevel.Error, () => "TransportCoordinator: ServiceContainer nicht verfügbar");
            return;
        }

        var simulation = await container.WaitForNamedService<Simulation>("Simulation");
        if (simulation != null)
        {
            simulation.Register(this);
            DebugLogger.LogTransport("TransportCoordinator: Bei Simulation registriert");
        }
        else
        {
            DebugLogger.Log("debug_transport", DebugLogger.LogLevel.Error, () => "TransportCoordinator: Simulation nicht gefunden");
        }
    }

    private async Task<ServiceContainer?> WarteAufServiceContainerAsync()
    {
        var tree = this.GetTree();
        if (tree == null)
        {
            return ServiceContainer.Instance;
        }

        return await ServiceContainer.WhenAvailableAsync(tree);
    }

    // COMPLETE Tick with all Sub-Service calls

    /// <inheritdoc/>
    public void Tick(double dt)
    {
        // 1. Truck-Tick (Movement, Pathfinding)
        this.truckManager.ProcessTruckTick(dt);

        // 2. Order-Tick (Manual Queue, Job-Planning)
        this.orderManager.ProcessOrderTick(dt);
    }

    // NodePath-Resolver entfernt: SC-only

    // Helper methods for Sub-Services
    public Vector2 CalculateCenter(Building gebaeude)
    {
        return ((Node2D)gebaeude).GlobalPosition + new Vector2(
            gebaeude.Size.X * this.buildingManager.TileSize / 2,
            gebaeude.Size.Y * this.buildingManager.TileSize / 2);
    }

    public void EmitTransportOrderCreated(Truck truck, Building source, Building target)
    {
        if (this.SignaleAktiv && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.TransportOrderCreated, truck, source, target);
        }
    }

    public void EmitOrdersChangedIfSignalsActive()
    {
        if (this.SignaleAktiv && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.OrdersChanged);
            this.eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
        }
    }

    public void EmitMarketOrdersChangedIfSignalsActive()
    {
        if (this.SignaleAktiv && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
        }
    }

    // Public API for Wrapper
    public void AcceptOrder(int id)
    {
        // Legacy wrapper: use Result API and ignore details
        this.orderManager.TryAcceptOrder(id);
    }

    public Result TryAcceptOrder(int id, string? correlationId = null)
    {
        return this.orderManager.TryAcceptOrder(id, correlationId);
    }

    public void HandleTransportClick(Vector2I cell) => this.orderManager.HandleTransportClick(cell);

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders() => this.orderManager.GetOrders();

    public void StartManualTransport(Building source, Building target) => this.orderManager.StartManualTransport(source, target);

    public Result TryStartManualTransport(Building source, Building target, string? correlationId = null)
        => this.orderManager.TryStartManualTransport(source, target, correlationId);

    public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f)
        => this.orderManager.StartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed);

    public Result TryStartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f, string? correlationId = null)
        => this.orderManager.TryStartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed, correlationId);

    public void StopPeriodicSupplyRoute(Building consumer, StringName resourceId)
        => this.orderManager.StopPeriodicSupplyRoute(consumer, resourceId);

    public Result TryStopPeriodicSupplyRoute(Building consumer, StringName resourceId, string? correlationId = null)
        => this.orderManager.TryStopPeriodicSupplyRoute(consumer, resourceId, correlationId);

    public void TruckArrived(Truck t)
    {
        // Interne Lieferung: in Consumer-Inventar buchen, falls möglich
        try
        {
            if (t.TargetNode is IHasInventory inv && t.ResourceId != new StringName("") && t.Amount > 0)
            {
                inv.AddToInventory(t.ResourceId, t.Amount);
            }
        }
        catch
        {
        }

        // Wirtschaftliche Verbuchung (bei Stadtlieferungen relevant)
        this.economyService.ProcessTruckArrival(t);
        try
        {
            // Rückfahrt (leer) einplanen, wenn Quelle/Ziel bekannt sind
            if (t.Amount > 0 && t.SourceNode is Building src && t.TargetNode is Building dst)
            {
                var startPos = this.CalculateCenter(dst);
                var zielPos = this.CalculateCenter(src);
                var rt = this.truckManager.SpawnTruck(startPos, zielPos, 0, 0.0);
                rt.SourceNode = dst;
                rt.TargetNode = src;
                rt.ResourceId = t.ResourceId;
                try
                {
                    rt.SetSpeed(t.GetSpeed());
                }
                catch
                {
                }
            }

            // Pendelbetrieb: Wenn Leerfahrt am Supplier ankommt, Route freigeben
            if (t.Amount == 0 && t.TargetNode is Building backAtSupplier && t.SourceNode is Building consumer && t.ResourceId != new StringName(""))
            {
                this.orderManager.MarkSupplyRouteReturned(backAtSupplier, consumer, t.ResourceId);
            }
        }
        catch
        {
        }
    }

    public void RestartPendingJobs()
    {
        this.truckManager.RestartPendingTrucks();
        this.orderManager.RestartPendingJobs();
    }

    public void RepathAllTrucks() => this.truckManager.RepathAllTrucks();

    public void CancelOrdersFor(Node2D node) => this.truckManager.CancelOrdersFor(node);

    /// <summary>
    /// Vollständiger Reset des Transport-Subsystems für NewGame/ClearState.
    /// </summary>
    public void ClearAllData()
    {
        try
        {
            this.truckManager.ClearAllTrucks();
        }
        catch
        {
        }
        try
        {
            this.orderManager.ClearAllData();
        }
        catch
        {
        }
        try
        {
            this.transportCore?.ClearAllData();
        }
        catch
        {
        }
        DebugLogger.LogTransport("TransportCoordinator: ClearAllData abgeschlossen");
    }

    // Event Handlers delegate to Sub-Services
    private void OnRoadGraphChanged()
    {
        if (!this.IsInsideTree())
        {
            return;
        }

        this.truckManager.RepathAllTrucks();
    }

    private void OnOrdersChanged()
    {
        try
        {
            this.orderManager.UpdateOrderBookFromCities();
        }
        catch
        {
        }
        try
        {
            this.orderManager.UpdateSupplyIndexFromBuildings();
        }
        catch
        {
        }
    }

    private void OnBuildingDestroyed(Node b)
    {
        if (!this.IsInsideTree())
        {
            return;
        }

        if (b is Node2D n2)
        {
            this.truckManager.CancelOrdersFor(n2);
        }
    }

    private void OnCoreJobGeplant(IndustrieLite.Transport.Core.Models.TransportJob job)
    {
        this.orderManager.MarkJobsForReplanning();
    }

    private void OnCoreJobGestartet(IndustrieLite.Transport.Core.Models.TransportJob job)
    {
        if (!this.SignaleAktiv || this.eventHub == null)
        {
            return;
        }

        if (job.TruckKontext is Truck truck)
        {
            var quelle = truck.SourceNode ?? (Node)this;
            var ziel = truck.TargetNode ?? (Node)this;
            this.eventHub.EmitSignal(EventHub.SignalName.TransportOrderCreated, truck, quelle, ziel);
        }

        this.eventHub.EmitSignal(EventHub.SignalName.OrdersChanged);
    }

    private void OnCoreJobAbgeschlossen(IndustrieLite.Transport.Core.Models.TransportJob job)
    {
        try
        {
            this.orderManager.UpdateOrderBookFromCities();
        }
        catch
        {
        }
        try
        {
            this.orderManager.UpdateSupplyIndexFromBuildings();
        }
        catch
        {
        }
        this.orderManager.MarkJobsForReplanning();

        if (this.SignaleAktiv && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.OrdersChanged);
            this.eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
        }
    }

    private void OnCoreJobFehlgeschlagen(IndustrieLite.Transport.Core.Models.TransportJob job)
    {
        var logHinweis = string.Empty;

        if (job.LieferantKontext is IHasInventory inventar)
        {
            // Return stock to inventory to prevent silent losses
            inventar.AddToInventory(job.ResourceId, job.Menge);
            logHinweis = $" - {job.Menge} {job.ResourceId} zurückgebucht";
        }

        DebugLogger.LogTransport(() => $"Job fehlgeschlagen: {job.JobId}{logHinweis}");
        this.orderManager.MarkJobsForReplanning();

        if (this.SignaleAktiv && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.OrdersChanged);
        }
    }
}

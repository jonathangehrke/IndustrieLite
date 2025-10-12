// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IndustrieLite.Transport.Interfaces;
using IndustrieLite.Transport.Core;
using IndustrieLite.Transport.Core.Models;
using IndustrieLite.Transport.Core.Interfaces;
using JobService = IndustrieLite.Transport.Core.Services.TransportJobManager;
using OrderService = IndustrieLite.Transport.Core.Services.TransportOrderManager;
using PlanningServiceCore = IndustrieLite.Transport.Core.Services.TransportPlanningService;
using SupplyServiceCore = IndustrieLite.Transport.Core.Services.TransportSupplyService;
using PersistenceServiceCore = IndustrieLite.Transport.Core.Services.TransportPersistenceService;
using EventServiceCore = IndustrieLite.Transport.Core.Services.TransportEventService;
using CoreJobManagerInterface = IndustrieLite.Transport.Core.Interfaces.ITransportJobManager;
using CoreOrderManagerInterface = IndustrieLite.Transport.Core.Interfaces.ITransportOrderManager;
using CorePlanningInterface = IndustrieLite.Transport.Core.Interfaces.ITransportPlanningService;
using CoreSupplyInterface = IndustrieLite.Transport.Core.Interfaces.ITransportSupplyService;
using CorePersistenceInterface = IndustrieLite.Transport.Core.Interfaces.ITransportPersistenceService;
using CoreEventInterface = IndustrieLite.Transport.Core.Interfaces.ITransportEventService;
using TransportJob = IndustrieLite.Transport.Core.Models.TransportJob;

public partial class TransportCoordinator : Node, ITickable
{
	// SC-only: keine NodePath-Exports mehr
	/// <summary>
	/// Aktiviert/Deaktiviert Transport-bezogene UI-Signale.
	/// </summary>
	[Export] public bool SignaleAktiv { get; set; } = true;
	/// <summary>
	/// Kosten pro Einheit und Tile für Transportkostenberechnung.
	/// </summary>
	[Export] public double CostPerUnitPerTile { get; set; } = GameConstants.Transport.CostPerUnitPerTile;
	/// <summary>
	/// Fixkosten pro Truck-Fahrt.
	/// </summary>
	[Export] public double TruckFixedCost { get; set; } = GameConstants.Transport.TruckFixedCost;
	/// <summary>
	/// Standard-Verkaufspreis pro Einheit (Fallback).
	/// </summary>
	[Export] public double DefaultPricePerUnit { get; set; } = GameConstants.Transport.DefaultPricePerUnit;
	/// <summary>
	/// Maximal transportierte Menge pro Truck (Fallback ohne Gebäude-Upgrade).
	/// </summary>
	[Export] public int MaxMengeProTruck { get; set; } = GameConstants.Transport.DefaultMaxPerTruck;

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
	private EventHub? eventHub;
	private readonly AboVerwalter _abos = new();

	// Public Properties for Wrapper access
	/// <summary>Core-Service-Aggregat für Transport.</summary>
	public TransportCoreService? TransportCore => transportCore;
	/// <summary>Truck-Manager Instanz.</summary>
	public TruckManager TruckManager => truckManager;
	/// <summary>Order-Manager Instanz.</summary>
	public TransportOrderManager OrderManager => orderManager;
	/// <summary>Economy-Service für Transportkosten/Nettoerlöse.</summary>
	public TransportEconomyService EconomyService => economyService;

	// ITickable
	string ITickable.Name => "TransportCoordinator";
	/// <summary>
	/// Anzeigename des Knotens.
	/// </summary>
	public new string Name => "TransportCoordinator";

	// Partial method für DI-Architektur (implementiert in .Initialize.cs)
	partial void OnReady_LegacyCheck();

	public override async void _Ready()
	{
		// 1. External Dependencies - Neue Architektur via Initialize() oder Legacy Fallback
		OnReady_LegacyCheck();

		// 2. Core Services creation (Master-Instances)
		var eh = eventHub; // EventHub should be injected via Initialize()
		if (eh == null)
		{
			DebugLogger.LogTransport("TransportCoordinator: EventHub not yet available, will be set via Initialize()");
		}
		if (roadManager != null && eh != null)
			router = new Router(roadManager, eh);
		var schedulerCore = new Scheduler();
		var jobService = new JobService();
		var orderServiceCore = new OrderService();
		var supplyServiceCore = new SupplyServiceCore();
		var planningServiceCore = new PlanningServiceCore(schedulerCore, router, orderServiceCore, jobService, supplyServiceCore);
		var persistenceServiceCore = new PersistenceServiceCore();
		var eventServiceCore = new EventServiceCore();
		transportCore = new TransportCoreService(jobService, orderServiceCore, planningServiceCore, supplyServiceCore, persistenceServiceCore, eventServiceCore);

		// Core services are managed internally by TransportCoordinator
		// No longer registering with ServiceContainer (Clean Architecture)

		fleet = new Fleet();
		fleet.Name = "Fleet";
		AddChild(fleet);

		// 3. Economy Service creation
		economyService = new TransportEconomyService();
		if (economyManager != null && buildingManager != null)
		{
			economyService.Initialize(economyManager, eventHub, buildingManager, CostPerUnitPerTile, TruckFixedCost, DefaultPricePerUnit);
		}
		else
		{
			DebugLogger.LogTransport($"TransportCoordinator: EconomyService waiting for dependencies - economyManager={economyManager != null}, buildingManager={buildingManager != null}");
		}
		// Set up delegates
		economyService.GetSignaleAktivDelegate = () => SignaleAktiv;
		economyService.SetJobsNeuZuPlanenDelegate = () => orderManager?.MarkJobsForReplanning();
		economyService.GetTransportCoreDelegate = () => transportCore;
		AddChild(economyService);

		// 4. Truck Manager creation
		truckManager = new TruckManager();
		if (buildingManager != null)
		{
			truckManager.Initialize(fleet, roadManager, buildingManager, MaxMengeProTruck);
		}
		else
		{
			DebugLogger.LogTransport("TransportCoordinator: TruckManager waiting for BuildingManager dependency");
		}
		// Set up delegates for cost calculation
		truckManager.CalculateCostDelegate = (start, target, amount) => economyService.CalculateTransportCost(start, target, amount);
		truckManager.CalculateCenterDelegate = (building) => CalculateCenter(building);
		AddChild(truckManager);

		// 5. Order Manager creation
		orderManager = new TransportOrderManager();
		if (buildingManager != null)
		{
			orderManager.Initialize(transportCore, buildingManager, truckManager, economyService, this);
			DebugLogger.LogTransport("TransportCoordinator: OrderManager initialized");
		}
		else
		{
			DebugLogger.LogTransport("TransportCoordinator: OrderManager waiting for BuildingManager dependency");
		}
		AddChild(orderManager);

		// 6. Event Subscriptions via AboVerwalter (sauberes Aufräumen)
		SubscribeToEvents();

		DebugLogger.LogTransport(() => $"TransportCoordinator: Initialization complete - EventHub={(eventHub != null ? "OK" : "NULL")}");

		// 7. Simulation Registration
		await RegistriereBeiSimulationAsync();

		// 8. Initial Data Updates
		try { orderManager.UpdateOrderBookFromCities(); } catch { }
		try { orderManager.UpdateSupplyIndexFromBuildings(); } catch { }
	}

	public override void _ExitTree()
	{
		UnsubscribeFromEvents();
		try { transportCore?.Dispose(); } catch { }
		try { router?.Dispose(); } catch { }
		router = null;
		eventHub = null;
		base._ExitTree();
	}

	private void SubscribeToEvents()
	{
		// EventHub-Events über AboVerwalter abonnieren
		if (eventHub != null)
		{
			_abos.Abonniere(
				() => eventHub.RoadGraphChanged += OnRoadGraphChanged,
				() => { try { eventHub.RoadGraphChanged -= OnRoadGraphChanged; } catch { } }
			);
			_abos.Abonniere(
				() => eventHub.BuildingDestroyed += OnBuildingDestroyed,
				() => { try { eventHub.BuildingDestroyed -= OnBuildingDestroyed; } catch { } }
			);
			_abos.Abonniere(
				() => eventHub.MarketOrdersChanged += OnOrdersChanged,
				() => { try { eventHub.MarketOrdersChanged -= OnOrdersChanged; } catch { } }
			);
			_abos.Abonniere(
				() => eventHub.OrdersChanged += OnOrdersChanged,
				() => { try { eventHub.OrdersChanged -= OnOrdersChanged; } catch { } }
			);
			DebugLogger.LogTransport("TransportCoordinator: EventHub via AboVerwalter verbunden");
		}
		else
		{
			// Kein Hard-Error: EventHub wird in Initialize() injiziert und danach abonniert
			DebugLogger.LogTransport("TransportCoordinator: EventHub noch nicht verfügbar; abonniere bei Initialize()");
		}

		// TransportCore-Events ebenfalls über AboVerwalter
		if (transportCore != null)
		{
			_abos.Abonniere(
				() => transportCore.JobGeplant += OnCoreJobGeplant,
				() => { try { transportCore.JobGeplant -= OnCoreJobGeplant; } catch { } }
			);
			_abos.Abonniere(
				() => transportCore.JobGestartet += OnCoreJobGestartet,
				() => { try { transportCore.JobGestartet -= OnCoreJobGestartet; } catch { } }
			);
			_abos.Abonniere(
				() => transportCore.JobAbgeschlossen += OnCoreJobAbgeschlossen,
				() => { try { transportCore.JobAbgeschlossen -= OnCoreJobAbgeschlossen; } catch { } }
			);
			_abos.Abonniere(
				() => transportCore.JobFehlgeschlagen += OnCoreJobFehlgeschlagen,
				() => { try { transportCore.JobFehlgeschlagen -= OnCoreJobFehlgeschlagen; } catch { } }
			);
		}
	}

	private void UnsubscribeFromEvents()
	{
		// Einheitliches Aufräumen aller Abos
		_abos.DisposeAll();
	}

	private async Task RegistriereBeiSimulationAsync()
	{
		var container = await WarteAufServiceContainerAsync();
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
		var tree = GetTree();
		if (tree == null)
			return ServiceContainer.Instance;
		return await ServiceContainer.WhenAvailableAsync(tree);
	}

	// COMPLETE Tick with all Sub-Service calls
	public void Tick(double dt)
	{
		// 1. Truck-Tick (Movement, Pathfinding)
		truckManager.ProcessTruckTick(dt);

		// 2. Order-Tick (Manual Queue, Job-Planning)
		orderManager.ProcessOrderTick(dt);
	}

	// NodePath-Resolver entfernt: SC-only

	// Helper methods for Sub-Services
	public Vector2 CalculateCenter(Building gebaeude)
	{
		return ((Node2D)gebaeude).GlobalPosition + new Vector2(
			gebaeude.Size.X * buildingManager.TileSize / 2,
			gebaeude.Size.Y * buildingManager.TileSize / 2);
	}

	public void EmitTransportOrderCreated(Truck truck, Building source, Building target)
	{
		if (SignaleAktiv && eventHub != null)
			eventHub.EmitSignal(EventHub.SignalName.TransportOrderCreated, truck, source, target);
	}

	public void EmitOrdersChangedIfSignalsActive()
	{
		if (SignaleAktiv && eventHub != null)
		{
			eventHub.EmitSignal(EventHub.SignalName.OrdersChanged);
			eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
		}
	}

	public void EmitMarketOrdersChangedIfSignalsActive()
	{
		if (SignaleAktiv && eventHub != null)
			eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
	}

	// Public API for Wrapper
public void AcceptOrder(int id)
{
	// Legacy wrapper: use Result API and ignore details
	orderManager.TryAcceptOrder(id);
}

public Result TryAcceptOrder(int id, string? correlationId = null)
{
	return orderManager.TryAcceptOrder(id, correlationId);
}
	public void HandleTransportClick(Vector2I cell) => orderManager.HandleTransportClick(cell);
	public Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders() => orderManager.GetOrders();
public void StartManualTransport(Building source, Building target) => orderManager.StartManualTransport(source, target);
public Result TryStartManualTransport(Building source, Building target, string? correlationId = null)
	=> orderManager.TryStartManualTransport(source, target, correlationId);
public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f)
	=> orderManager.StartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed);

public Result TryStartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f, string? correlationId = null)
	=> orderManager.TryStartPeriodicSupplyRoute(supplier, consumer, resourceId, maxPerTruck, periodSec, speed, correlationId);
public void StopPeriodicSupplyRoute(Building consumer, StringName resourceId)
	=> orderManager.StopPeriodicSupplyRoute(consumer, resourceId);

public Result TryStopPeriodicSupplyRoute(Building consumer, StringName resourceId, string? correlationId = null)
	=> orderManager.TryStopPeriodicSupplyRoute(consumer, resourceId, correlationId);
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
		catch { }

		// Wirtschaftliche Verbuchung (bei Stadtlieferungen relevant)
		economyService.ProcessTruckArrival(t);
		try
		{
			// Rückfahrt (leer) einplanen, wenn Quelle/Ziel bekannt sind
			if (t.Amount > 0 && t.SourceNode is Building src && t.TargetNode is Building dst)
			{
				var startPos = CalculateCenter(dst);
				var zielPos = CalculateCenter(src);
				var rt = truckManager.SpawnTruck(startPos, zielPos, 0, 0.0);
				rt.SourceNode = dst;
				rt.TargetNode = src;
				rt.ResourceId = t.ResourceId;
				try { rt.SetSpeed(t.GetSpeed()); } catch { }
			}

			// Pendelbetrieb: Wenn Leerfahrt am Supplier ankommt, Route freigeben
			if (t.Amount == 0 && t.TargetNode is Building backAtSupplier && t.SourceNode is Building consumer && t.ResourceId != new StringName(""))
			{
				orderManager.MarkSupplyRouteReturned(backAtSupplier, consumer, t.ResourceId);
			}
		}
		catch { }
	}

	public void RestartPendingJobs()
	{
		truckManager.RestartPendingTrucks();
		orderManager.RestartPendingJobs();
	}

	public void RepathAllTrucks() => truckManager.RepathAllTrucks();
	public void CancelOrdersFor(Node2D node) => truckManager.CancelOrdersFor(node);

	/// <summary>
	/// Vollständiger Reset des Transport-Subsystems für NewGame/ClearState
	/// </summary>
	public void ClearAllData()
	{
		try { truckManager.ClearAllTrucks(); } catch { }
		try { orderManager.ClearAllData(); } catch { }
		try { transportCore?.ClearAllData(); } catch { }
		DebugLogger.LogTransport("TransportCoordinator: ClearAllData abgeschlossen");
	}

	// Event Handlers delegate to Sub-Services
	private void OnRoadGraphChanged()
	{
		if (!IsInsideTree())
			return;
		truckManager.RepathAllTrucks();
	}

	private void OnOrdersChanged()
	{
		try { orderManager.UpdateOrderBookFromCities(); } catch { }
		try { orderManager.UpdateSupplyIndexFromBuildings(); } catch { }
	}

	private void OnBuildingDestroyed(Node b)
	{
		if (!IsInsideTree())
			return;
		if (b is Node2D n2)
			truckManager.CancelOrdersFor(n2);
	}

	private void OnCoreJobGeplant(IndustrieLite.Transport.Core.Models.TransportJob job)
	{
		orderManager.MarkJobsForReplanning();
	}

	private void OnCoreJobGestartet(IndustrieLite.Transport.Core.Models.TransportJob job)
	{
		if (!SignaleAktiv || eventHub == null)
			return;

		if (job.TruckKontext is Truck truck)
		{
			var quelle = truck.SourceNode ?? (Node)this;
			var ziel = truck.TargetNode ?? (Node)this;
			eventHub.EmitSignal(EventHub.SignalName.TransportOrderCreated, truck, quelle, ziel);
		}

		eventHub.EmitSignal(EventHub.SignalName.OrdersChanged);
	}

	private void OnCoreJobAbgeschlossen(IndustrieLite.Transport.Core.Models.TransportJob job)
	{
		try { orderManager.UpdateOrderBookFromCities(); } catch { }
		try { orderManager.UpdateSupplyIndexFromBuildings(); } catch { }
		orderManager.MarkJobsForReplanning();

		if (SignaleAktiv && eventHub != null)
		{
			eventHub.EmitSignal(EventHub.SignalName.OrdersChanged);
			eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
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
		orderManager.MarkJobsForReplanning();

		if (SignaleAktiv && eventHub != null)
			eventHub.EmitSignal(EventHub.SignalName.OrdersChanged);
	}
}

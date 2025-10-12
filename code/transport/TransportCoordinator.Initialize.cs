// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// TransportCoordinator: Explizite Dependency Injection (neue DI-Architektur)
/// </summary>
public partial class TransportCoordinator
{
	private bool _initialized;

	/// <summary>
	/// Explizite Dependency Injection (neue Architektur).
	/// Wird von TransportManager.Initialize() aufgerufen.
	/// </summary>
	public void Initialize(BuildingManager buildingManager, RoadManager? roadManager, EconomyManager economyManager, EventHub? eventHub)
	{
		if (_initialized)
		{
			DebugLogger.LogTransport("TransportCoordinator.Initialize(): Bereits initialisiert, überspringe");
			return;
		}

		this.buildingManager = buildingManager;
		this.roadManager = roadManager;
		this.economyManager = economyManager;
		this.eventHub = eventHub;

		_initialized = true;
		DebugLogger.LogTransport($"TransportCoordinator.Initialize(): Dependencies gesetzt OK (Building={buildingManager != null}, Road={roadManager != null}, Economy={economyManager != null})");

		// If services were already created in _Ready() but couldn't be initialized, initialize them now
		if (IsInsideTree())
		{
			ReinitializeServices();
		}
	}

	/// <summary>
	/// Re-initialize services that were created in _Ready() but couldn't be fully initialized due to missing dependencies.
	/// </summary>
	private void ReinitializeServices()
	{
		DebugLogger.LogTransport("TransportCoordinator.ReinitializeServices(): Re-initializing services with injected dependencies");

		if (economyService != null && economyManager != null && buildingManager != null)
		{
			economyService.Initialize(economyManager, eventHub, buildingManager, CostPerUnitPerTile, TruckFixedCost, DefaultPricePerUnit);
			DebugLogger.LogTransport("TransportCoordinator: Re-initialized EconomyService");
		}

		if (truckManager != null && buildingManager != null && fleet != null)
		{
			truckManager.Initialize(fleet, roadManager, buildingManager, MaxMengeProTruck);
			DebugLogger.LogTransport("TransportCoordinator: Re-initialized TruckManager");
		}

		if (orderManager != null && transportCore != null && buildingManager != null && truckManager != null && economyService != null)
		{
			orderManager.Initialize(transportCore, buildingManager, truckManager, economyService, this);
			DebugLogger.LogTransport("TransportCoordinator: Re-initialized OrderManager");
		}

		// Re-create router if needed
		if (router == null && roadManager != null && eventHub != null)
		{
			router = new Router(roadManager, eventHub);
			DebugLogger.LogTransport("TransportCoordinator: Created Router");
		}

		// Ensure event subscriptions are active once EventHub is available
		if (eventHub != null)
		{
			try { UnsubscribeFromEvents(); } catch { }
			SubscribeToEvents();
		}
	}

	/// <summary>
	/// Validate dependencies were properly initialized via DI.
	/// </summary>
	partial void OnReady_LegacyCheck()
	{
		if (_initialized)
		{
			// Bereits via Initialize() initialisiert - Dependencies bereits gesetzt
			DebugLogger.LogTransport("TransportCoordinator: OnReady_LegacyCheck - already initialized OK");
			return;
		}

		// Note: This is expected during NewGame startup due to Godot's _Ready() call order
		// TransportManager will call Initialize() shortly after via DIContainer.InitializeAll()
		DebugLogger.LogTransport("TransportCoordinator: OnReady_LegacyCheck - not yet initialized (will be initialized by DIContainer)");

		// Leave dependencies as-is - they will be injected via TransportManager.Initialize()
	}
}

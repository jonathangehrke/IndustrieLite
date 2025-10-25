// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// TransportCoordinator: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class TransportCoordinator
{
    private bool initialized;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von TransportManager.Initialize() aufgerufen.
    /// </summary>
    public void Initialize(BuildingManager buildingManager, RoadManager? roadManager, EconomyManager economyManager, GameManager gameManager, EventHub? eventHub)
    {
        if (this.initialized)
        {
            DebugLogger.LogTransport("TransportCoordinator.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.buildingManager = buildingManager;
        this.roadManager = roadManager;
        this.economyManager = economyManager;
        this.gameManager = gameManager;
        this.eventHub = eventHub;

        this.initialized = true;
        DebugLogger.LogTransport($"TransportCoordinator.Initialize(): Dependencies gesetzt OK (Building={buildingManager != null}, Road={roadManager != null}, Economy={economyManager != null}, Game={gameManager != null})");

        // If services were already created in _Ready() but couldn't be initialized, initialize them now
        if (this.IsInsideTree())
        {
            this.ReinitializeServices();
        }
    }

    /// <summary>
    /// Re-initialize services that were created in _Ready() but couldn't be fully initialized due to missing dependencies.
    /// </summary>
    private void ReinitializeServices()
    {
        DebugLogger.LogTransport("TransportCoordinator.ReinitializeServices(): Re-initializing services with injected dependencies");

        if (this.economyService != null && this.economyManager != null && this.buildingManager != null)
        {
            this.economyService.Initialize(this.economyManager, this.eventHub, this.buildingManager, this.CostPerUnitPerTile, this.TruckFixedCost, this.DefaultPricePerUnit);
            DebugLogger.LogTransport("TransportCoordinator: Re-initialized EconomyService");
        }

        if (this.truckManager != null && this.buildingManager != null && this.fleet != null && this.gameManager != null)
        {
            this.truckManager.Initialize(this.fleet, this.roadManager, this.buildingManager, this.gameManager, this.MaxMengeProTruck);
            DebugLogger.LogTransport("TransportCoordinator: Re-initialized TruckManager");
        }

        if (this.orderManager != null && this.transportCore != null && this.buildingManager != null && this.truckManager != null && this.economyService != null)
        {
            this.orderManager.Initialize(this.transportCore, this.buildingManager, this.truckManager, this.economyService, this);
            DebugLogger.LogTransport("TransportCoordinator: Re-initialized OrderManager");
        }

        // Re-create router if needed
        if (this.router == null && this.roadManager != null && this.eventHub != null)
        {
            this.router = new Router(this.roadManager, this.eventHub);
            DebugLogger.LogTransport("TransportCoordinator: Created Router");
        }

        // Ensure event subscriptions are active once EventHub is available
        if (this.eventHub != null)
        {
            try
            {
                this.UnsubscribeFromEvents();
            }
            catch
            {
            }
            this.SubscribeToEvents();
        }
    }

    /// <summary>
    /// Validate dependencies were properly initialized via DI.
    /// </summary>
    partial void OnReady_LegacyCheck()
    {
        if (this.initialized)
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

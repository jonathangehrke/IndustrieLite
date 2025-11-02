// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// TransportManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class TransportManager
{
    private bool initialized;

    // Store dependencies for later initialization
    private IRoadManager? pendingRoadManager;
    private IEconomyManager? pendingEconomyManager;
    private GameManager? pendingGameManager;
    private EventHub? pendingEventHub;
    private ISceneGraph? pendingSceneGraph;
    private GameTimeManager? pendingGameTimeManager;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(IBuildingManager buildingManager, IRoadManager? roadManager, IEconomyManager economyManager, GameManager gameManager, ISceneGraph sceneGraph, EventHub? eventHub, GameTimeManager? gameTimeManager)
    {
        if (this.initialized)
        {
            DebugLogger.LogTransport("TransportManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        // Validate required dependencies (fail-fast)
        if (buildingManager == null)
        {
            throw new System.ArgumentNullException(nameof(buildingManager), "TransportManager requires BuildingManager");
        }
        if (economyManager == null)
        {
            throw new System.ArgumentNullException(nameof(economyManager), "TransportManager requires EconomyManager for transport costs");
        }
        if (gameManager == null)
        {
            throw new System.ArgumentNullException(nameof(gameManager), "TransportManager requires GameManager");
        }
        if (sceneGraph == null)
        {
            throw new System.ArgumentNullException(nameof(sceneGraph), "TransportManager requires ISceneGraph to add coordinator");
        }

        // Store BuildingManager reference (for ResolveBuildingManager)
        this.buildingManager = (BuildingManager?)buildingManager; // Cast for storage (will be replaced with interface field later)

        // Store dependencies for later initialization (when coordinator exists)
        this.pendingRoadManager = roadManager;
        this.pendingEconomyManager = economyManager;
        this.pendingGameManager = gameManager;
        this.pendingSceneGraph = sceneGraph;
        this.pendingEventHub = eventHub;
        this.pendingGameTimeManager = gameTimeManager;

        // If coordinator already exists (e.g., _Ready was called first), initialize it now
        if (this.coordinator != null)
        {
            this.coordinator.Initialize((BuildingManager)buildingManager, (RoadManager?)roadManager, (EconomyManager)economyManager, gameManager, eventHub, gameTimeManager);
            DebugLogger.LogTransport("TransportManager.Initialize(): Coordinator initialized immediately");
        }
        else
        {
            DebugLogger.LogTransport("TransportManager.Initialize(): Dependencies stored, will initialize coordinator in _Ready()");
        }

        this.initialized = true;
        DebugLogger.LogTransport($"TransportManager.Initialize(): Initialisiert OK (Building={buildingManager != null}, Road={roadManager != null}, Economy={economyManager != null}, Game={gameManager != null})");
    }

    /// <summary>
    /// Called from _Ready() after coordinator is created, to apply pending dependencies.
    /// </summary>
    private void InitializeCoordinatorIfPending()
    {
        if (this.initialized && this.coordinator != null && this.buildingManager != null && this.pendingGameManager != null)
        {
            this.coordinator.Initialize(this.buildingManager, (RoadManager?)this.pendingRoadManager, (EconomyManager)this.pendingEconomyManager!, this.pendingGameManager, this.pendingEventHub, this.pendingGameTimeManager);
            DebugLogger.LogTransport("TransportManager: Applied pending dependencies to coordinator");
        }
    }
}

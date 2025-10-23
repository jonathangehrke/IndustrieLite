// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// TransportManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class TransportManager
{
    private bool initialized;

    // Store dependencies for later initialization
    private RoadManager? pendingRoadManager;
    private EconomyManager? pendingEconomyManager;
    private EventHub? pendingEventHub;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(BuildingManager buildingManager, RoadManager? roadManager, EconomyManager economyManager, EventHub? eventHub)
    {
        if (this.initialized)
        {
            DebugLogger.LogTransport("TransportManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        // Store BuildingManager reference (for ResolveBuildingManager)
        this.buildingManager = buildingManager;

        // Store dependencies for later initialization (when coordinator exists)
        this.pendingRoadManager = roadManager;
        this.pendingEconomyManager = economyManager;
        this.pendingEventHub = eventHub;

        // If coordinator already exists (e.g., _Ready was called first), initialize it now
        if (this.coordinator != null)
        {
            this.coordinator.Initialize(buildingManager, roadManager, economyManager, eventHub);
            DebugLogger.LogTransport("TransportManager.Initialize(): Coordinator initialized immediately");
        }
        else
        {
            DebugLogger.LogTransport("TransportManager.Initialize(): Dependencies stored, will initialize coordinator in _Ready()");
        }

        this.initialized = true;
        DebugLogger.LogTransport($"TransportManager.Initialize(): Initialisiert OK (Building={buildingManager != null}, Road={roadManager != null}, Economy={economyManager != null})");
    }

    /// <summary>
    /// Called from _Ready() after coordinator is created, to apply pending dependencies.
    /// </summary>
    private void InitializeCoordinatorIfPending()
    {
        if (this.initialized && this.coordinator != null && this.buildingManager != null)
        {
            this.coordinator.Initialize(this.buildingManager, this.pendingRoadManager, this.pendingEconomyManager!, this.pendingEventHub);
            DebugLogger.LogTransport("TransportManager: Applied pending dependencies to coordinator");
        }
    }
}

// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// TransportManager: Explizite Dependency Injection (neue DI-Architektur)
/// </summary>
public partial class TransportManager
{
    private bool _initialized;

    // Store dependencies for later initialization
    private RoadManager? _pendingRoadManager;
    private EconomyManager? _pendingEconomyManager;
    private EventHub? _pendingEventHub;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(BuildingManager buildingManager, RoadManager? roadManager, EconomyManager economyManager, EventHub? eventHub)
    {
        if (_initialized)
        {
            DebugLogger.LogTransport("TransportManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        // Store BuildingManager reference (for ResolveBuildingManager)
        this.buildingManager = buildingManager;

        // Store dependencies for later initialization (when coordinator exists)
        _pendingRoadManager = roadManager;
        _pendingEconomyManager = economyManager;
        _pendingEventHub = eventHub;

        // If coordinator already exists (e.g., _Ready was called first), initialize it now
        if (coordinator != null)
        {
            coordinator.Initialize(buildingManager, roadManager, economyManager, eventHub);
            DebugLogger.LogTransport("TransportManager.Initialize(): Coordinator initialized immediately");
        }
        else
        {
            DebugLogger.LogTransport("TransportManager.Initialize(): Dependencies stored, will initialize coordinator in _Ready()");
        }

        _initialized = true;
        DebugLogger.LogTransport($"TransportManager.Initialize(): Initialisiert OK (Building={buildingManager != null}, Road={roadManager != null}, Economy={economyManager != null})");
    }

    /// <summary>
    /// Called from _Ready() after coordinator is created, to apply pending dependencies.
    /// </summary>
    private void InitializeCoordinatorIfPending()
    {
        if (_initialized && coordinator != null && buildingManager != null)
        {
            coordinator.Initialize(buildingManager, _pendingRoadManager, _pendingEconomyManager!, _pendingEventHub);
            DebugLogger.LogTransport("TransportManager: Applied pending dependencies to coordinator");
        }
    }
}

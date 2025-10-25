// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// SupplierService - Explicit DI Initialization (Phase 6)
/// Dependencies werden via Initialize() injiziert statt via ServiceContainer lookup.
/// </summary>
public partial class SupplierService : Node
{
    /// <summary>
    /// Explizite Initialisierung mit allen Dependencies.
    /// Ersetzt InitializeDependencies() ServiceContainer lookups.
    /// </summary>
    public void Initialize(BuildingManager? buildingManager, TransportManager? transportManager, GameDatabase? gameDatabase, EventHub? eventHub)
    {
        this.buildingManager = buildingManager;
        this.transportManager = transportManager;
        this.gameDatabase = gameDatabase;
        this.eventHub = eventHub;

        if (buildingManager == null)
        {
            DebugLogger.LogServices("SupplierService: WARNING - BuildingManager not found");
        }

        if (transportManager == null)
        {
            DebugLogger.LogServices("SupplierService: WARNING - TransportManager not found");
        }

        if (gameDatabase == null)
        {
            DebugLogger.LogServices("SupplierService: WARNING - GameDatabase not found");
        }

        if (eventHub == null)
        {
            DebugLogger.LogServices("SupplierService: WARNING - EventHub not found");
        }
    }
}

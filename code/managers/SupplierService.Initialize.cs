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
    public void Initialize(IBuildingManager buildingManager, ITransportManager transportManager, GameDatabase gameDatabase, EventHub? eventHub)
    {
        // Validate required dependencies (fail-fast)
        if (buildingManager == null)
        {
            throw new System.ArgumentNullException(nameof(buildingManager), "SupplierService requires BuildingManager");
        }
        if (transportManager == null)
        {
            throw new System.ArgumentNullException(nameof(transportManager), "SupplierService requires TransportManager");
        }
        if (gameDatabase == null)
        {
            throw new System.ArgumentNullException(nameof(gameDatabase), "SupplierService requires GameDatabase");
        }

        this.buildingManager = (BuildingManager)buildingManager; // Cast for storage (will be replaced with interface field later)
        this.transportManager = (TransportManager)transportManager; // Cast for storage (will be replaced with interface field later)
        this.gameDatabase = gameDatabase;
        this.eventHub = eventHub;

        // EventHub is truly optional for this service
        if (eventHub == null)
        {
            DebugLogger.LogServices("SupplierService: INFO - EventHub not available, supplier change events won't be emitted");
        }
    }
}

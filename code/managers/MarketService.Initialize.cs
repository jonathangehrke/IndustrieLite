// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// MarketService - Explicit DI Initialization (Phase 6)
/// Dependencies werden via Initialize() injiziert statt via ServiceContainer lookup.
/// </summary>
public partial class MarketService : Node
{
    /// <summary>
    /// Explizite Initialisierung mit allen Dependencies.
    /// Ersetzt InitializeDependencies() ServiceContainer lookups.
    /// </summary>
    public void Initialize(
        IResourceManager resourceManager,  // Changed to required
        ITransportManager transportManager,  // Changed to required
        IEconomyManager economyManager,  // Changed to required
        IBuildingManager buildingManager,  // Changed to required
        LevelManager? levelManager = null,
        Database? database = null)
    {
        // Validate required dependencies (fail-fast)
        if (resourceManager == null)
        {
            throw new System.ArgumentNullException(nameof(resourceManager), "MarketService requires ResourceManager");
        }
        if (transportManager == null)
        {
            throw new System.ArgumentNullException(nameof(transportManager), "MarketService requires TransportManager");
        }
        if (economyManager == null)
        {
            throw new System.ArgumentNullException(nameof(economyManager), "MarketService requires EconomyManager");
        }
        if (buildingManager == null)
        {
            throw new System.ArgumentNullException(nameof(buildingManager), "MarketService requires BuildingManager");
        }

        this.resourceManager = (ResourceManager)resourceManager; // Cast for storage (will be replaced with interface field later)
        this.transportManager = (TransportManager)transportManager; // Cast for storage (will be replaced with interface field later)
        this.economyManager = (EconomyManager)economyManager; // Cast for storage (will be replaced with interface field later)
        this.buildingManager = (BuildingManager)buildingManager; // Cast for storage (will be replaced with interface field later)
        this.levelManager = levelManager;
        this.database = database;

        // Register in ServiceContainer for TransportEconomyService to find
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(ServiceNames.MarketService, this);
                DebugLogger.LogServices("MarketService: Registered in ServiceContainer");
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogServices($"MarketService: Failed to register in ServiceContainer: {ex.Message}");
            }
        }

        // Removed WARNING logs - dependencies are now required and validated above

        if (levelManager == null)
        {
            DebugLogger.LogServices("MarketService: WARNING - LevelManager not found (Level-System disabled)");
        }
        else
        {
            DebugLogger.LogServices("MarketService: LevelManager injected successfully");
        }

        if (database == null)
        {
            DebugLogger.LogServices("MarketService: WARNING - Database not found (product unlock checks may fallback)");
        }
        else
        {
            DebugLogger.LogServices("MarketService: Database injected successfully");
        }
    }
}

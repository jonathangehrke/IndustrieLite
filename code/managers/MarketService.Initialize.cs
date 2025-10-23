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
        ResourceManager? resourceManager,
        TransportManager? transportManager,
        EconomyManager? economyManager,
        BuildingManager? buildingManager,
        LevelManager? levelManager = null,
        Database? database = null)
    {
        this.resourceManager = resourceManager;
        this.transportManager = transportManager;
        this.economyManager = economyManager;
        this.buildingManager = buildingManager;
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

        if (resourceManager == null)
        {
            DebugLogger.LogServices("MarketService: WARNING - ResourceManager not found");
        }

        if (transportManager == null)
        {
            DebugLogger.LogServices("MarketService: WARNING - TransportManager not found");
        }

        if (economyManager == null)
        {
            DebugLogger.LogServices("MarketService: WARNING - EconomyManager not found");
        }

        if (buildingManager == null)
        {
            DebugLogger.LogServices("MarketService: WARNING - BuildingManager not found");
        }

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

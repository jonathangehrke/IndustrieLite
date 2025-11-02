// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// LogisticsService - Explicit DI Initialization (Phase 6)
/// Dependencies werden via Initialize() injiziert statt via ServiceContainer lookup.
/// </summary>
public partial class LogisticsService : Node
{
    /// <summary>
    /// Explizite Initialisierung mit allen Dependencies.
    /// Ersetzt InitializeDependencies() ServiceContainer lookups.
    /// </summary>
    public void Initialize(IEconomyManager economyManager, EventHub? eventHub)
    {
        // Validate required dependencies (fail-fast)
        if (economyManager == null)
        {
            throw new System.ArgumentNullException(nameof(economyManager), "LogisticsService requires EconomyManager for upgrade cost calculations");
        }

        this.economyManager = (EconomyManager)economyManager; // Cast for storage (will be replaced with interface field later)
        this.eventHub = eventHub;

        if (eventHub == null)
        {
            DebugLogger.LogServices("LogisticsService: INFO - EventHub not available, upgrade events won't be emitted");
        }

        this.isInitialized = true;
    }
}

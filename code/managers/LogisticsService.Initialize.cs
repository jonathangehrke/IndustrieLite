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
    public void Initialize(EconomyManager? economyManager, EventHub? eventHub)
    {
        this.economyManager = economyManager;
        this.eventHub = eventHub;

        if (economyManager == null)
        {
            DebugLogger.Error("debug_services", "LogisticsEconomyMissing", "EconomyManager not found! Buttons will be disabled.");
        }

        if (eventHub == null)
        {
            DebugLogger.Warn("debug_services", "LogisticsEventHubMissing", "EventHub not found");
        }

        this.isInitialized = true;
    }
}

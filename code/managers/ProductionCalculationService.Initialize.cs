// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// ProductionCalculationService - Explicit DI Initialization (Phase 6)
/// Dependencies werden via Initialize() injiziert statt via ServiceContainer lookup.
/// </summary>
public partial class ProductionCalculationService : Node
{
    /// <summary>
    /// Explizite Initialisierung mit allen Dependencies.
    /// Ersetzt InitializeDependencies() ServiceContainer lookups.
    /// </summary>
    public void Initialize(GameDatabase? gameDatabase)
    {
        this.gameDatabase = gameDatabase;

        if (gameDatabase == null)
        {
            DebugLogger.LogServices("ProductionCalculationService: WARNING - GameDatabase not found");
        }
    }
}

// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// LandManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class LandManager
{
    private bool initialized;
    private EconomyManager? economyManager;
    private EventHub? eventHub;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(EconomyManager economyManager, EventHub? eventHub)
    {
        if (this.initialized)
        {
            DebugLogger.LogServices("LandManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        // Validate required dependencies (fail-fast)
        if (economyManager == null)
        {
            throw new System.ArgumentNullException(nameof(economyManager), "LandManager requires EconomyManager for BuyLand operations");
        }

        this.economyManager = economyManager;
        this.eventHub = eventHub;

        // Grid-Initialisierung
        this.Land = new bool[this.GridW, this.GridH];
        this.StartLand = new bool[this.GridW, this.GridH];
        this.InitializeStartingLand();

        this.initialized = true;
        DebugLogger.LogServices($"LandManager.Initialize(): Initialisiert mit GridW={this.GridW}, GridH={this.GridH}, EconomyManager={((economyManager != null) ? "✓" : "null")}, EventHub={((eventHub != null) ? "✓" : "null")}");
    }
}

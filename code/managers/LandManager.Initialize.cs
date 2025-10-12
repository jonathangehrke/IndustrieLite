// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// LandManager: Explizite Dependency Injection (neue DI-Architektur)
/// </summary>
public partial class LandManager
{
    private bool _initialized;
    private EconomyManager? _economyManager;
    private EventHub? _eventHub;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(EconomyManager economyManager, EventHub? eventHub)
    {
        if (_initialized)
        {
            DebugLogger.LogServices("LandManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        _economyManager = economyManager;
        _eventHub = eventHub;

        // Grid-Initialisierung
        Land = new bool[GridW, GridH];
        StartLand = new bool[GridW, GridH];
        InitializeStartingLand();

        _initialized = true;
        DebugLogger.LogServices($"LandManager.Initialize(): Initialisiert mit GridW={GridW}, GridH={GridH}, EconomyManager={((economyManager != null) ? "✓" : "null")}, EventHub={((eventHub != null) ? "✓" : "null")}");
    }
}

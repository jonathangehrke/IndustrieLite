// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// GameClockManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class GameClockManager
{
    private bool initialized;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(EventHub? eventHub)
    {
        if (this.initialized)
        {
            DebugLogger.LogServices("GameClockManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        // GameClockManager hat aktuell keine Dependencies außer EventHub (optional für zukünftige Events)
        // EventHub wird gespeichert falls später Signals gebraucht werden
        this.initialized = true;
        DebugLogger.LogServices($"GameClockManager.Initialize(): Initialisiert (Enabled={this.Enabled}, TickRate={this.TickRate}Hz)");
    }
}

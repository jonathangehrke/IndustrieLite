// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// CityGrowthManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class CityGrowthManager
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
            DebugLogger.LogServices("CityGrowthManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.eventHub = eventHub;

        // Signal-Verbindung
        if (eventHub != null)
        {
            this.abos.VerbindeSignal(eventHub, EventHub.SignalName.MonthChanged, this, nameof(this.OnMonthChanged));
        }

        this.initialized = true;
        DebugLogger.LogServices($"CityGrowthManager.Initialize(): Initialisiert (EventHub={(eventHub != null ? "OK" : "null")})");
    }
}

// SPDX-License-Identifier: MIT
using System.Globalization;
using Godot;

/// <summary>
/// EconomyManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class EconomyManager
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
            DebugLogger.LogServices("EconomyManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.eventHub = eventHub;
        this.Money = this.StartingMoney;

        this.initialized = true;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "EconomyManager.Initialize(): Initialisiert mit StartingMoney={0}, EventHub={1}", this.Money, (eventHub != null) ? "OK" : "null"));
    }
}

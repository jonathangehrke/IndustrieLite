// SPDX-License-Identifier: MIT
using Godot;
using System.Globalization;

/// <summary>
/// EconomyManager: Explizite Dependency Injection (neue DI-Architektur)
/// </summary>
public partial class EconomyManager
{
    private bool _initialized;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(EventHub? eventHub)
    {
        if (_initialized)
        {
            DebugLogger.LogServices("EconomyManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.eventHub = eventHub;
        this.Money = StartingMoney;

        _initialized = true;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "EconomyManager.Initialize(): Initialisiert mit StartingMoney={0}, EventHub={1}", Money, ((eventHub != null) ? "OK" : "null")));
    }
}

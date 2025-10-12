// SPDX-License-Identifier: MIT
using Godot;
using System;

/// <summary>
/// GameTimeManager: Explizite Dependency Injection (neue DI-Architektur)
/// </summary>
public partial class GameTimeManager
{
    private bool _initialized;
    private bool _registeredWithSimulation;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(EventHub? eventHub, Simulation? simulation)
    {
        if (_initialized)
        {
            DebugLogger.LogServices("GameTimeManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this._eventHub = eventHub;

        // Registrierung bei Simulation
        if (simulation != null)
        {
            try
            {
                simulation.Register(this);
                _registeredWithSimulation = true;
                DebugLogger.LogServices("GameTimeManager.Initialize(): Bei Simulation registriert");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("debug_simulation", "GameTimeRegisterWithSimulationFailed", ex.Message);
            }
        }
        else
        {
            DebugLogger.Log("debug_simulation", DebugLogger.LogLevel.Warn, () => "GameTimeManager.Initialize(): Simulation ist null - nicht registriert");
        }

        _initialized = true;
        DebugLogger.LogServices($"GameTimeManager.Initialize(): Initialisiert OK (EventHub={eventHub != null}, Simulation={simulation != null})");

        // Initiales Datum emittieren (nach DI-Setup)
        EmitDateChanged();
    }
}

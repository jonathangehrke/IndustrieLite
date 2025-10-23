// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// GameTimeManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class GameTimeManager
{
    private bool initialized;
    private bool registeredWithSimulation;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(EventHub? eventHub, Simulation? simulation)
    {
        if (this.initialized)
        {
            DebugLogger.LogServices("GameTimeManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.eventHub = eventHub;

        // Registrierung bei Simulation
        if (simulation != null)
        {
            try
            {
                simulation.Register(this);
                this.registeredWithSimulation = true;
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

        this.initialized = true;
        DebugLogger.LogServices($"GameTimeManager.Initialize(): Initialisiert OK (EventHub={eventHub != null}, Simulation={simulation != null})");

        // Initiales Datum emittieren (nach DI-Setup)
        this.EmitDateChanged();
    }
}

// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Simulation Manager fuer M8 - Shadow-Mode Produktionssystem
/// Verwaltet alle ITickable Systeme mit fester Tick-Rate ueber den GameClockManager.
/// </summary>
public partial class Simulation : Node
{
    [Export]
    public float TickRate = 5.0f; // 5 Ticks pro Sekunde

    private readonly List<ITickable> tickables = new();
    private bool isActive;
    private GameClockManager? gameClock;
    private double accumulator;
    private readonly AboVerwalter abos = new();

    private static Simulation? instance;

    public static Simulation? Instance
    {
        get
        {
            var inst = Volatile.Read(ref instance);
            if (inst != null && !GodotObject.IsInstanceValid(inst))
            {
                return null;
            }

            return inst;
        }
    }

    public bool IstAktiv => this.isActive;

    private static int simTickSchachtelung = 0;

    private static void BetreteSimTickKontext()
    {
        simTickSchachtelung++;
    }

    private static void VerlasseSimTickKontext()
    {
        if (simTickSchachtelung > 0)
        {
            simTickSchachtelung--;
        }
    }

    public static bool IsInSimTick()
    {
        return simTickSchachtelung > 0;
    }

    public static void ValidateSimTickContext(string vorgang = "")
    {
#if DEBUG
        if (!IsInSimTick())
        {
            var meldung = string.IsNullOrEmpty(vorgang) ? "Unbekannte Operation" : vorgang;
            throw new InvalidOperationException($"DETERMINISMUS-VERLETZUNG: {meldung} ausserhalb des SimTick-Kontextes");
        }
#else
		if (!IsInSimTick())
		{
			var meldung = string.IsNullOrEmpty(vorgang) ? "Unbekannte Operation" : vorgang;
			DebugLogger.LogServices(() => $"WARNUNG: {meldung} ausserhalb des SimTick-Kontextes - potenziell nicht deterministisch");
		}
#endif
    }

    public static IDisposable EnterDeterministicTestScope()
    {
        BetreteSimTickKontext();
        return new SimTickTestScope();
    }

    private sealed class SimTickTestScope : IDisposable
    {
        private bool aktiv = true;

        public void Dispose()
        {
            if (!this.aktiv)
            {
                return;
            }

            VerlasseSimTickKontext();
            this.aktiv = false;
        }
    }

    public override async void _Ready()
    {
        var previous = Interlocked.CompareExchange(ref instance, this, null);
        if (previous != null && !ReferenceEquals(previous, this))
        {
            DebugLogger.Error("debug_simulation", "SimulationMultipleInstances", $"Multiple instances detected!", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "existing", previous.GetHashCode() }, { "new", this.GetHashCode() } });
            this.QueueFree();
            return;
        }
        DebugLogger.Info("debug_simulation", "SimulationInitialized", $"Initialized as singleton", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "hash", this.GetHashCode() } });

        await this.VerbindeMitGameClockAsync();
        DebugLogger.LogServices($"Simulation bereit: TickRate={this.TickRate}, Interval={(this.TickRate > 0 ? 1.0f / this.TickRate : 0.2f):F2}s");
    }

    /// <summary>
    /// Registriert ein ITickable System.
    /// </summary>
    public void Register(ITickable tickable)
    {
        if (!this.tickables.Contains(tickable))
        {
            this.tickables.Add(tickable);
            DebugLogger.LogServices($"Simulation: {tickable.Name} registriert ({this.tickables.Count} Systeme)");

            // City-Instanzen speziell loggen
            if (tickable is City city)
            {
                DebugLogger.LogBuilding(() => $"Simulation: City '{city.CityName}' registered for market order generation");
            }
        }
    }

    /// <summary>
    /// Entfernt ein ITickable System.
    /// </summary>
    public void Unregister(ITickable tickable)
    {
        if (this.tickables.Remove(tickable))
        {
            DebugLogger.LogServices($"Simulation: {tickable.Name} entfernt ({this.tickables.Count} Systeme)");
        }
    }

    /// <summary>
    /// Startet die Simulation.
    /// </summary>
    public void Start()
    {
        if (!this.isActive)
        {
            this.isActive = true;
            this.accumulator = 0.0;
            DebugLogger.Info("debug_simulation", "SimulationStarted", "Simulation started", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "hash", this.GetHashCode() } });
            DebugLogger.LogServices($"Simulation gestartet: {this.tickables.Count} Systeme");
        }
        else
        {
            DebugLogger.Warn("debug_simulation", "SimulationAlreadyStarted", "Already started", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "hash", this.GetHashCode() } });
        }
    }

    /// <summary>
    /// Stoppt die Simulation.
    /// </summary>
    public void Stop()
    {
        if (this.isActive)
        {
            this.isActive = false;
            DebugLogger.Info("debug_simulation", "SimulationStopped", "Stopped by external call");
            DebugLogger.LogServices("Simulation gestoppt");
        }
    }

    /// <summary>
    /// Manueller Tick (fuer Tests).
    /// </summary>
    public void ManualTick()
    {
        DebugLogger.LogPerf("=== Manueller Simulation Tick ===");
        var interval = this.TickRate > 0 ? 1.0 / this.TickRate : 0.2; // Fallback 5 Hz
        this.OnTick(interval);
    }

    private void OnTick(double dt)
    {
        BetreteSimTickKontext();
        var tickStart = Time.GetUnixTimeFromSystem();

        DebugLogger.LogServices($"Simulation OnTick({dt:F3}): {this.tickables.Count} systems");

        try
        {
            foreach (var tickable in this.tickables)
            {
                try
                {
                    DebugLogger.LogServices($"Simulation: Calling {tickable.Name}.Tick({dt:F3})");
                    tickable.Tick(dt);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log("debug_simulation", DebugLogger.LogLevel.Error, () => $"Fehler beim Ticken von {tickable.Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            VerlasseSimTickKontext();
        }

        var tickEnd = Time.GetUnixTimeFromSystem();
        var duration = (tickEnd - tickStart) * 1000; // ms

        if (duration > 10)
        {
            DebugLogger.LogPerf($"Simulation Tick: {duration:F1}ms fuer {this.tickables.Count} Systeme");
        }
    }

    private void OnGameClockSimTick(double dt)
    {
        if (!this.isActive)
        {
            DebugLogger.Debug("debug_simulation", "SimulationInactiveSkip", "Not active, skipping tick", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "hash", this.GetHashCode() } });
            return;
        }

        var interval = this.TickRate > 0 ? 1.0 / this.TickRate : 0.2; // Fallback 5 Hz
        this.accumulator += dt;
        while (this.accumulator >= interval)
        {
            this.OnTick(interval);
            this.accumulator -= interval;
        }
    }

    /// <summary>
    /// Debug-Information ueber registrierte Systeme.
    /// </summary>
    public void PrintSystemInfo()
    {
        DebugLogger.LogServices($"=== Simulation System Info ===");
        DebugLogger.LogServices($"Aktiv: {this.isActive}, TickRate: {this.TickRate}, Systeme: {this.tickables.Count}");

        foreach (var tickable in this.tickables)
        {
            DebugLogger.LogServices($"  - {tickable.Name} ({tickable.GetType().Name})");
        }
    }

    /// <summary>
    /// Prueft, ob ein ITickable-System registriert ist.
    /// </summary>
    /// <returns></returns>
    public bool IsRegistered(ITickable tickable)
    {
        return this.tickables.Contains(tickable);
    }

    /// <summary>
    /// Liefert die Namen aller registrierten Systeme (Debug/Tests).
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<string> GetRegisteredSystemNames()
    {
        var arr = new Godot.Collections.Array<string>();
        foreach (var t in this.tickables)
        {
            arr.Add(t.Name);
        }
        return arr;
    }

    private async Task VerbindeMitGameClockAsync()
    {
        var container = await this.WarteAufServiceContainerAsync();
        if (container == null)
        {
            DebugLogger.Log("debug_simulation", DebugLogger.LogLevel.Error, () => "Simulation: ServiceContainer nicht verfuegbar");
            return;
        }

        this.gameClock = await container.WaitForNamedService<GameClockManager>("GameClockManager");
        if (this.gameClock == null)
        {
            DebugLogger.Log("debug_simulation", DebugLogger.LogLevel.Error, () => "Simulation: GameClockManager nicht gefunden");
            return;
        }

        this.abos.VerbindeSignal(this.gameClock, GameClockManager.SignalName.SimTick, this, nameof(this.OnGameClockSimTick));
    }

    private async Task<ServiceContainer?> WarteAufServiceContainerAsync()
    {
        // Vereinheitlichte Warte-Logik: vermeidet lokale Spin-Loops
        var tree = this.GetTree();
        if (tree == null)
        {
            return ServiceContainer.Instance;
        }

        return await ServiceContainer.WhenAvailableAsync(tree);
    }

    /// <summary>
    /// Bereinigt Singleton-Referenz beim Scene-Restart.
    /// </summary>
    public override void _ExitTree()
    {
        // Alle Signal-Abos loesen
        this.abos.DisposeAll();

        // Clear singleton reference if this is the current instance
        if (ReferenceEquals(Interlocked.CompareExchange(ref instance, null, this), this))
        {
            DebugLogger.Info("debug_simulation", "SimulationSingletonCleared", "Singleton reference cleared", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "hash", this.GetHashCode() } });
        }

        // Clear all registered systems
        this.tickables.Clear();
        this.isActive = false;

        DebugLogger.LogServices($"Simulation: ExitTree cleanup complete - Hash={this.GetHashCode()}");
        base._ExitTree();
    }

    /// <summary>
    /// Force reset singleton (for scene restart).
    /// </summary>
    public static void ResetSingleton()
    {
        var current = Interlocked.Exchange(ref instance, null);
        if (current != null)
        {
            DebugLogger.Warn("debug_simulation", "SimulationForceReset", "Force resetting singleton", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "hash", current.GetHashCode() } });
        }
    }
}

// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

/// <summary>
/// Simulation Manager fuer M8 - Shadow-Mode Produktionssystem
/// Verwaltet alle ITickable Systeme mit fester Tick-Rate ueber den GameClockManager.
/// </summary>
public partial class Simulation : Node
{
	[Export] public float TickRate = 5.0f; // 5 Ticks pro Sekunde

	private readonly List<ITickable> tickables = new();
	private bool isActive;
	private GameClockManager? gameClock;
	private double _accumulator;
	private readonly AboVerwalter _abos = new();

	private static Simulation? _instance;
	public static Simulation? Instance
	{
		get
		{
			var inst = Volatile.Read(ref _instance);
			if (inst != null && !GodotObject.IsInstanceValid(inst))
				return null;
			return inst;
		}
	}
	public bool IstAktiv => isActive;

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
			if (!aktiv)
			{
				return;
			}

			VerlasseSimTickKontext();
			aktiv = false;
		}
	}

	public override async void _Ready()
	{
		var previous = Interlocked.CompareExchange(ref _instance, this, null);
		if (previous != null && !ReferenceEquals(previous, this))
		{
			DebugLogger.Error("debug_simulation", "SimulationMultipleInstances", $"Multiple instances detected!", new System.Collections.Generic.Dictionary<string, object?> { { "existing", previous.GetHashCode() }, { "new", GetHashCode() } });
			QueueFree();
			return;
		}
		DebugLogger.Info("debug_simulation", "SimulationInitialized", $"Initialized as singleton", new System.Collections.Generic.Dictionary<string, object?> { { "hash", GetHashCode() } });

		await VerbindeMitGameClockAsync();
		DebugLogger.LogServices($"Simulation bereit: TickRate={TickRate}, Interval={(TickRate > 0 ? 1.0f / TickRate : 0.2f):F2}s");
	}

	/// <summary>
	/// Registriert ein ITickable System.
	/// </summary>
	public void Register(ITickable tickable)
	{
		if (!tickables.Contains(tickable))
		{
			tickables.Add(tickable);
			DebugLogger.LogServices($"Simulation: {tickable.Name} registriert ({tickables.Count} Systeme)");

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
		if (tickables.Remove(tickable))
		{
			DebugLogger.LogServices($"Simulation: {tickable.Name} entfernt ({tickables.Count} Systeme)");
		}
	}

	/// <summary>
	/// Startet die Simulation.
	/// </summary>
	public void Start()
	{
		if (!isActive)
		{
			isActive = true;
			_accumulator = 0.0;
				DebugLogger.Info("debug_simulation", "SimulationStarted", "Simulation started", new System.Collections.Generic.Dictionary<string, object?> { { "hash", GetHashCode() } });
			DebugLogger.LogServices($"Simulation gestartet: {tickables.Count} Systeme");
		}
		else
		{
				DebugLogger.Warn("debug_simulation", "SimulationAlreadyStarted", "Already started", new System.Collections.Generic.Dictionary<string, object?> { { "hash", GetHashCode() } });
		}
	}

	/// <summary>
	/// Stoppt die Simulation.
	/// </summary>
	public void Stop()
	{
		if (isActive)
		{
			isActive = false;
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
		var interval = TickRate > 0 ? 1.0 / TickRate : 0.2; // Fallback 5 Hz
		OnTick(interval);
	}

	private void OnTick(double dt)
	{
		BetreteSimTickKontext();
		var tickStart = Time.GetUnixTimeFromSystem();

		DebugLogger.LogServices($"Simulation OnTick({dt:F3}): {tickables.Count} systems");

		try
		{
			foreach (var tickable in tickables)
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
			DebugLogger.LogPerf($"Simulation Tick: {duration:F1}ms fuer {tickables.Count} Systeme");
		}
	}

	private void OnGameClockSimTick(double dt)
	{
		if (!isActive)
		{
				DebugLogger.Debug("debug_simulation", "SimulationInactiveSkip", "Not active, skipping tick", new System.Collections.Generic.Dictionary<string, object?> { { "hash", GetHashCode() } });
			return;
		}

		var interval = TickRate > 0 ? 1.0 / TickRate : 0.2; // Fallback 5 Hz
		_accumulator += dt;
		while (_accumulator >= interval)
		{
			OnTick(interval);
			_accumulator -= interval;
		}
	}

	/// <summary>
	/// Debug-Information ueber registrierte Systeme.
	/// </summary>
	public void PrintSystemInfo()
	{
		DebugLogger.LogServices($"=== Simulation System Info ===");
		DebugLogger.LogServices($"Aktiv: {isActive}, TickRate: {TickRate}, Systeme: {tickables.Count}");

		foreach (var tickable in tickables)
		{
			DebugLogger.LogServices($"  - {tickable.Name} ({tickable.GetType().Name})");
		}
	}

	/// <summary>
	/// Prueft, ob ein ITickable-System registriert ist.
	/// </summary>
	public bool IsRegistered(ITickable tickable)
	{
		return tickables.Contains(tickable);
	}

	/// <summary>
	/// Liefert die Namen aller registrierten Systeme (Debug/Tests).
	/// </summary>
	public Godot.Collections.Array<string> GetRegisteredSystemNames()
	{
		var arr = new Godot.Collections.Array<string>();
		foreach (var t in tickables)
		{
			arr.Add(t.Name);
		}
		return arr;
	}

	private async Task VerbindeMitGameClockAsync()
	{
		var container = await WarteAufServiceContainerAsync();
		if (container == null)
		{
			DebugLogger.Log("debug_simulation", DebugLogger.LogLevel.Error, () => "Simulation: ServiceContainer nicht verfuegbar");
			return;
		}

		gameClock = await container.WaitForNamedService<GameClockManager>("GameClockManager");
		if (gameClock == null)
		{
			DebugLogger.Log("debug_simulation", DebugLogger.LogLevel.Error, () => "Simulation: GameClockManager nicht gefunden");
			return;
		}

		_abos.VerbindeSignal(gameClock, GameClockManager.SignalName.SimTick, this, nameof(OnGameClockSimTick));
	}

	private async Task<ServiceContainer?> WarteAufServiceContainerAsync()
	{
		// Vereinheitlichte Warte-Logik: vermeidet lokale Spin-Loops
		var tree = GetTree();
		if (tree == null)
			return ServiceContainer.Instance;
		return await ServiceContainer.WhenAvailableAsync(tree);
	}

	/// <summary>
	/// Bereinigt Singleton-Referenz beim Scene-Restart
	/// </summary>
	public override void _ExitTree()
	{
		// Alle Signal-Abos loesen
		_abos.DisposeAll();

		// Clear singleton reference if this is the current instance
		if (ReferenceEquals(Interlocked.CompareExchange(ref _instance, null, this), this))
		{
			DebugLogger.Info("debug_simulation", "SimulationSingletonCleared", "Singleton reference cleared", new System.Collections.Generic.Dictionary<string, object?> { { "hash", GetHashCode() } });
		}

		// Clear all registered systems
		tickables.Clear();
		isActive = false;

		DebugLogger.LogServices($"Simulation: ExitTree cleanup complete - Hash={GetHashCode()}");
		base._ExitTree();
	}

	/// <summary>
	/// Force reset singleton (for scene restart)
	/// </summary>
	public static void ResetSingleton()
	{
		var current = Interlocked.Exchange(ref _instance, null);
		if (current != null)
			DebugLogger.Warn("debug_simulation", "SimulationForceReset", "Force resetting singleton", new System.Collections.Generic.Dictionary<string, object?> { { "hash", current.GetHashCode() } });
	}
}

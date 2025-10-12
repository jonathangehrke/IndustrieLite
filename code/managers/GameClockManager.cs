// SPDX-License-Identifier: MIT
using Godot;
using System;

/// <summary>
/// Zentrale Spiel-Zeitbasis (GameClock) mit fester Tickrate.
/// - Liefert Fixed-Step Ticks (z. B. 20 Hz) über Signal <see cref="SimTick"/>
/// - Unterstützt Pause/Resume und Zeitfaktor (TimeScale)
/// - Event-getrieben: Andere Manager/Subsysteme können auf SimTick reagieren
/// Phase 1: Läuft parallel zu bestehenden Systemen (keine Migration erzwungen)
/// </summary>
public partial class GameClockManager : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    // --- Signale ---
    // Festes Simulations-Tick-Signal. Übergibt die Tick-Dauer in Sekunden.
    [Signal] public delegate void SimTickEventHandler(double dt);
    // Statusänderungen für UI/Debug
    [Signal] public delegate void PausedChangedEventHandler(bool paused);
    [Signal] public delegate void TimeScaleChangedEventHandler(double timeScale);

    // --- Konfiguration ---
    [Export] public bool Enabled { get; set; } = true; // Feature-Flag: GameClock aktiv?
    [Export] public double TickRate { get; set; } = 20.0; // Ticks pro Sekunde (z. B. 20 Hz)
    [Export] public bool DebugLogs { get; set; } = false; // Optional: Debug-Ausgaben

    // --- Laufzeitzustand ---
    private double _accumulator = 0.0; // Akkumulierte (skalierte) Zeit
    private double _timeScale = 1.0;   // Zeitfaktor (1.0 = normal)
    private bool _paused = false;      // Pausenstatus
    private ulong _tickCounter = 0;    // Anzahl gesendeter Ticks (für Debug)
    private double _totalSimTime = 0.0; // Summe aller Sim-Zeit in Sekunden

    /// <summary>Aktueller Pausenstatus (nur lesen).</summary>
    public bool IsPaused => _paused;

    /// <summary>Aktuelle Zeitfaktor (nur lesen).</summary>
    public double TimeScale => _timeScale;

    /// <summary>Dauer eines Fix-Ticks in Sekunden.</summary>
    public double TickInterval => TickRate > 0 ? 1.0 / TickRate : 0.05; // Fallback 20ms

    /// <summary>Gesamte Simulationszeit (nur GameClock-basiert).</summary>
    public double TotalSimTime => _totalSimTime;

    /// <summary>
    /// Interpolationsfaktor zwischen letzten und nächsten Fixed-Tick (0..1).
    /// Nützlich für glatte Visuals bei Fixed-Step Logik.
    /// </summary>
    public double InterpolationsAlpha
    {
        get
        {
            var interval = TickInterval;
            if (interval <= 0.0) return 0.0;
            var a = _accumulator / interval;
            if (a < 0.0) return 0.0;
            if (a > 1.0) return 1.0;
            return a;
        }
    }

    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(ServiceNames.GameClockManager, this);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_services", "GameClockRegisterFailed", ex.Message);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!Enabled || _paused || TickRate <= 0)
            return;

        // Skalierte Zeit akkumulieren (eigene TimeScale, Engine.TimeScale bleibt unberührt)
        _accumulator += delta * Math.Max(0.0, _timeScale);

        // Fixed-Step Ticks ausführen
        while (_accumulator >= TickInterval)
        {
            EmitSignal(SignalName.SimTick, TickInterval);
            _tickCounter++;
            _totalSimTime += TickInterval;
            _accumulator -= TickInterval;

            if (DebugLogs && (_tickCounter % 100 == 0))
            {
                DebugLogger.LogGameClock(() => $"GameClock Tick #{_tickCounter} – TotalSimTime={_totalSimTime:F2}s");
            }
        }
    }

    // --- Öffentliche API ---

    /// <summary>Setzt den Zeitfaktor (>= 0). 1.0 = Normalgeschwindigkeit.</summary>
    public void SetTimeScale(double scale)
    {
        var clamped = Math.Max(0.0, scale);
        if (Math.Abs(clamped - _timeScale) > 0.0001)
        {
            _timeScale = clamped;
            EmitSignal(SignalName.TimeScaleChanged, _timeScale);
            if (DebugLogs) DebugLogger.LogGameClock(() => $"GameClock: TimeScale -> {_timeScale:F2}");
        }
    }

    /// <summary>Pausiert oder setzt die Simulation fort (nur GameClock).</summary>
    public void SetPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        EmitSignal(SignalName.PausedChanged, _paused);
        if (DebugLogs) DebugLogger.LogGameClock(_paused ? "GameClock: Paused" : "GameClock: Resumed");
    }

    public readonly struct GameClockState
    {
        public bool Paused { get; }
        public double TimeScale { get; }
        public double TotalSimTime { get; }
        public double Accumulator { get; }
        public double TickRate { get; }

        public GameClockState(bool paused, double timeScale, double totalSimTime, double accumulator, double tickRate)
        {
            Paused = paused;
            TimeScale = timeScale;
            TotalSimTime = totalSimTime;
            Accumulator = accumulator;
            TickRate = tickRate;
        }
    }

    public GameClockState CaptureState()
    {
        return new GameClockState(_paused, _timeScale, _totalSimTime, _accumulator, TickRate);
    }

    public void RestoreState(GameClockState state)
    {
        TickRate = state.TickRate;
        _totalSimTime = Math.Max(0.0, state.TotalSimTime);
        _accumulator = Math.Max(0.0, state.Accumulator);
        SetTimeScale(state.TimeScale);
        SetPaused(state.Paused);
    }

    /// <summary>Wechselt den Pausenstatus.</summary>
    public void TogglePause() => SetPaused(!_paused);
}


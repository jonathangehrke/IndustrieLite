// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;
using Godot;

/// <summary>
/// Zentrale Spiel-Zeitbasis (GameClock) mit fester Tickrate.
/// - Liefert Fixed-Step Ticks (z. B. 20 Hz) über Signal <see cref="SimTick"/>
/// - Unterstützt Pause/Resume und Zeitfaktor (TimeScale)
/// - Event-getrieben: Andere Manager/Subsysteme können auf SimTick reagieren
/// Phase 1: Läuft parallel zu bestehenden Systemen (keine Migration erzwungen).
/// </summary>
public partial class GameClockManager : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    // --- Signale ---
    // Festes Simulations-Tick-Signal. Übergibt die Tick-Dauer in Sekunden.
    [Signal]
    public delegate void SimTickEventHandler(double dt);

    // Statusänderungen für UI/Debug
    [Signal]
    public delegate void PausedChangedEventHandler(bool paused);

    [Signal]
    public delegate void TimeScaleChangedEventHandler(double timeScale);

    // --- Konfiguration ---
    [Export]
    public bool Enabled { get; set; } = true; // Feature-Flag: GameClock aktiv?

    [Export]
    public double TickRate { get; set; } = 20.0; // Ticks pro Sekunde (z. B. 20 Hz)

    [Export]
    public bool DebugLogs { get; set; } = false; // Optional: Debug-Ausgaben

    // --- Laufzeitzustand ---
    private double accumulator = 0.0; // Akkumulierte (skalierte) Zeit
    private double timeScale = 1.0;   // Zeitfaktor (1.0 = normal)
    private bool paused = false;      // Pausenstatus
    private ulong tickCounter = 0;    // Anzahl gesendeter Ticks (für Debug)
    private double totalSimTime = 0.0; // Summe aller Sim-Zeit in Sekunden

    /// <summary>Gets a value indicating whether aktueller Pausenstatus (nur lesen).</summary>
    public bool IsPaused => this.paused;

    /// <summary>Gets aktuelle Zeitfaktor (nur lesen).</summary>
    public double TimeScale => this.timeScale;

    /// <summary>Gets dauer eines Fix-Ticks in Sekunden.</summary>
    public double TickInterval => this.TickRate > 0 ? 1.0 / this.TickRate : 0.05; // Fallback 20ms

    /// <summary>Gets gesamte Simulationszeit (nur GameClock-basiert).</summary>
    public double TotalSimTime => this.totalSimTime;

    /// <summary>
    /// Gets interpolationsfaktor zwischen letzten und nächsten Fixed-Tick (0..1).
    /// Nützlich für glatte Visuals bei Fixed-Step Logik.
    /// </summary>
    public double InterpolationsAlpha
    {
        get
        {
            var interval = this.TickInterval;
            if (interval <= 0.0)
            {
                return 0.0;
            }

            var a = this.accumulator / interval;
            if (a < 0.0)
            {
                return 0.0;
            }

            if (a > 1.0)
            {
                return 1.0;
            }

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
        if (!this.Enabled || this.paused || this.TickRate <= 0)
        {
            return;
        }

        // Skalierte Zeit akkumulieren (eigene TimeScale, Engine.TimeScale bleibt unberührt)
        this.accumulator += delta * Math.Max(0.0, this.timeScale);

        // Fixed-Step Ticks ausführen
        while (this.accumulator >= this.TickInterval)
        {
            this.EmitSignal(SignalName.SimTick, this.TickInterval);
            this.tickCounter++;
            this.totalSimTime += this.TickInterval;
            this.accumulator -= this.TickInterval;

            if (this.DebugLogs && (this.tickCounter % 100 == 0))
            {
                DebugLogger.LogGameClock(() => $"GameClock Tick #{this.tickCounter} – TotalSimTime={this.totalSimTime:F2}s");
            }
        }
    }

    // --- Öffentliche API ---

    /// <summary>Setzt den Zeitfaktor (>= 0). 1.0 = Normalgeschwindigkeit.</summary>
    public void SetTimeScale(double scale)
    {
        var clamped = Math.Max(0.0, scale);
        if (Math.Abs(clamped - this.timeScale) > 0.0001)
        {
            this.timeScale = clamped;
            this.EmitSignal(SignalName.TimeScaleChanged, this.timeScale);
            if (this.DebugLogs)
            {
                DebugLogger.LogGameClock(() => $"GameClock: TimeScale -> {this.timeScale:F2}");
            }
        }
    }

    /// <summary>Pausiert oder setzt die Simulation fort (nur GameClock).</summary>
    public void SetPaused(bool paused)
    {
        if (this.paused == paused)
        {
            return;
        }

        this.paused = paused;
        this.EmitSignal(SignalName.PausedChanged, this.paused);
        if (this.DebugLogs)
        {
            DebugLogger.LogGameClock(this.paused ? "GameClock: Paused" : "GameClock: Resumed");
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct GameClockState
    {
        public bool Paused { get; }

        public double TimeScale { get; }

        public double TotalSimTime { get; }

        public double Accumulator { get; }

        public double TickRate { get; }

        public GameClockState(bool paused, double timeScale, double totalSimTime, double accumulator, double tickRate)
        {
            this.Paused = paused;
            this.TimeScale = timeScale;
            this.TotalSimTime = totalSimTime;
            this.Accumulator = accumulator;
            this.TickRate = tickRate;
        }
    }

    public GameClockState CaptureState()
    {
        return new GameClockState(this.paused, this.timeScale, this.totalSimTime, this.accumulator, this.TickRate);
    }

    public void RestoreState(GameClockState state)
    {
        this.TickRate = state.TickRate;
        this.totalSimTime = Math.Max(0.0, state.TotalSimTime);
        this.accumulator = Math.Max(0.0, state.Accumulator);
        this.SetTimeScale(state.TimeScale);
        this.SetPaused(state.Paused);
    }

    /// <summary>Wechselt den Pausenstatus.</summary>
    public void TogglePause() => this.SetPaused(!this.paused);
}


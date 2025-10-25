// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// GameTimeManager: Kalender-Zeit auf Basis der GameClock (SimTick)
/// - 6 Minuten pro Monat => 12 Sekunden pro Tag (Standard)
/// - Startdatum konfigurierbar
/// - Emittiert Events via EventHub: DayChanged, MonthChanged, YearChanged, DateChanged.
/// </summary>
public partial class GameTimeManager : Node, ITickable, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    [Export]
    public int StartYear { get; set; } = 2015;

    [Export]
    public int StartMonth { get; set; } = 1;

    [Export]
    public int StartDay { get; set; } = 1;

    [Export]
    public double SecondsPerDay { get; set; } = 12.0;

    [Export]
    public bool DebugLogs { get; set; } = false;

    private DateTime currentDate;
    private double accum;
    private EventHub? eventHub;

    // ITickable-Name nur ueber explizite Interface-Implementierung, damit Node.Name weiterhin setzbar bleibt

    /// <inheritdoc/>
    string ITickable.Name => "GameTimeManager";

    public DateTime CurrentDate => this.currentDate;

    /// <inheritdoc/>
    public override void _Ready()
    {
        this.currentDate = new DateTime(this.StartYear, this.StartMonth, this.StartDay);
        this.accum = 0.0;

        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(GameTimeManager), this);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("debug_gameclock", "RegisterWithServiceContainerFailed", ex.Message);
            }
        }

        // Dependencies (EventHub, Simulation) werden via Initialize() gesetzt
        // Initiales Datum wird nach Initialize() emittiert
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        try
        {
            Simulation.Instance?.Unregister(this);
        }
        catch
        {
        }
        base._ExitTree();
    }

    /// <inheritdoc/>
    public void Tick(double dt)
    {
        if (this.SecondsPerDay <= 0)
        {
            return;
        }

        this.accum += dt;
        while (this.accum >= this.SecondsPerDay)
        {
            this.accum -= this.SecondsPerDay;
            DebugLogger.Debug("debug_gameclock", "AdvanceOneDay", $"Advancing one day", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "date", this.currentDate.ToString("dd.MM.yyyy") } });
            this.AdvanceOneDay();
        }
    }

    private void AdvanceOneDay()
    {
        var before = this.currentDate;
        this.currentDate = this.currentDate.AddDays(1);

        // DayChanged
        this.eventHub?.EmitSignal(EventHub.SignalName.DayChanged, this.currentDate.Year, this.currentDate.Month, this.currentDate.Day);

        // Month/Year Changed
        if (this.currentDate.Month != before.Month || this.currentDate.Year != before.Year)
        {
            this.eventHub?.EmitSignal(EventHub.SignalName.MonthChanged, this.currentDate.Year, this.currentDate.Month);
        }
        if (this.currentDate.Year != before.Year)
        {
            this.eventHub?.EmitSignal(EventHub.SignalName.YearChanged, this.currentDate.Year);
        }

        this.EmitDateChanged();
        if (this.DebugLogs)
        {
            DebugLogger.Log("debug_gameclock", DebugLogger.LogLevel.Info, () => $"GameTime: {this.currentDate:dd.MM.yyyy}");
        }
    }

    private void EmitDateChanged()
    {
        var s = this.currentDate.ToString("dd.MM.yyyy");
        DebugLogger.Debug("debug_gameclock", "EmitDateChanged", $"EmitDateChanged", new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "date", s }, { "eventHub", this.eventHub != null } });
        this.eventHub?.EmitSignal(EventHub.SignalName.DateChanged, s);
    }

    // Public API
    public void ResetToStart()
    {
        this.currentDate = new DateTime(this.StartYear, this.StartMonth, this.StartDay);
        this.accum = 0.0;
        this.EmitDateChanged();
    }

    public void SetDate(int year, int month, int day)
    {
        try
        {
            this.currentDate = new DateTime(year, month, day);
            this.accum = 0.0;
            this.EmitDateChanged();
        }
        catch
        { /* ignore invalid */
        }
    }

    public string GetCurrentDateString()
    {
        return this.currentDate.ToString("dd.MM.yyyy");
    }
}


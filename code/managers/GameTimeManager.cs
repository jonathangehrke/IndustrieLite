// SPDX-License-Identifier: MIT
using Godot;
using System;

/// <summary>
/// GameTimeManager: Kalender-Zeit auf Basis der GameClock (SimTick)
/// - 6 Minuten pro Monat => 12 Sekunden pro Tag (Standard)
/// - Startdatum konfigurierbar
/// - Emittiert Events via EventHub: DayChanged, MonthChanged, YearChanged, DateChanged
/// </summary>
public partial class GameTimeManager : Node, ITickable, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    [Export] public int StartYear { get; set; } = 2015;
    [Export] public int StartMonth { get; set; } = 1;
    [Export] public int StartDay { get; set; } = 1;
    [Export] public double SecondsPerDay { get; set; } = 12.0;
    [Export] public bool DebugLogs { get; set; } = false;

    private DateTime _currentDate;
    private double _accum;
    private EventHub? _eventHub;
    // ITickable-Name nur ueber explizite Interface-Implementierung, damit Node.Name weiterhin setzbar bleibt
    string ITickable.Name => "GameTimeManager";

    public DateTime CurrentDate => _currentDate;

    public override void _Ready()
    {
        _currentDate = new DateTime(StartYear, StartMonth, StartDay);
        _accum = 0.0;

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

    public override void _ExitTree()
    {
        try { Simulation.Instance?.Unregister(this); } catch { }
        base._ExitTree();
    }
    public void Tick(double dt)
    {
        if (SecondsPerDay <= 0) return;
        _accum += dt;
        while (_accum >= SecondsPerDay)
        {
            _accum -= SecondsPerDay;
            DebugLogger.Debug("debug_gameclock", "AdvanceOneDay", $"Advancing one day", new System.Collections.Generic.Dictionary<string, object?> { { "date", _currentDate.ToString("dd.MM.yyyy") } });
            AdvanceOneDay();
        }
    }


    private void AdvanceOneDay()
    {
        var before = _currentDate;
        _currentDate = _currentDate.AddDays(1);

        // DayChanged
        _eventHub?.EmitSignal(EventHub.SignalName.DayChanged, _currentDate.Year, _currentDate.Month, _currentDate.Day);

        // Month/Year Changed
        if (_currentDate.Month != before.Month || _currentDate.Year != before.Year)
        {
            _eventHub?.EmitSignal(EventHub.SignalName.MonthChanged, _currentDate.Year, _currentDate.Month);
        }
        if (_currentDate.Year != before.Year)
        {
            _eventHub?.EmitSignal(EventHub.SignalName.YearChanged, _currentDate.Year);
        }

        EmitDateChanged();
        if (DebugLogs)
        {
            DebugLogger.Log("debug_gameclock", DebugLogger.LogLevel.Info, () => $"GameTime: {_currentDate:dd.MM.yyyy}");
        }
    }

    private void EmitDateChanged()
    {
        var s = _currentDate.ToString("dd.MM.yyyy");
        DebugLogger.Debug("debug_gameclock", "EmitDateChanged", $"EmitDateChanged", new System.Collections.Generic.Dictionary<string, object?> { { "date", s }, { "eventHub", _eventHub != null } });
        _eventHub?.EmitSignal(EventHub.SignalName.DateChanged, s);
    }

    // Public API
    public void ResetToStart()
    {
        _currentDate = new DateTime(StartYear, StartMonth, StartDay);
        _accum = 0.0;
        EmitDateChanged();
    }

    public void SetDate(int year, int month, int day)
    {
        try
        {
            _currentDate = new DateTime(year, month, day);
            _accum = 0.0;
            EmitDateChanged();
        }
        catch { /* ignore invalid */ }
    }

    public string GetCurrentDateString()
    {
        return _currentDate.ToString("dd.MM.yyyy");
    }
}


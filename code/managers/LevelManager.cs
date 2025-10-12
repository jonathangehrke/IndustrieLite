// SPDX-License-Identifier: MIT
using Godot;
using System;

/// <summary>
/// LevelManager: Verwaltet das Level-System und Progression durch Marktverkäufe
/// </summary>
public partial class LevelManager : Node, ILifecycleScope
{
	public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

	// Level-System Konstanten
	public const int MIN_LEVEL = 1;
	public const int MAX_LEVEL = 3;
	private const double LEVEL_2_THRESHOLD = 250.0;
	private const double LEVEL_3_THRESHOLD = 1250.0;

	// Zustand
	private int _currentLevel = MIN_LEVEL;
	private double _totalMarketRevenue = 0.0;

	// Dependencies
	private EventHub? _eventHub;

	// Properties
	public int CurrentLevel => _currentLevel;
	public double TotalMarketRevenue => _totalMarketRevenue;

	public override void _Ready()
	{
		// Registrierung erfolgt jetzt in Initialize() statt hier
	}

	/// <summary>
	/// Initialisiert den LevelManager mit EventHub
	/// </summary>
	public void Initialize(EventHub? eventHub)
	{
		_eventHub = eventHub;

		// Self-registration in ServiceContainer (verschoben von _Ready hierher)
		var sc = ServiceContainer.Instance;
		if (sc != null)
		{
			try
			{
				sc.RegisterNamedService(nameof(LevelManager), this);
				DebugLogger.Info("debug_services", "LevelManagerRegistered", "LevelManager registered in ServiceContainer");
			}
			catch (Exception ex)
			{
				DebugLogger.Error("debug_services", "LevelManagerRegistrationFailed", ex.Message);
			}
		}

		DebugLogger.Info("debug_progression", "LevelManagerInitialized", $"Level: {_currentLevel}, Revenue: {_totalMarketRevenue:F2}");
	}

	/// <summary>
	/// Fügt Marktverkaufs-Umsatz hinzu und prüft Level-Aufstieg
	/// </summary>
	public void AddMarketRevenue(double amount)
	{
		if (amount <= 0.0)
			return;

		_totalMarketRevenue += amount;
		DebugLogger.Info("debug_progression", "MarketRevenueAdded", $"Added {amount:F2}, Total: {_totalMarketRevenue:F2}");
		DebugLogger.LogEconomy($"LevelManager.AddMarketRevenue: Added {amount:F2}, Total now: {_totalMarketRevenue:F2}, Current Level: {_currentLevel}");

		// Event für UI-Update
		if (_eventHub != null)
		{
			_eventHub.EmitSignal(EventHub.SignalName.MarketRevenueChanged, _totalMarketRevenue, _currentLevel);
			DebugLogger.LogEconomy($"LevelManager: Emitted MarketRevenueChanged signal - Revenue: {_totalMarketRevenue:F2}, Level: {_currentLevel}");
		}
		else
		{
			DebugLogger.LogEconomy("LevelManager: WARNING - EventHub is null, cannot emit signal");
		}

		// Level-Aufstieg prüfen
		CheckLevelUp();
	}

	/// <summary>
	/// Prüft, ob ein Level-Aufstieg möglich ist
	/// </summary>
	private void CheckLevelUp()
	{
		int newLevel = CalculateLevel(_totalMarketRevenue);
		if (newLevel > _currentLevel && newLevel <= MAX_LEVEL)
		{
			_currentLevel = newLevel;
			DebugLogger.Info("debug_progression", "LevelUp", $"Level aufgestiegen auf {_currentLevel}!",
				new System.Collections.Generic.Dictionary<string, object?> { { "revenue", _totalMarketRevenue } });

			// Event emittieren
			_eventHub?.EmitSignal(EventHub.SignalName.LevelChanged, _currentLevel);
		}
	}

	/// <summary>
	/// Berechnet das Level basierend auf Gesamtumsatz
	/// </summary>
	private int CalculateLevel(double revenue)
	{
		if (revenue >= LEVEL_3_THRESHOLD)
			return 3;
		if (revenue >= LEVEL_2_THRESHOLD)
			return 2;
		return 1;
	}

	/// <summary>
	/// Prüft, ob ein bestimmtes Level freigeschaltet ist
	/// </summary>
	public bool IsLevelUnlocked(int level)
	{
		return _currentLevel >= level;
	}

	/// <summary>
	/// Gibt den Fortschritt zum nächsten Level zurück (0.0 - 1.0)
	/// </summary>
	public double GetProgressToNextLevel()
	{
		if (_currentLevel >= MAX_LEVEL)
			return 1.0; // Max-Level erreicht

		double currentThreshold = GetLevelThreshold(_currentLevel);
		double nextThreshold = GetLevelThreshold(_currentLevel + 1);

		if (nextThreshold <= currentThreshold)
			return 1.0;

		double progress = (_totalMarketRevenue - currentThreshold) / (nextThreshold - currentThreshold);
		return Math.Clamp(progress, 0.0, 1.0);
	}

	/// <summary>
	/// Gibt den Umsatz-Schwellwert für ein Level zurück
	/// </summary>
	public double GetLevelThreshold(int level)
	{
		return level switch
		{
			1 => 0.0,
			2 => LEVEL_2_THRESHOLD,
			3 => LEVEL_3_THRESHOLD,
			_ => double.MaxValue
		};
	}

	/// <summary>
	/// Gibt den verbleibenden Umsatz bis zum nächsten Level zurück
	/// </summary>
	public double GetRevenueToNextLevel()
	{
		if (_currentLevel >= MAX_LEVEL)
			return 0.0;

		double nextThreshold = GetLevelThreshold(_currentLevel + 1);
		return Math.Max(0.0, nextThreshold - _totalMarketRevenue);
	}

	/// <summary>
	/// Setzt das Level (für SaveLoad)
	/// </summary>
	public void SetLevel(int level)
	{
		if (level < MIN_LEVEL || level > MAX_LEVEL)
		{
			DebugLogger.Warn("debug_progression", "InvalidLevelSet", $"Versuch, ungültiges Level zu setzen: {level}");
			return;
		}

		_currentLevel = level;
		DebugLogger.Info("debug_progression", "LevelSet", $"Level gesetzt auf {_currentLevel}");
		_eventHub?.EmitSignal(EventHub.SignalName.LevelChanged, _currentLevel);
	}

	/// <summary>
	/// Setzt den Markt-Umsatz (für SaveLoad)
	/// </summary>
	public void SetMarketRevenue(double revenue)
	{
		_totalMarketRevenue = Math.Max(0.0, revenue);
		DebugLogger.Info("debug_progression", "MarketRevenueSet", $"Markt-Umsatz gesetzt auf {_totalMarketRevenue:F2}");
		_eventHub?.EmitSignal(EventHub.SignalName.MarketRevenueChanged, _totalMarketRevenue, _currentLevel);
	}

	/// <summary>
	/// Setzt Level und Umsatz gleichzeitig (für SaveLoad)
	/// </summary>
	public void SetLevelAndRevenue(int level, double revenue)
	{
		if (level < MIN_LEVEL || level > MAX_LEVEL)
		{
			DebugLogger.Warn("debug_progression", "InvalidLevelSet", $"Versuch, ungültiges Level zu setzen: {level}");
			return;
		}

		_currentLevel = level;
		_totalMarketRevenue = Math.Max(0.0, revenue);

		DebugLogger.Info("debug_progression", "LevelAndRevenueSet",
			$"Level und Umsatz gesetzt: Level {_currentLevel}, Revenue {_totalMarketRevenue:F2}");

		_eventHub?.EmitSignal(EventHub.SignalName.LevelChanged, _currentLevel);
		_eventHub?.EmitSignal(EventHub.SignalName.MarketRevenueChanged, _totalMarketRevenue, _currentLevel);
	}

	/// <summary>
	/// Setzt alles zurück (für New Game)
	/// </summary>
	public void Reset()
	{
		_currentLevel = MIN_LEVEL;
		_totalMarketRevenue = 0.0;
		DebugLogger.Info("debug_progression", "LevelManagerReset", "Level-System zurückgesetzt");
		_eventHub?.EmitSignal(EventHub.SignalName.LevelChanged, _currentLevel);
		_eventHub?.EmitSignal(EventHub.SignalName.MarketRevenueChanged, _totalMarketRevenue, _currentLevel);
	}
}

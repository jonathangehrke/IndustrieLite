// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// LevelManager: Verwaltet das Level-System und Progression durch Marktverkäufe.
/// </summary>
public partial class LevelManager : Node, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    // Level-System Konstanten
    public const int MINLEVEL = 1;
    public const int MAXLEVEL = 3;
    private const double LEVEL2THRESHOLD = 250.0;
    private const double LEVEL3THRESHOLD = 1250.0;

    // Zustand
    private int currentLevel = MINLEVEL;
    private double totalMarketRevenue = 0.0;

    // Dependencies
    private EventHub? eventHub;

    // Properties
    public int CurrentLevel => this.currentLevel;

    public double TotalMarketRevenue => this.totalMarketRevenue;

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Registrierung erfolgt jetzt in Initialize() statt hier
    }

    /// <summary>
    /// Initialisiert den LevelManager mit EventHub.
    /// </summary>
    public void Initialize(EventHub? eventHub)
    {
        this.eventHub = eventHub;

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

        DebugLogger.Info("debug_progression", "LevelManagerInitialized", $"Level: {this.currentLevel}, Revenue: {this.totalMarketRevenue:F2}");
    }

    /// <summary>
    /// Fügt Marktverkaufs-Umsatz hinzu und prüft Level-Aufstieg.
    /// </summary>
    public void AddMarketRevenue(double amount)
    {
        if (amount <= 0.0)
        {
            return;
        }

        this.totalMarketRevenue += amount;
        DebugLogger.Info("debug_progression", "MarketRevenueAdded", $"Added {amount:F2}, Total: {this.totalMarketRevenue:F2}");
        DebugLogger.LogEconomy($"LevelManager.AddMarketRevenue: Added {amount:F2}, Total now: {this.totalMarketRevenue:F2}, Current Level: {this.currentLevel}");

        // Event für UI-Update
        if (this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.MarketRevenueChanged, this.totalMarketRevenue, this.currentLevel);
            DebugLogger.LogEconomy($"LevelManager: Emitted MarketRevenueChanged signal - Revenue: {this.totalMarketRevenue:F2}, Level: {this.currentLevel}");
        }
        else
        {
            DebugLogger.LogEconomy("LevelManager: WARNING - EventHub is null, cannot emit signal");
        }

        // Level-Aufstieg prüfen
        this.CheckLevelUp();
    }

    /// <summary>
    /// Prüft, ob ein Level-Aufstieg möglich ist.
    /// </summary>
    private void CheckLevelUp()
    {
        int newLevel = this.CalculateLevel(this.totalMarketRevenue);
        if (newLevel > this.currentLevel && newLevel <= MAXLEVEL)
        {
            this.currentLevel = newLevel;
            DebugLogger.Info("debug_progression", "LevelUp", $"Level aufgestiegen auf {this.currentLevel}!",
                new System.Collections.Generic.Dictionary<string, object?>(StringComparer.Ordinal) { { "revenue", this.totalMarketRevenue } });

            // Event emittieren
            this.eventHub?.EmitSignal(EventHub.SignalName.LevelChanged, this.currentLevel);
        }
    }

    /// <summary>
    /// Berechnet das Level basierend auf Gesamtumsatz.
    /// </summary>
    private int CalculateLevel(double revenue)
    {
        if (revenue >= LEVEL3THRESHOLD)
        {
            return 3;
        }

        if (revenue >= LEVEL2THRESHOLD)
        {
            return 2;
        }

        return 1;
    }

    /// <summary>
    /// Prüft, ob ein bestimmtes Level freigeschaltet ist.
    /// </summary>
    /// <returns></returns>
    public bool IsLevelUnlocked(int level)
    {
        return this.currentLevel >= level;
    }

    /// <summary>
    /// Gibt den Fortschritt zum nächsten Level zurück (0.0 - 1.0).
    /// </summary>
    /// <returns></returns>
    public double GetProgressToNextLevel()
    {
        if (this.currentLevel >= MAXLEVEL)
        {
            return 1.0; // Max-Level erreicht
        }

        double currentThreshold = this.GetLevelThreshold(this.currentLevel);
        double nextThreshold = this.GetLevelThreshold(this.currentLevel + 1);

        if (nextThreshold <= currentThreshold)
        {
            return 1.0;
        }

        double progress = (this.totalMarketRevenue - currentThreshold) / (nextThreshold - currentThreshold);
        return Math.Clamp(progress, 0.0, 1.0);
    }

    /// <summary>
    /// Gibt den Umsatz-Schwellwert für ein Level zurück.
    /// </summary>
    /// <returns></returns>
    public double GetLevelThreshold(int level)
    {
        return level switch
        {
            1 => 0.0,
            2 => LEVEL2THRESHOLD,
            3 => LEVEL3THRESHOLD,
            _ => double.MaxValue,
        };
    }

    /// <summary>
    /// Gibt den verbleibenden Umsatz bis zum nächsten Level zurück.
    /// </summary>
    /// <returns></returns>
    public double GetRevenueToNextLevel()
    {
        if (this.currentLevel >= MAXLEVEL)
        {
            return 0.0;
        }

        double nextThreshold = this.GetLevelThreshold(this.currentLevel + 1);
        return Math.Max(0.0, nextThreshold - this.totalMarketRevenue);
    }

    /// <summary>
    /// Setzt das Level (für SaveLoad).
    /// </summary>
    public void SetLevel(int level)
    {
        if (level < MINLEVEL || level > MAXLEVEL)
        {
            DebugLogger.Warn("debug_progression", "InvalidLevelSet", $"Versuch, ungültiges Level zu setzen: {level}");
            return;
        }

        this.currentLevel = level;
        DebugLogger.Info("debug_progression", "LevelSet", $"Level gesetzt auf {this.currentLevel}");
        this.eventHub?.EmitSignal(EventHub.SignalName.LevelChanged, this.currentLevel);
    }

    /// <summary>
    /// Setzt den Markt-Umsatz (für SaveLoad).
    /// </summary>
    public void SetMarketRevenue(double revenue)
    {
        this.totalMarketRevenue = Math.Max(0.0, revenue);
        DebugLogger.Info("debug_progression", "MarketRevenueSet", $"Markt-Umsatz gesetzt auf {this.totalMarketRevenue:F2}");
        this.eventHub?.EmitSignal(EventHub.SignalName.MarketRevenueChanged, this.totalMarketRevenue, this.currentLevel);
    }

    /// <summary>
    /// Setzt Level und Umsatz gleichzeitig (für SaveLoad).
    /// </summary>
    public void SetLevelAndRevenue(int level, double revenue)
    {
        if (level < MINLEVEL || level > MAXLEVEL)
        {
            DebugLogger.Warn("debug_progression", "InvalidLevelSet", $"Versuch, ungültiges Level zu setzen: {level}");
            return;
        }

        this.currentLevel = level;
        this.totalMarketRevenue = Math.Max(0.0, revenue);

        DebugLogger.Info("debug_progression", "LevelAndRevenueSet",
            $"Level und Umsatz gesetzt: Level {this.currentLevel}, Revenue {this.totalMarketRevenue:F2}");

        this.eventHub?.EmitSignal(EventHub.SignalName.LevelChanged, this.currentLevel);
        this.eventHub?.EmitSignal(EventHub.SignalName.MarketRevenueChanged, this.totalMarketRevenue, this.currentLevel);
    }

    /// <summary>
    /// Setzt alles zurück (für New Game).
    /// </summary>
    public void Reset()
    {
        this.currentLevel = MINLEVEL;
        this.totalMarketRevenue = 0.0;
        DebugLogger.Info("debug_progression", "LevelManagerReset", "Level-System zurückgesetzt");
        this.eventHub?.EmitSignal(EventHub.SignalName.LevelChanged, this.currentLevel);
        this.eventHub?.EmitSignal(EventHub.SignalName.MarketRevenueChanged, this.totalMarketRevenue, this.currentLevel);
    }
}

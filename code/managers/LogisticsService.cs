// SPDX-License-Identifier: MIT
using Godot;
using System;

/// <summary>
/// Service für Logistik-Upgrades und Kostenberechnungen
/// Migriert aus ProductionBuildingPanel.gd - enthält Upgrade-Logik für Kapazität und Geschwindigkeit
/// </summary>
public partial class LogisticsService : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    private EconomyManager? economyManager;
    private EventHub? eventHub;

    // Upgrade constants
    private const int CapacityStep = 5;
    private const float SpeedStep = 8.0f;
    private const int CapacityBase = 5;
    private const float SpeedBase = 32.0f;
    private const float CapacityBaseCost = 100.0f;
    private const float SpeedBaseCost = 150.0f;

    private bool _isInitialized = false;

    public override void _Ready()
    {
        // No self-registration - managed by DIContainer (Clean Architecture)
        // Dependencies are injected via Initialize() method
    }

    /// <summary>
    /// Gets the current logistics settings for a building
    /// </summary>
    public LogisticsUpgradeData GetLogisticsSettings(Building building)
    {
        if (building == null)
            return new LogisticsUpgradeData();

        var capacity = CapacityBase;
        var speed = SpeedBase;

        try
        {
            // Read from building properties directly
            capacity = building.LogisticsTruckCapacity;
            speed = building.LogisticsTruckSpeed;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"LogisticsService: Error reading logistics settings: {ex.Message}");
        }

        return new LogisticsUpgradeData
        {
            CurrentCapacity = capacity,
            CurrentSpeed = speed,
            CapacityUpgradeCost = CalculateCapacityUpgradeCost(capacity),
            SpeedUpgradeCost = CalculateSpeedUpgradeCost(speed),
            CanAffordCapacityUpgrade = CanAfford(CalculateCapacityUpgradeCost(capacity)),
            CanAffordSpeedUpgrade = CanAfford(CalculateSpeedUpgradeCost(speed))
        };
    }

    /// <summary>
    /// Upgrades the capacity of a building's logistics
    /// </summary>
    public bool UpgradeCapacity(Building building)
    {
        if (building == null || economyManager == null)
            return false;

        var currentCapacity = GetCurrentCapacity(building);
        var cost = CalculateCapacityUpgradeCost(currentCapacity);

        if (!economyManager.SpendMoney(cost))
            return false;

        var newCapacity = Math.Max(1, currentCapacity + CapacityStep);
        building.LogisticsTruckCapacity = newCapacity;

        DebugLogger.LogServices($"LogisticsService: Upgraded capacity for {building.Name} from {currentCapacity} to {newCapacity} (cost: {cost})");

        // Emit logistics upgrade event
        EmitLogisticsUpgradeEvent(building, "capacity", currentCapacity, newCapacity, cost);

        return true;
    }

    /// <summary>
    /// Upgrades the speed of a building's logistics
    /// </summary>
    public bool UpgradeSpeed(Building building)
    {
        if (building == null || economyManager == null)
            return false;

        var currentSpeed = GetCurrentSpeed(building);
        var cost = CalculateSpeedUpgradeCost(currentSpeed);

        if (!economyManager.SpendMoney(cost))
            return false;

        var newSpeed = Math.Max(1.0f, currentSpeed + SpeedStep);
        building.LogisticsTruckSpeed = newSpeed;

        DebugLogger.LogServices($"LogisticsService: Upgraded speed for {building.Name} from {currentSpeed} to {newSpeed} (cost: {cost})");

        // Emit logistics upgrade event
        EmitLogisticsUpgradeEvent(building, "speed", currentSpeed, newSpeed, cost);

        return true;
    }

    /// <summary>
    /// Calculates the cost for a capacity upgrade
    /// </summary>
    public float CalculateCapacityUpgradeCost(int currentCapacity)
    {
        var diff = Math.Max(0, currentCapacity - CapacityBase);
        var level = (int)Math.Floor((float)diff / CapacityStep) + 1;
        return CapacityBaseCost * level;
    }

    /// <summary>
    /// Calculates the cost for a speed upgrade
    /// </summary>
    public float CalculateSpeedUpgradeCost(float currentSpeed)
    {
        var diff = Math.Max(0.0f, currentSpeed - SpeedBase);
        var level = (int)Math.Floor(diff / SpeedStep) + 1;
        return SpeedBaseCost * level;
    }

    /// <summary>
    /// Checks if a cost can be afforded
    /// </summary>
    public bool CanAfford(float cost)
    {
        if (economyManager == null)
        {
            DebugLogger.Error("debug_services", "LogisticsCanAffordEconomyNull", $"EconomyManager is null! Cost: {cost}");
            return false;
        }

        var money = economyManager.GetMoney();
        var canAfford = economyManager.CanAfford(cost);

        DebugLogger.Debug("debug_services", "LogisticsCanAffordInfo", $"CanAfford",
            new System.Collections.Generic.Dictionary<string, object?> { { "cost", cost }, { "money", money }, { "canAfford", canAfford } });

        return canAfford;
    }

    /// <summary>
    /// Gets the current capacity setting for a building
    /// </summary>
    private int GetCurrentCapacity(Building building)
    {
        try
        {
            return building.LogisticsTruckCapacity;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"LogisticsService: Error reading capacity: {ex.Message}");
        }
        return CapacityBase;
    }

    /// <summary>
    /// Gets the current speed setting for a building
    /// </summary>
    private float GetCurrentSpeed(Building building)
    {
        try
        {
            return building.LogisticsTruckSpeed;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"LogisticsService: Error reading speed: {ex.Message}");
        }
        return SpeedBase;
    }

    /// <summary>
    /// Emits a logistics upgrade event
    /// </summary>
    private void EmitLogisticsUpgradeEvent(Building building, string upgradeType, float oldValue, float newValue, float cost)
    {
        if (eventHub == null)
            return;

        try
        {
            // Create upgrade data
            var upgradeData = new Godot.Collections.Dictionary
            {
                ["building"] = building,
                ["upgradeType"] = upgradeType,
                ["oldValue"] = oldValue,
                ["newValue"] = newValue,
                ["cost"] = cost
            };

            eventHub.EmitSignal("LogisticsUpgraded", upgradeData);
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"LogisticsService: Error emitting upgrade event: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets logistics settings for a building to default values
    /// </summary>
    public void ResetLogisticsSettings(Building building)
    {
        if (building == null)
            return;

        building.LogisticsTruckCapacity = CapacityBase;
        building.LogisticsTruckSpeed = SpeedBase;

        DebugLogger.LogServices($"LogisticsService: Reset logistics settings for {building.Name} to defaults");
    }

    /// <summary>
    /// Gets upgrade information for UI display
    /// </summary>
    public UpgradeInfo GetCapacityUpgradeInfo(Building building)
    {
        if (building == null)
        {
            DebugLogger.Warn("debug_services", "LogisticsCapacityUpgradeBuildingNull", "Building is null");
            return new UpgradeInfo();
        }

        var currentCapacity = GetCurrentCapacity(building);
        var cost = CalculateCapacityUpgradeCost(currentCapacity);
        var canAfford = CanAfford(cost);

        DebugLogger.Debug("debug_services", "LogisticsCapacityUpgradeInfo", $"Info",
            new System.Collections.Generic.Dictionary<string, object?> { { "building", building.Name }, { "capacity", currentCapacity }, { "cost", cost }, { "canAfford", canAfford } });

        return new UpgradeInfo
        {
            CurrentValue = currentCapacity,
            NewValue = currentCapacity + CapacityStep,
            Cost = cost,
            CanAfford = canAfford,
            Description = $"Upgrade Kapazität (+{CapacityStep})\nKosten: {cost:F0}"
        };
    }

    /// <summary>
    /// Gets upgrade information for UI display
    /// </summary>
    public UpgradeInfo GetSpeedUpgradeInfo(Building building)
    {
        if (building == null)
            return new UpgradeInfo();

        var currentSpeed = GetCurrentSpeed(building);
        var cost = CalculateSpeedUpgradeCost(currentSpeed);
        var canAfford = CanAfford(cost);

        return new UpgradeInfo
        {
            CurrentValue = currentSpeed,
            NewValue = currentSpeed + SpeedStep,
            Cost = cost,
            CanAfford = canAfford,
            Description = $"Upgrade Geschwindigkeit (+{SpeedStep:F0})\nKosten: {cost:F0}"
        };
    }
}

/// <summary>
/// Complete logistics data for a building
/// </summary>
public class LogisticsUpgradeData
{
    public int CurrentCapacity { get; set; } = 5;
    public float CurrentSpeed { get; set; } = 32.0f;
    public float CapacityUpgradeCost { get; set; }
    public float SpeedUpgradeCost { get; set; }
    public bool CanAffordCapacityUpgrade { get; set; }
    public bool CanAffordSpeedUpgrade { get; set; }
}

/// <summary>
/// Upgrade information for UI display
/// </summary>
public class UpgradeInfo
{
    public float CurrentValue { get; set; }
    public float NewValue { get; set; }
    public float Cost { get; set; }
    public bool CanAfford { get; set; }
    public string Description { get; set; } = string.Empty;
}

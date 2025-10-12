// SPDX-License-Identifier: MIT
using Godot;
using System.Globalization;

/// <summary>
/// UIService.Economy: Geld/CanAfford API
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Get current money amount
    /// </summary>
    public double GetMoney()
    {
        if (economyManager == null)
        {
            DebugLogger.LogServices("UIService.GetMoney(): economyManager is null, calling InitializeServices", DebugLogs);
            InitializeServices();
        }

        if (economyManager == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService.GetMoney(): economyManager STILL null after InitializeServices!");
            return 0.0;
        }

        var money = economyManager.GetMoney();
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "UIService.GetMoney(): economyManager found, money = {0}", money), DebugLogs);
        return money;
    }

    /// <summary>
    /// Get formatted money string
    /// </summary>
    public string GetMoneyString()
    {
        return $"Geld: {GetMoney():F2}";
    }

    /// <summary>
    /// Check if player can afford amount
    /// </summary>
    public bool CanAfford(double amount)
    {
        if (economyManager == null) InitializeServices();
        var canAfford = economyManager?.CanAfford(amount) ?? false;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "UIService.CanAfford({0}) = {1}", amount, canAfford), DebugLogs);
        return canAfford;
    }
}

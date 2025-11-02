// SPDX-License-Identifier: MIT
using System.Globalization;
using Godot;

/// <summary>
/// UIService.Economy: Geld/CanAfford API.
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Get current money amount.
    /// </summary>
    /// <returns></returns>
    public double GetMoney()
    {
        if (this.economyManager == null)
        {
            DebugLogger.LogServices("UIService.GetMoney(): economyManager is null, calling InitializeServices", this.DebugLogs);
            this.InitializeServices();
        }

        if (this.economyManager == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService.GetMoney(): economyManager STILL null after InitializeServices!");
            return 0.0;
        }

        var money = this.economyManager.GetMoney();
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "UIService.GetMoney(): economyManager found, money = {0}", money), this.DebugLogs);
        return money;
    }

    /// <summary>
    /// Get formatted money string.
    /// </summary>
    /// <returns></returns>
    public string GetMoneyString()
    {
        return $"Geld: {this.GetMoney():F2}";
    }

    /// <summary>
    /// Check if player can afford amount.
    /// </summary>
    /// <returns></returns>
    public bool CanAfford(double amount)
    {
        if (this.economyManager == null)
        {
            this.InitializeServices();
        }

        var canAfford = this.economyManager?.CanAfford(amount) ?? false;
        DebugLogger.LogServices(string.Format(CultureInfo.InvariantCulture, "UIService.CanAfford({0}) = {1}", amount, canAfford), this.DebugLogs);
        return canAfford;
    }
}

// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Util;

namespace IndustrieLite.Core.Ports;

/// <summary>
/// Ökonomie-Operationen (enginefrei) – getrennt von dem minimalistischen IEconomyCore (CanAfford),
/// um bestehende Adapter nicht zu brechen.
/// </summary>
public interface IEconomyOps
{
    double GetMoney();
    bool CanAfford(double amount);
    CoreResult TryDebit(double amount);
    CoreResult TryCredit(double amount);
}


// SPDX-License-Identifier: MIT
namespace IndustrieLite.Core.Ports;

/// <summary>
/// Port für Ökonomie-Ereignisse (enginefrei).
/// </summary>
public interface IEconomyEvents
{
    void OnMoneyChanged(double money);
}


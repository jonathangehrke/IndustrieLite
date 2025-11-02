// SPDX-License-Identifier: MIT
using System;

/// <summary>
/// Port-Interface: Wirtschaftliche Abfragen/Aktionen, die vom Kern genutzt werden.
/// Dient der Entkopplung von Godot-abhängigen Managern in Unit-Tests.
/// </summary>
public interface IEconomy
{
    /// <summary>
    /// Prüft, ob die angegebene Menge bezahlt werden kann.
    /// </summary>
    bool CanAfford(int amount);
}


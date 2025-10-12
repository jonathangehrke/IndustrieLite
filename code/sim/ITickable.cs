// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

/// <summary>
/// Interface für alle Systeme, die in der Simulation getickelt werden können
/// </summary>
public interface ITickable
{
    /// <summary>
    /// Name des Systems für Debugging
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Führt einen Simulationstick aus.
    /// Der Parameter dt ist die Dauer des aktuellen Simulationsticks (Sekunden).
    /// </summary>
    void Tick(double dt);
}

/// <summary>
/// Interface für Produktionssysteme mit Totals-Berechnung
/// </summary>
public interface IProductionSystem : ITickable
{
    /// <summary>
    /// Berechnet die aktuellen Gesamt-Ressourcen
    /// Verwendet für Shadow-Mode Vergleiche
    /// </summary>
    Dictionary<string, double> GetTotals();
    
    /// <summary>
    /// Setzt das System zurück (für neue Ticks)
    /// </summary>
    void Reset();
}


// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Scheduler: Waehlt Lieferanten-Kombinationen und splittet in Teillieferungen.
/// Nutzt vorhandene PlaneTeillieferungen-Logik ueber IHasInventory/IHasStock und arbeitet auf Supplier-Daten.
/// </summary>
public class Scheduler
{
    /// <summary>
    /// Plant Teillieferungen fuer eine Ressource basierend auf Lieferanten mit Bestand.
    /// </summary>
    /// <param name="resourceId">Ressourcen-ID</param>
    /// <param name="lieferanten">Lieferanten-Liste</param>
    /// <param name="totalAmount">Gesamtbedarf</param>
    /// <param name="maxProTruck">Maximale Menge je Truck</param>
    public List<(SupplyIndex.Supplier Lieferant, int Menge)> Plan(StringName resourceId, List<SupplyIndex.Supplier> lieferanten, int totalAmount, int maxProTruck)
    {
        var result = new List<(SupplyIndex.Supplier, int)>();
        int rest = totalAmount;

        foreach (var lieferant in lieferanten)
        {
            while (lieferant.Free > 0.0 && rest > 0)
            {
                int ladung = System.Math.Min(maxProTruck, (int)System.Math.Floor(lieferant.Free));
                if (ladung <= 0)
                {
                    break;
                }

                lieferant.Reserved += ladung;
                rest -= ladung;
                result.Add((lieferant, ladung));
            }

            if (rest <= 0)
            {
                break;
            }
        }

        return result;
    }
}


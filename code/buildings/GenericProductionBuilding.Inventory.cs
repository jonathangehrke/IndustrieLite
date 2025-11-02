// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

// Teil-Implementierung: IHasInventory fuer GenericProductionBuilding
public partial class GenericProductionBuilding
{
    // --- IHasInventory Implementierung ---

    /// <inheritdoc/>
    public IReadOnlyDictionary<StringName, float> GetInventory() => this.inventar;

    /// <inheritdoc/>
    public void SetInventoryAmount(StringName resourceId, float amount)
    {
        this.inventar[resourceId] = amount;
        this.PruefeSimTickUndSendeSignale($"GenericProductionBuilding '{this.DefinitionId}': Inventarbestand setzen");
    }

    /// <inheritdoc/>
    public void AddToInventory(StringName resourceId, float amount)
    {
        var current = this.inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        this.inventar[resourceId] = current + amount;
        this.PruefeSimTickUndSendeSignale($"GenericProductionBuilding '{this.DefinitionId}': Inventarbestand erhoehen");
    }

    /// <inheritdoc/>
    public bool ConsumeFromInventory(StringName resourceId, float amount)
    {
        var current = this.inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        if (current < amount)
        {
            return false;
        }

        this.inventar[resourceId] = current - amount;
        this.PruefeSimTickUndSendeSignale($"GenericProductionBuilding '{this.DefinitionId}': Inventarbestand reduzieren");
        return true;
    }

    private void PruefeSimTickUndSendeSignale(string vorgang)
    {
        if (Simulation.IsInSimTick())
        {
            Simulation.ValidateSimTickContext(vorgang);
        }

        this.SendeInventarSignale();
    }
}

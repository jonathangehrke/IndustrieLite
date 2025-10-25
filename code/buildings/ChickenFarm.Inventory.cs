// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

// Teil-Implementierung: IHasInventory fuer ChickenFarm
public partial class ChickenFarm
{
    // --- IHasInventory Implementierung ---

    /// <inheritdoc/>
    public IReadOnlyDictionary<StringName, float> GetInventory() => this.inventar;

    /// <inheritdoc/>
    public void SetInventoryAmount(StringName resourceId, float amount)
    {
        this.inventar[resourceId] = amount;
        if (resourceId == MainResourceId || resourceId == "egg")
        {
            this.PruefeSimTickUndSendeSignale("ChickenFarm: Inventarbestand setzen");
        }
    }

    /// <inheritdoc/>
    public void AddToInventory(StringName resourceId, float amount)
    {
        var current = this.inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        this.inventar[resourceId] = current + amount;
        if (resourceId == MainResourceId || resourceId == "egg" || resourceId == new StringName("grain"))
        {
            this.PruefeSimTickUndSendeSignale("ChickenFarm: Inventarbestand erhoehen");
        }
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
        if (resourceId == MainResourceId || resourceId == "egg")
        {
            this.PruefeSimTickUndSendeSignale("ChickenFarm: Inventarbestand reduzieren");
        }
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

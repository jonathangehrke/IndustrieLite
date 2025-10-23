// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

// Teil-Implementierung: IHasInventory fuer GrainFarm
public partial class GrainFarm
{
    public IReadOnlyDictionary<StringName, float> GetInventory() => this.inventar;

    public void SetInventoryAmount(StringName resourceId, float amount)
    {
        this.inventar[resourceId] = amount;
        if (resourceId == MainResourceId)
        {
            this.PruefeSimTickUndSendeSignale("GrainFarm: Inventarbestand setzen");
        }
    }

    public void AddToInventory(StringName resourceId, float amount)
    {
        var current = this.inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        this.inventar[resourceId] = current + amount;
        if (resourceId == MainResourceId)
        {
            this.PruefeSimTickUndSendeSignale("GrainFarm: Inventarbestand erhoehen");
        }
    }

    public bool ConsumeFromInventory(StringName resourceId, float amount)
    {
        var current = this.inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        if (current < amount)
        {
            return false;
        }

        this.inventar[resourceId] = current - amount;
        if (resourceId == MainResourceId)
        {
            this.PruefeSimTickUndSendeSignale("GrainFarm: Inventarbestand reduzieren");
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

// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

// Teil-Implementierung: IHasInventory fuer ChickenFarm
public partial class ChickenFarm
{
    // --- IHasInventory Implementierung ---
    public IReadOnlyDictionary<StringName, float> GetInventory() => _inventar;

    public void SetInventoryAmount(StringName resourceId, float amount)
    {
        _inventar[resourceId] = amount;
        if (resourceId == MainResourceId || resourceId == "egg")
        {
            PruefeSimTickUndSendeSignale("ChickenFarm: Inventarbestand setzen");
        }
    }

    public void AddToInventory(StringName resourceId, float amount)
    {
        var current = _inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        _inventar[resourceId] = current + amount;
        if (resourceId == MainResourceId || resourceId == "egg" || resourceId == new StringName("grain"))
        {
            PruefeSimTickUndSendeSignale("ChickenFarm: Inventarbestand erhoehen");
        }
    }

    public bool ConsumeFromInventory(StringName resourceId, float amount)
    {
        var current = _inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        if (current < amount)
            return false;

        _inventar[resourceId] = current - amount;
        if (resourceId == MainResourceId || resourceId == "egg")
        {
            PruefeSimTickUndSendeSignale("ChickenFarm: Inventarbestand reduzieren");
        }
        return true;
    }

    private void PruefeSimTickUndSendeSignale(string vorgang)
    {
        if (Simulation.IsInSimTick())
        {
            Simulation.ValidateSimTickContext(vorgang);
        }
        SendeInventarSignale();
    }
}

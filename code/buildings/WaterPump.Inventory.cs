// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

// Teil-Implementierung: IHasInventory fuer WaterPump (leer, kein Lagerbedarf)
public partial class WaterPump
{
    private readonly Dictionary<StringName, float> _inventar = new();

    public IReadOnlyDictionary<StringName, float> GetInventory() => _inventar;

    public void SetInventoryAmount(StringName resourceId, float amount)
    {
        _inventar[resourceId] = amount;
    }

    public void AddToInventory(StringName resourceId, float amount)
    {
        var current = _inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        _inventar[resourceId] = current + amount;
    }

    public bool ConsumeFromInventory(StringName resourceId, float amount)
    {
        var current = _inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        if (current < amount) return false;
        _inventar[resourceId] = current - amount;
        return true;
    }
}


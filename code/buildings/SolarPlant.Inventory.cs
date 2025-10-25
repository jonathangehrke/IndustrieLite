// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

// Teil-Implementierung: IHasInventory fuer SolarPlant (leer, kein Lagerbedarf)
public partial class SolarPlant
{
    private readonly Dictionary<StringName, float> inventar = new();

    /// <inheritdoc/>
    public IReadOnlyDictionary<StringName, float> GetInventory() => this.inventar;

    /// <inheritdoc/>
    public void SetInventoryAmount(StringName resourceId, float amount)
    {
        this.inventar[resourceId] = amount;
    }

    /// <inheritdoc/>
    public void AddToInventory(StringName resourceId, float amount)
    {
        var current = this.inventar.TryGetValue(resourceId, out var v) ? v : 0f;
        this.inventar[resourceId] = current + amount;
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
        return true;
    }
}


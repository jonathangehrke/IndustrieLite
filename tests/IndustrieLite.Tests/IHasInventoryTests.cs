// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;
using Xunit;

public class IHasInventoryTests
{
    private class DummyInventory : IHasInventory
    {
        private readonly Dictionary<StringName, float> _map = new();
        public IReadOnlyDictionary<StringName, float> GetInventory() => _map;
        public void SetInventoryAmount(StringName resourceId, float amount) => _map[resourceId] = amount;
        public void AddToInventory(StringName resourceId, float amount) { _map[resourceId] = (_map.TryGetValue(resourceId, out var v) ? v : 0f) + amount; }
        public bool ConsumeFromInventory(StringName resourceId, float amount)
        {
            var cur = _map.TryGetValue(resourceId, out var v) ? v : 0f;
            if (cur < amount) return false;
            _map[resourceId] = cur - amount;
            return true;
        }
    }

    [Fact(Skip="Requires Godot StringName runtime (engine)")]
    public void Inventory_Add_Set_Consume_Works()
    {
        var inv = new DummyInventory();
        var rid = new StringName("chickens");

        inv.SetInventoryAmount(rid, 5);
        Assert.Equal(5f, inv.GetInventory()[rid]);

        inv.AddToInventory(rid, 3);
        Assert.Equal(8f, inv.GetInventory()[rid]);

        var ok = inv.ConsumeFromInventory(rid, 2);
        Assert.True(ok);
        Assert.Equal(6f, inv.GetInventory()[rid]);

        var fail = inv.ConsumeFromInventory(rid, 10);
        Assert.False(fail);
        Assert.Equal(6f, inv.GetInventory()[rid]);
    }
}

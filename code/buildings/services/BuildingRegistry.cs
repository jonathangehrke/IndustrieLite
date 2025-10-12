// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

/// <summary>
/// Registry haelt schnelle Indizes auf Gebaeude (per Zelle, per Id optional).
/// </summary>
public class BuildingRegistry
{
    private readonly Dictionary<Vector2I, Building> byCell = new();

    public void Add(Building b)
    {
        var size = b.Size;
        for (int x = 0; x < size.X; x++)
        for (int y = 0; y < size.Y; y++)
        {
            var c = new Vector2I(b.GridPos.X + x, b.GridPos.Y + y);
            byCell[c] = b;
        }
    }

    public void Remove(Building b)
    {
        var size = b.Size;
        for (int x = 0; x < size.X; x++)
        for (int y = 0; y < size.Y; y++)
        {
            var c = new Vector2I(b.GridPos.X + x, b.GridPos.Y + y);
            if (byCell.ContainsKey(c) && byCell[c] == b)
                byCell.Remove(c);
        }
    }

    public Building? GetAt(Vector2I cell)
    {
        if (byCell.TryGetValue(cell, out var b)) return b;
        return null;
    }

    public void Clear()
    {
        byCell.Clear();
    }
}

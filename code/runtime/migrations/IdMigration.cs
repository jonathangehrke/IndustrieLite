// SPDX-License-Identifier: MIT
using System.Collections.Generic;

public static class IdMigration
{
    private static readonly Dictionary<string,string> _map = new()
    {
        { "House", "house" },
        { "Solar", "solar_plant" },
        { "Water", "water_pump" },
        { "ChickenFarm", "chicken_farm" },
        { "City", "city" },
        { "Road", "road" }
    };

    public static string ToCanonical(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        return _map.TryGetValue(id, out var v) ? v : id;
    }
}


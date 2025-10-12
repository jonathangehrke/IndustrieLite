// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// DistanceCalculator: Einheitliche Distanz- und Kostenberechnungen.
/// Bietet Tile-/Welt-Distanzen sowie Transportkosten basierend auf Kachelabstand.
/// </summary>
public static class DistanceCalculator
{
    /// <summary>
    /// Manhattan-Distanz in Kacheln zwischen zwei Rasterzellen.
    /// </summary>
    public static int GetTileDistance(Vector2I von, Vector2I nach)
    {
        return Mathf.Abs(von.X - nach.X) + Mathf.Abs(von.Y - nach.Y);
    }

    /// <summary>
    /// Euklidische Distanz in Weltkoordinaten (Pixel/Einheiten).
    /// </summary>
    public static float GetWorldDistance(Vector2 von, Vector2 nach)
    {
        return von.DistanceTo(nach);
    }

    /// <summary>
    /// Transportkosten auf Basis von Kachelabstand (Manhattan) zwischen zwei Rasterzellen.
    /// </summary>
    public static double GetTransportCostTiles(Vector2I startTile, Vector2I endTile, double baseCostPerTile)
    {
        int kachelDist = GetTileDistance(startTile, endTile);
        return kachelDist * baseCostPerTile;
    }

    /// <summary>
    /// Transportkosten aus Weltkoordinaten, indem Koordinaten auf Rasterzellen abgebildet werden.
    /// </summary>
    public static double GetTransportCost(Vector2 startWorld, Vector2 endWorld, double baseCostPerTile, int tileSize)
    {
        var startTile = new Vector2I(Mathf.FloorToInt(startWorld.X / tileSize), Mathf.FloorToInt(startWorld.Y / tileSize));
        var endTile = new Vector2I(Mathf.FloorToInt(endWorld.X / tileSize), Mathf.FloorToInt(endWorld.Y / tileSize));
        return GetTransportCostTiles(startTile, endTile, baseCostPerTile);
    }

    /// <summary>
    /// Summierte Weltlänge eines Pfades (Liste von Weltpunkten).
    /// </summary>
    public static double GetPathWorldLength(IList<Vector2> path)
    {
        if (path == null || path.Count < 2) return 0.0;
        double total = 0.0;
        for (int i = 1; i < path.Count; i++)
        {
            total += path[i - 1].DistanceTo(path[i]);
        }
        return total;
    }
}

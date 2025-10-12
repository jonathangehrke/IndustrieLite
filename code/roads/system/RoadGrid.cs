// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// RoadGrid: Modell fuer Strassenbelegung.
/// Haltet belegte Zellen und sendet Events bei Aenderungen.
/// </summary>
public class RoadGrid
{
    public int Width { get; }
    public int Height { get; }

    private readonly bool[,] roads;
    private int roadCount = 0;

    public event Action<Vector2I>? RoadAdded;
    public event Action<Vector2I>? RoadRemoved;

    public RoadGrid(int width, int height)
    {
        Width = width;
        Height = height;
        roads = new bool[width, height];
    }

    public bool InBounds(Vector2I cell)
    {
        return cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
    }

    public bool IsRoad(Vector2I cell)
    {
        if (!InBounds(cell)) return false;
        return roads[cell.X, cell.Y];
    }

    public bool AddRoad(Vector2I cell)
    {
        if (!InBounds(cell)) return false;
        if (roads[cell.X, cell.Y]) return false;
        roads[cell.X, cell.Y] = true;
        roadCount++;
        RoadAdded?.Invoke(cell);
        return true;
    }

    public bool RemoveRoad(Vector2I cell)
    {
        if (!InBounds(cell)) return false;
        if (!roads[cell.X, cell.Y]) return false;
        roads[cell.X, cell.Y] = false;
        if (roadCount > 0) roadCount--;
        RoadRemoved?.Invoke(cell);
        return true;
    }

    public bool AnyRoadExists()
    {
        return roadCount > 0;
    }

    public bool GetCell(int x, int y)
    {
        return roads[x, y];
    }

    /// <summary>
    /// Clear all roads (for NewGame)
    /// </summary>
    public void Clear()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (roads[x, y])
                {
                    roads[x, y] = false;
                    RoadRemoved?.Invoke(new Vector2I(x, y));
                }
            }
        }
        roadCount = 0;
    }
}

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
        this.Width = width;
        this.Height = height;
        this.roads = new bool[width, height];
    }

    public bool InBounds(Vector2I cell)
    {
        return cell.X >= 0 && cell.Y >= 0 && cell.X < this.Width && cell.Y < this.Height;
    }

    public bool IsRoad(Vector2I cell)
    {
        if (!this.InBounds(cell))
        {
            return false;
        }

        return this.roads[cell.X, cell.Y];
    }

    public bool AddRoad(Vector2I cell)
    {
        if (!this.InBounds(cell))
        {
            return false;
        }

        if (this.roads[cell.X, cell.Y])
        {
            return false;
        }

        this.roads[cell.X, cell.Y] = true;
        this.roadCount++;
        this.RoadAdded?.Invoke(cell);
        return true;
    }

    public bool RemoveRoad(Vector2I cell)
    {
        if (!this.InBounds(cell))
        {
            return false;
        }

        if (!this.roads[cell.X, cell.Y])
        {
            return false;
        }

        this.roads[cell.X, cell.Y] = false;
        if (this.roadCount > 0)
        {
            this.roadCount--;
        }

        this.RoadRemoved?.Invoke(cell);
        return true;
    }

    public bool AnyRoadExists()
    {
        return this.roadCount > 0;
    }

    public bool GetCell(int x, int y)
    {
        return this.roads[x, y];
    }

    /// <summary>
    /// Clear all roads (for NewGame).
    /// </summary>
    public void Clear()
    {
        for (int x = 0; x < this.Width; x++)
        {
            for (int y = 0; y < this.Height; y++)
            {
                if (this.roads[x, y])
                {
                    this.roads[x, y] = false;
                    this.RoadRemoved?.Invoke(new Vector2I(x, y));
                }
            }
        }
        this.roadCount = 0;
    }
}

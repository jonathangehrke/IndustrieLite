// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Router: Wrap um RoadManager für Pfad-/Distanz-/Kostenberechnung.
/// Hält eine einfache GraphVersion (erhöht bei RoadGraphChanged Events), um Caches zu invalidieren.
/// </summary>
public class Router : System.IDisposable
{
    private readonly RoadManager roadManager;
    private readonly EventHub? eventHub;
    private bool disposed;
    private readonly AboVerwalter abos = new();

    public long GraphVersion { get; private set; } = 0;

    public void SetGraphVersion(long version) => this.GraphVersion = version;

    public int GetGraphVersionAsInt() => this.GraphVersion > int.MaxValue ? int.MaxValue : (int)this.GraphVersion;

    public void SetGraphVersionFromInt(int version) => this.GraphVersion = version;

    public Router(RoadManager rm, EventHub? eh)
    {
        this.roadManager = rm;
        this.eventHub = eh;
        if (this.eventHub != null)
        {
            this.abos.Abonniere(
                () => this.eventHub.RoadGraphChanged += this.OnRoadGraphChanged,
                () =>
                {
                    try
                    {
                        this.eventHub.RoadGraphChanged -= this.OnRoadGraphChanged;
                    }
                    catch
                    {
                    }
                });
        }
    }

    private void OnRoadGraphChanged()
    {
        this.GraphVersion++;
    }

    public List<Vector2> GetPath(Vector2 start, Vector2 ziel)
    {
        return this.roadManager.GetPath(start, ziel);
    }

    public double ComputeCost(Vector2 start, Vector2 ziel, double basePerTilePerUnit, int units, int tileSize, double truckFixedCost)
    {
        // Bevorzugt: Pfadkosten über RoadManager
        var path = this.roadManager.GetPath(start, ziel);
        double tiles = 0.0;
        if (path != null && path.Count > 1)
        {
            var worldLen = DistanceCalculator.GetPathWorldLength(path);
            tiles = worldLen / tileSize;
        }
        else
        {
            // Fallback: Luftlinie in Tiles schätzen
            var cost = DistanceCalculator.GetTransportCost(start, ziel, basePerTilePerUnit, tileSize);
            tiles = cost / basePerTilePerUnit; // invertiert, um gleiche Formel zu nutzen
        }
        return (tiles * basePerTilePerUnit * units) + truckFixedCost;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        try
        {
            this.abos.DisposeAll();
        }
        catch
        {
        }
    }
}


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
    private bool _disposed;
    private readonly AboVerwalter _abos = new();

    public long GraphVersion { get; private set; } = 0;
    public void SetGraphVersion(long version) => GraphVersion = version;
    public int GetGraphVersionAsInt() => GraphVersion > int.MaxValue ? int.MaxValue : (int)GraphVersion;
    public void SetGraphVersionFromInt(int version) => GraphVersion = version;

    public Router(RoadManager rm, EventHub? eh)
    {
        roadManager = rm;
        eventHub = eh;
        if (eventHub != null)
        {
            _abos.Abonniere(
                () => eventHub.RoadGraphChanged += OnRoadGraphChanged,
                () => { try { eventHub.RoadGraphChanged -= OnRoadGraphChanged; } catch { } }
            );
        }
    }

    private void OnRoadGraphChanged()
    {
        GraphVersion++;
    }

    public List<Vector2> GetPath(Vector2 start, Vector2 ziel)
    {
        return roadManager.GetPath(start, ziel);
    }

    public double ComputeCost(Vector2 start, Vector2 ziel, double basePerTilePerUnit, int units, int tileSize, double truckFixedCost)
    {
        // Bevorzugt: Pfadkosten über RoadManager
        var path = roadManager.GetPath(start, ziel);
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
        return tiles * basePerTilePerUnit * units + truckFixedCost;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _abos.DisposeAll(); } catch { }
    }
}


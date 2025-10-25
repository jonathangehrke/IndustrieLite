// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;
using IndustrieLite.Transport.Interfaces;

public partial class TruckManager : Node, ITruckManager
{
    /// <inheritdoc/>
    public List<Truck> Trucks { get; private set; } = new List<Truck>();

    /// <inheritdoc/>
    public int MaxMengeProTruck { get; private set; }

    private Fleet? fleet;
    private RoadManager? roadManager;
    private BuildingManager buildingManager = default!;
    private GameManager? gameManager;

    // Delegate für Kostenberechnung (wird vom TransportCoordinator gesetzt)
    public System.Func<Vector2, Vector2, int, double>? CalculateCostDelegate { get; set; }

    public System.Func<Building, Vector2>? CalculateCenterDelegate { get; set; }

    public void Initialize(Fleet fleet, RoadManager? roadManager, BuildingManager buildingManager, GameManager gameManager, int maxMengeProTruck)
    {
        this.fleet = fleet;
        this.roadManager = roadManager;
        this.buildingManager = buildingManager;
        this.gameManager = gameManager;
        this.MaxMengeProTruck = maxMengeProTruck;
    }

    private Vector2 Zentrum(Building gebaeude)
    {
        if (this.CalculateCenterDelegate != null)
        {
            return this.CalculateCenterDelegate(gebaeude);
        }

        // Fallback implementation
        return ((Node2D)gebaeude).GlobalPosition + new Vector2(
            gebaeude.Size.X * this.buildingManager.TileSize / 2,
            gebaeude.Size.Y * this.buildingManager.TileSize / 2);
    }

    /// <inheritdoc/>
    public Truck SpawnTruck(Vector2 start, Vector2 ziel, int menge, double ppu, float? speedOverride)
    {
        // Debug output to track all truck spawns
        DebugLogger.Debug("debug_transport", "TruckManagerSpawnTruck", $"Creating truck",
            new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "amount", menge }, { "start", start }, { "target", ziel } });

        // Print stack trace to see who called this
        var stackTrace = System.Environment.StackTrace;
        var lines = stackTrace.Split('\n');
        DebugLogger.Debug("debug_transport", "TruckManagerSpawnStack", "SpawnTruck called from");
        for (int i = 1; i < System.Math.Min(5, lines.Length); i++) // Show first 4 stack frames
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line))
            {
                DebugLogger.Debug("debug_transport", "TruckManagerSpawnStackLine", line);
            }
        }

        var game = this.gameManager; // Injected via DI in Initialize()
        Truck truck;
        if (this.fleet != null)
        {
            truck = this.fleet.SpawnTruck(start, ziel, menge, 0.0, game);
            truck.PricePerUnit = ppu;
            this.Trucks.Add(truck);
        }
        else
        {
            truck = new Truck();
            truck.GlobalPosition = start;
            truck.Target = ziel;
            truck.Amount = menge;
            truck.PricePerUnit = ppu;
            truck.Game = game;
            this.AddChild(truck);
            this.Trucks.Add(truck);
        }

        // Darstellungs-Reihenfolge: Trucks ueber Strassen (RoadRenderer ZIndex=10)
        try
        {
            truck.ZAsRelative = false;
            truck.ZIndex = 11;
        }
        catch
        {
        }

        if (this.roadManager != null)
        {
            var path = this.roadManager.GetPath(start, ziel);
            if (path != null && path.Count > 0)
            {
                truck.Path = path;
                try
                {
                    if (this.CalculateCostDelegate != null)
                    {
                        truck.TransportCost = this.CalculateCostDelegate(start, ziel, menge);
                    }
                    else
                    {
                        // Fallback cost calculation
                        var worldLen = DistanceCalculator.GetPathWorldLength(path);
                        var tiles = worldLen / this.buildingManager.TileSize;
                        truck.TransportCost = (tiles * 0.05 * menge) + 1.0; // Default values
                    }
                }
                catch
                {
                    truck.TransportCost = 1.0;
                } // Default TruckFixedCost
            }
        }

        if (truck.Path == null || truck.Path.Count == 0)
        {
            try
            {
                if (this.CalculateCostDelegate != null)
                {
                    truck.TransportCost = this.CalculateCostDelegate(start, ziel, menge);
                }
                else
                {
                    // Fallback cost calculation
                    double cost = DistanceCalculator.GetTransportCost(start, ziel, 0.05 * menge, this.buildingManager.TileSize) + 1.0;
                    truck.TransportCost = cost;
                }
            }
            catch
            {
                truck.TransportCost = 1.0;
            } // Default TruckFixedCost
        }

        if (speedOverride.HasValue)
        {
            try
            {
                truck.SetSpeed(speedOverride.Value);
            }
            catch
            {
            }
        }
        return truck;
    }

    /// <inheritdoc/>
    public Truck SpawnTruck(Vector2 start, Vector2 ziel, int menge, double ppu)
    {
        return this.SpawnTruck(start, ziel, menge, ppu, null);
    }

    /// <inheritdoc/>
    public void ProcessTruckTick(double dt)
    {
        // Snapshot verwenden, um Aenderungen (z. B. Rueckfahrten) waehrend Iteration zu erlauben
        var snapshot = new System.Collections.Generic.List<Truck>(this.Trucks);
        foreach (var t in snapshot)
        {
            if (t == null || !GodotObject.IsInstanceValid(t) || t.IsQueuedForDeletion())
            {
                continue;
            }

            t.FixedStepTick(dt);
        }
    }

    /// <inheritdoc/>
    public void RepathAllTrucks()
    {
        if (!this.IsInsideTree() || this.roadManager == null)
        {
            return;
        }

        var alive = new List<Truck>();
        foreach (var t in this.Trucks)
        {
            if (t == null || !GodotObject.IsInstanceValid(t) || t.IsQueuedForDeletion())
            {
                continue;
            }

            alive.Add(t);
            var path = this.roadManager.GetPath(t.GlobalPosition, t.Target);
            t.Path = (path != null && path.Count > 0) ? path : null;
        }
        this.Trucks.Clear();
        this.Trucks.AddRange(alive);
    }

    /// <inheritdoc/>
    public void CancelOrdersFor(Node2D n)
    {
        var alive = new List<Truck>();
        foreach (var t in this.Trucks)
        {
            if (t == null || !GodotObject.IsInstanceValid(t) || t.IsQueuedForDeletion())
            {
                continue;
            }

            if (t.SourceNode == n || t.TargetNode == n)
            {
                t.QueueFree();
                continue;
            }
            alive.Add(t);
        }
        this.Trucks.Clear();
        this.Trucks.AddRange(alive);

        DebugLogger.LogTransport(() => $"CancelOrdersFor: Trucks für {n.Name} bereinigt");
    }

    /// <inheritdoc/>
    public void RestartPendingTrucks()
    {
        foreach (var truck in this.Trucks)
        {
            if (truck == null)
            {
                continue;
            }

            if (!GodotObject.IsInstanceValid(truck))
            {
                continue;
            }

            truck.QueueFree();
        }
        this.Trucks.Clear();

        if (this.fleet != null)
        {
            this.fleet.Trucks.RemoveAll(t => t == null || !GodotObject.IsInstanceValid(t));
        }

        DebugLogger.LogTransport("TruckManager: Pending trucks nach Load neu gestartet");
    }

    /// <summary>
    /// Entfernt alle aktiven Trucks (Hard-Reset für NewGame/ClearState).
    /// </summary>
    public void ClearAllTrucks()
    {
        this.RestartPendingTrucks();
        DebugLogger.LogTransport("TruckManager: ClearAllTrucks ausgeführt");
    }
}

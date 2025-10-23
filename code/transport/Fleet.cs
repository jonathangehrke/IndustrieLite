// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Fleet: Godot-Adapter zum Instanziieren/Verwalten von Trucks (Nodes).
/// </summary>
public partial class Fleet : Node
{
    public List<Truck> Trucks { get; } = new List<Truck>();

    public Truck SpawnTruck(Vector2 start, Vector2 ziel, int menge, double transportCost, GameManager game)
    {
        DebugLogger.Debug("debug_transport", "FleetSpawnTruck", $"Creating truck",
            new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "amount", menge }, { "start", start }, { "target", ziel } });

        var t = new Truck();
        t.GlobalPosition = start;
        t.Target = ziel;
        t.Amount = menge;
        t.TransportCost = transportCost;
        t.Game = game;
        this.AddChild(t);
        this.Trucks.Add(t);

        DebugLogger.Info("debug_transport", "FleetSpawnTruckCreated", $"Truck created",
            new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "amount", t.Amount } });
        return t;
    }

    public void CleanupInvalid()
    {
        this.Trucks.RemoveAll(t => t == null || !GodotObject.IsInstanceValid(t) || t.IsQueuedForDeletion());
    }
}

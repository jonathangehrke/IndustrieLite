// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

namespace IndustrieLite.Transport.Interfaces
{
    public interface ITruckManager
    {
        List<Truck> Trucks { get; }
        int MaxMengeProTruck { get; }
        Truck SpawnTruck(Vector2 start, Vector2 target, int amount, double pricePerUnit);
        Truck SpawnTruck(Vector2 start, Vector2 target, int amount, double pricePerUnit, float? speedOverride);
        void RepathAllTrucks();
        void CancelOrdersFor(Node2D node);
        void ProcessTruckTick(double dt);
        void RestartPendingTrucks();
    }
}

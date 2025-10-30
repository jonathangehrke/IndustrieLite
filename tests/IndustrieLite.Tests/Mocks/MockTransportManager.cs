// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;
using IndustrieLite.Transport.Core;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of ITransportManager for testing.
/// </summary>
public class MockTransportManager : ITransportManager
{
    public bool AcceptOrderWasCalled { get; set; }
    public bool ClearAllDataWasCalled { get; set; }

    public void AcceptOrder(int id)
    {
        AcceptOrderWasCalled = true;
    }

    public Result TryAcceptOrder(int id, string? correlationId = null)
    {
        AcceptOrderWasCalled = true;
        return Result.Ok();
    }

    public void HandleTransportClick(Vector2I cell) { }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders() => new();

    public void StartManualTransport(Building source, Building target) { }

    public Result TryStartManualTransport(Building source, Building target, string? correlationId = null)
    {
        return Result.Ok();
    }

    public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec) { }

    public void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed) { }

    public Result TryStartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f, string? correlationId = null)
    {
        return Result.Ok();
    }

    public void StopPeriodicSupplyRoute(Building consumer, StringName resourceId) { }

    public Result TryStopPeriodicSupplyRoute(Building consumer, StringName resourceId, string? correlationId = null)
    {
        return Result.Ok();
    }

    public void TruckArrived(Truck t) { }
    public void RestartPendingJobs() { }
    public void RepathAllTrucks() { }
    public void CancelOrdersFor(Node2D node) { }
    public double GetCurrentMarketPrice(string product, City city) => 0.0;
    public void UpdateOrderBookFromCities() { }
    public void UpdateSupplyIndexFromBuildings() { }
    public bool IsReady() => true;
    public TransportCoreService? GetTransportCore() => null;
    public List<Truck> GetTrucks() => new();

    public void ClearAllData()
    {
        ClearAllDataWasCalled = true;
    }

    public void SetFixedSupplierRoute(Node consumer, string resourceId, Node supplier) { }
    public void ClearFixedSupplierRoute(Node consumer, string resourceId) { }
    public Building? GetFixedSupplierRoute(Node consumer, string resourceId) => null;
}

// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;
using IndustrieLite.Transport.Core;

/// <summary>
/// Interface for the Transport Manager - handles transport orders, routes, and trucks.
/// </summary>
public interface ITransportManager
{
    /// <summary>
    /// Accepts a transport order by ID.
    /// </summary>
    void AcceptOrder(int id);

    /// <summary>
    /// Tries to accept a transport order by ID using Result pattern.
    /// </summary>
    Result TryAcceptOrder(int id, string? correlationId = null);

    /// <summary>
    /// Handles a transport click at the specified cell.
    /// </summary>
    void HandleTransportClick(Vector2I cell);

    /// <summary>
    /// Gets all pending transport orders.
    /// </summary>
    Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders();

    /// <summary>
    /// Starts a manual transport between source and target buildings.
    /// </summary>
    void StartManualTransport(Building source, Building target);

    /// <summary>
    /// Tries to start a manual transport using Result pattern.
    /// </summary>
    Result TryStartManualTransport(Building source, Building target, string? correlationId = null);

    /// <summary>
    /// Starts a periodic supply route between supplier and consumer.
    /// </summary>
    void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec);

    /// <summary>
    /// Starts a periodic supply route with custom speed.
    /// </summary>
    void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed);

    /// <summary>
    /// Tries to start a periodic supply route using Result pattern.
    /// </summary>
    Result TryStartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f, string? correlationId = null);

    /// <summary>
    /// Stops a periodic supply route for the specified consumer and resource.
    /// </summary>
    void StopPeriodicSupplyRoute(Building consumer, StringName resourceId);

    /// <summary>
    /// Tries to stop a periodic supply route using Result pattern.
    /// </summary>
    Result TryStopPeriodicSupplyRoute(Building consumer, StringName resourceId, string? correlationId = null);

    /// <summary>
    /// Notifies the transport manager that a truck has arrived.
    /// </summary>
    void TruckArrived(Truck t);

    /// <summary>
    /// Restarts all pending transport jobs.
    /// </summary>
    void RestartPendingJobs();

    /// <summary>
    /// Recalculates paths for all trucks.
    /// </summary>
    void RepathAllTrucks();

    /// <summary>
    /// Cancels all transport orders for the specified node.
    /// </summary>
    void CancelOrdersFor(Node2D node);

    /// <summary>
    /// Gets the current market price for a product at a city.
    /// </summary>
    double GetCurrentMarketPrice(string product, City city);

    /// <summary>
    /// Updates the order book from cities.
    /// </summary>
    void UpdateOrderBookFromCities();

    /// <summary>
    /// Updates the supply index from buildings.
    /// </summary>
    void UpdateSupplyIndexFromBuildings();

    /// <summary>
    /// Checks if the transport manager is ready.
    /// </summary>
    bool IsReady();

    /// <summary>
    /// Gets the transport core service (used by SaveLoadService).
    /// </summary>
    TransportCoreService? GetTransportCore();

    /// <summary>
    /// Gets all trucks.
    /// </summary>
    List<Truck> GetTrucks();

    /// <summary>
    /// Clears all transport data (lifecycle management).
    /// </summary>
    void ClearAllData();

    /// <summary>
    /// Sets a fixed supplier route for a consumer's resource.
    /// </summary>
    void SetFixedSupplierRoute(Node consumer, string resourceId, Node supplier);

    /// <summary>
    /// Clears a fixed supplier route for a consumer's resource.
    /// </summary>
    void ClearFixedSupplierRoute(Node consumer, string resourceId);

    /// <summary>
    /// Gets the fixed supplier for a consumer's resource.
    /// </summary>
    Building? GetFixedSupplierRoute(Node consumer, string resourceId);
}

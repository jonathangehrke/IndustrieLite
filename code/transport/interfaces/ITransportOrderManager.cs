// SPDX-License-Identifier: MIT
using Godot;

namespace IndustrieLite.Transport.Interfaces
{
    public interface ITransportOrderManager
    {
        void AcceptOrder(int orderId);
        void StartManualTransport(Building source, Building target);
        // Periodische Lieferroute: Lieferant -> Verbraucher fuer Ressource
        void StartPeriodicSupplyRoute(Building supplier, Building consumer, StringName resourceId, int maxPerTruck, double periodSec, float speed = 120f);
        void StopPeriodicSupplyRoute(Building consumer, StringName resourceId);
        void HandleTransportClick(Vector2I cell);
        Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders();
        void ProcessOrderTick(double dt);
        void RestartPendingJobs();
        void UpdateOrderBookFromCities();
        void UpdateSupplyIndexFromBuildings();
    }
}

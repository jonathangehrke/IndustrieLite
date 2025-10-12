// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Interfaces
{
    /// <summary>
    /// Verantwortlich für das Auftragsbuch und die Zuordnung von Jobs zu Orders.
    /// </summary>
    public interface ITransportOrderManager
    {
        IReadOnlyDictionary<int, DeliveryOrder> DeliveryOrders { get; }
        OrderBook Auftragsbuch { get; }

        void AktualisiereAuftragsbuch(IEnumerable<TransportAuftragsDaten> daten);
        DeliveryOrder EnsureDeliveryOrder(TransportAuftragsDaten daten);
        DeliveryOrder? HoleDeliveryOrder(int orderId);
        void RemoveJobFromDeliveryOrder(Guid jobId, int orderId);

        void VerarbeiteJobAbschluss(TransportJob job, int gelieferteMenge);
        void VerarbeiteJobFehler(TransportJob job);

        void RegistriereWiederhergestelltenAuftrag(DeliveryOrder order);
        void EntferneAlleOrders();
    }
}

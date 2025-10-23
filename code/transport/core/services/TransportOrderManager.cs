// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Services
{
    using System;
    using System.Collections.Generic;
    using IndustrieLite.Transport.Core.Interfaces;
    using IndustrieLite.Transport.Core.Models;

    /// <summary>
    /// Verwaltet das Auftragsbuch und die Zuordnung zwischen Aufträgen und Jobs.
    /// </summary>
    public class TransportOrderManager : ITransportOrderManager
    {
        private readonly OrderBook auftragsbuch;
        private readonly Dictionary<int, DeliveryOrder> deliveryOrders = new Dictionary<int, DeliveryOrder>();

        public TransportOrderManager(OrderBook? orderBook = null)
        {
            this.auftragsbuch = orderBook ?? new OrderBook();
        }

        public IReadOnlyDictionary<int, DeliveryOrder> DeliveryOrders => this.deliveryOrders;

        public OrderBook Auftragsbuch => this.auftragsbuch;

        public void AktualisiereAuftragsbuch(IEnumerable<TransportAuftragsDaten> daten)
        {
            foreach (var auftrag in daten)
            {
                var info = new OrderBook.OrderInfo
                {
                    OrderId = auftrag.AuftragId,
                    ResourceId = auftrag.ResourceId,
                    TotalAmount = auftrag.Gesamtmenge,
                    Remaining = auftrag.Restmenge <= 0 ? auftrag.Gesamtmenge : auftrag.Restmenge,
                    PricePerUnit = auftrag.PreisProEinheit,
                    CreatedOn = auftrag.ErzeugtAm,
                    ExpiresOn = auftrag.GueltigBis,
                    QuelleReferenz = auftrag.QuelleReferenz,
                    ZielReferenz = auftrag.ZielReferenz,
                    Accepted = auftrag.IstAkzeptiert,
                };

                this.auftragsbuch.AddOrUpdate(info);

                var delivery = this.EnsureDeliveryOrder(auftrag);
                delivery.Remaining = auftrag.Restmenge <= 0 ? auftrag.Gesamtmenge : auftrag.Restmenge;
                delivery.PreisProEinheit = auftrag.PreisProEinheit;
                if (delivery.JobIds.Count == 0)
                {
                    delivery.Status = delivery.Remaining > 0 ? DeliveryOrderStatus.Offen : DeliveryOrderStatus.Abgeschlossen;
                }
            }
        }

        public DeliveryOrder EnsureDeliveryOrder(TransportAuftragsDaten daten)
        {
            if (!this.deliveryOrders.TryGetValue(daten.AuftragId, out var delivery))
            {
                delivery = new DeliveryOrder(daten.AuftragId, daten.ResourceId, daten.ProduktName, daten.Gesamtmenge, daten.PreisProEinheit, daten.QuelleReferenz, daten.ZielReferenz)
                {
                    Remaining = daten.Restmenge <= 0 ? daten.Gesamtmenge : daten.Restmenge,
                };
                this.deliveryOrders[daten.AuftragId] = delivery;
            }

            return delivery;
        }

        public DeliveryOrder? HoleDeliveryOrder(int orderId)
        {
            return this.deliveryOrders.TryGetValue(orderId, out var order) ? order : null;
        }

        public void RemoveJobFromDeliveryOrder(Guid jobId, int orderId)
        {
            if (!this.deliveryOrders.TryGetValue(orderId, out var order))
            {
                return;
            }

            order.JobIds.Remove(jobId);
            if (order.JobIds.Count == 0 && order.Status != DeliveryOrderStatus.Abgeschlossen && order.Remaining > 0)
            {
                order.Status = DeliveryOrderStatus.Offen;
            }
        }

        public void VerarbeiteJobAbschluss(TransportJob job, int gelieferteMenge)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            this.RemoveJobFromDeliveryOrder(job.JobId, job.OrderId);

            if (this.deliveryOrders.TryGetValue(job.OrderId, out var order))
            {
                order.Remaining = Math.Max(0, order.Remaining - gelieferteMenge);
                if (order.Remaining <= 0)
                {
                    order.Status = DeliveryOrderStatus.Abgeschlossen;
                }
            }
        }

        public void VerarbeiteJobFehler(TransportJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            this.RemoveJobFromDeliveryOrder(job.JobId, job.OrderId);
        }

        public void RegistriereWiederhergestelltenAuftrag(DeliveryOrder order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            this.deliveryOrders[order.OrderId] = order;
        }

        public void EntferneAlleOrders()
        {
            this.deliveryOrders.Clear();
        }
    }
}

// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace IndustrieLite.Transport.Core.Models
{
    /// <summary>
    /// Serialisierte Daten eines Lieferauftrags.
    /// </summary>
    public class DeliveryOrderSaveData
    {
        public int OrderId { get; set; }
        public string ResourceId { get; set; } = string.Empty;
        public string Produkt { get; set; } = string.Empty;
        public int Gesamtmenge { get; set; }
        public int Remaining { get; set; }
        public double PreisProEinheit { get; set; }
        public DeliveryOrderStatus Status { get; set; } = DeliveryOrderStatus.Offen;
        public List<Guid> JobIds { get; set; } = new List<Guid>();
    }
}

// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace IndustrieLite.Transport.Core.Models
{
    /// <summary>
    /// Aggregiert den serialisierbaren Zustand des Transport-Subsystems.
    /// </summary>
    public class TransportCoreSaveData
    {
        public List<TransportJobSaveData> Jobs { get; set; } = new List<TransportJobSaveData>();
        public List<Guid> JobQueue { get; set; } = new List<Guid>();
        public List<DeliveryOrderSaveData> DeliveryOrders { get; set; } = new List<DeliveryOrderSaveData>();
    }
}

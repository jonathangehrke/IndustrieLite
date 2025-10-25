// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Models
{
    using System;
    using System.Collections.Generic;

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

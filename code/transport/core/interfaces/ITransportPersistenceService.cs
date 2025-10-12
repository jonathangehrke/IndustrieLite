// SPDX-License-Identifier: MIT
using System;
using Godot;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Interfaces
{
    /// <summary>
    /// Kümmert sich um das Serialisieren und Wiederherstellen des Transport-Zustands.
    /// </summary>
    public interface ITransportPersistenceService
    {
        TransportCoreSaveData CaptureState();
        void RestoreState(TransportCoreSaveData state, Func<Guid, Building?>? buildingResolver = null);

        void SetServiceReferences(ITransportJobManager jobManager,
                                  ITransportOrderManager orderManager,
                                  ITransportSupplyService supplyService,
                                  ITransportPlanningService planningService);
    }
}

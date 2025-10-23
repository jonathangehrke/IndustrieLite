// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Interfaces
{
    using System;
    using Godot;
    using IndustrieLite.Transport.Core.Models;

    /// <summary>
    /// Kümmert sich um das Serialisieren und Wiederherstellen des Transport-Zustands.
    /// </summary>
    public interface ITransportPersistenceService
    {
        TransportCoreSaveData CaptureState();

        void RestoreState(TransportCoreSaveData state, Func<Guid, Building?>? buildingResolver = null);

        void SetServiceReferences(
            ITransportJobManager jobManager,
                                  ITransportOrderManager orderManager,
                                  ITransportSupplyService supplyService,
                                  ITransportPlanningService planningService);
    }
}

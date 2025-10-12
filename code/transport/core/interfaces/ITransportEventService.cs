// SPDX-License-Identifier: MIT
using System;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Interfaces
{
    /// <summary>
    /// Vermittelt alle Transport-bezogenen Events und stellt API-Kompatibilität sicher.
    /// </summary>
    public interface ITransportEventService
    {
        void PublishJobEvent(string eventType, TransportJob job);
        IDisposable SubscribeToJobEvents(Action<string, TransportJob> handler);

        void ConnectJobManager(ITransportJobManager jobManager);
        void ConnectPlanningService(ITransportPlanningService planningService);

        // Neue Disconnect-APIs zur sauberen Aufräumung
        void DisconnectJobManager(ITransportJobManager jobManager);
        void DisconnectPlanningService(ITransportPlanningService planningService);

        void AddLegacyJobGeplantHandler(Action<TransportJob> handler);
        void RemoveLegacyJobGeplantHandler(Action<TransportJob> handler);
        void AddLegacyJobGestartetHandler(Action<TransportJob> handler);
        void RemoveLegacyJobGestartetHandler(Action<TransportJob> handler);
        void AddLegacyJobAbgeschlossenHandler(Action<TransportJob> handler);
        void RemoveLegacyJobAbgeschlossenHandler(Action<TransportJob> handler);
        void AddLegacyJobFehlgeschlagenHandler(Action<TransportJob> handler);
        void RemoveLegacyJobFehlgeschlagenHandler(Action<TransportJob> handler);
    }
}

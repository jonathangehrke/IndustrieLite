// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Interfaces
{
    using System;
    using System.Collections.Generic;
    using Godot;
    using IndustrieLite.Transport.Core.Models;

    /// <summary>
    /// Verwaltet Queue und Lebenszyklus der Transport-Jobs (Queue, Status, Events).
    /// </summary>
    public interface ITransportJobManager
    {
        IReadOnlyDictionary<Guid, TransportJob> Jobs { get; }

        TransportJob? HoleNaechstenJob();

        void AddJob(TransportJob job);

        void RequeueJob(Guid jobId);

        void ResetAllJobsToPlanned();

        void MeldeJobGestartet(Guid jobId, object? truckKontext);

        void MeldeJobAbgeschlossen(Guid jobId, int gelieferteMenge);

        void MeldeJobFehlgeschlagen(Guid jobId);

        void CancelJobsForNode(Node node);

        IEnumerable<Guid> HoleJobQueueIds();

        void SetJobQueue(IEnumerable<Guid> jobReihenfolge);

        void EntferneAlleJobs();

        event Action<TransportJob>? JobGestartet;

        event Action<TransportJob>? JobAbgeschlossen;

        event Action<TransportJob>? JobFehlgeschlagen;
    }
}

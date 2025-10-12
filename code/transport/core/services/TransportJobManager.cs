// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IndustrieLite.Transport.Core.Interfaces;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Services
{
    /// <summary>
    /// Verwaltet Queue und Lebenszyklus der Transport-Jobs.
    /// </summary>
    public class TransportJobManager : ITransportJobManager
    {
        private readonly Queue<TransportJob> jobQueue = new Queue<TransportJob>();
        private readonly Dictionary<Guid, TransportJob> jobsById = new Dictionary<Guid, TransportJob>();

        public IReadOnlyDictionary<Guid, TransportJob> Jobs => jobsById;

        public event Action<TransportJob>? JobGestartet;
        public event Action<TransportJob>? JobAbgeschlossen;
        public event Action<TransportJob>? JobFehlgeschlagen;

        /// <summary>
        /// Fuegt einen neuen Transport-Job zur Queue hinzu.
        /// </summary>
        public void AddJob(TransportJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            jobsById[job.JobId] = job;
            jobQueue.Enqueue(job);
        }

        /// <summary>
        /// Entnimmt den naechsten zugewiesenen Job aus der Queue (oder null).
        /// </summary>
        public TransportJob? HoleNaechstenJob()
        {
            int sicherheit = jobQueue.Count;
            while (sicherheit-- > 0 && jobQueue.Count > 0)
            {
                var job = jobQueue.Dequeue();
                if (jobsById.ContainsKey(job.JobId))
                {
                    job.Status = TransportJobStatus.Zugewiesen;
                    return job;
                }

                jobQueue.Enqueue(job);
            }

            return null;
        }

        /// <summary>
        /// Setzt einen Job zurueck in den Geplant-Status und reiht ihn wieder ein.
        /// </summary>
        public void RequeueJob(Guid jobId)
        {
            if (jobsById.TryGetValue(jobId, out var job))
            {
                job.Status = TransportJobStatus.Geplant;
                job.TruckKontext = null;
                jobQueue.Enqueue(job);
            }
        }

        /// <summary>
        /// Resets all jobs (except Abgeschlossen/Fehlgeschlagen) back to Geplant and re-queues them.
        /// Used after LoadGame since trucks are not persisted.
        /// </summary>
        public void ResetAllJobsToPlanned()
        {
            jobQueue.Clear();

            foreach (var job in jobsById.Values)
            {
                if (job.Status != TransportJobStatus.Abgeschlossen &&
                    job.Status != TransportJobStatus.Fehlgeschlagen)
                {
                    job.Status = TransportJobStatus.Geplant;
                    job.TruckKontext = null;
                    jobQueue.Enqueue(job);
                }
            }
        }

        /// <summary>
        /// Markiert einen Job als gestartet und setzt optional den Truck-Kontext.
        /// </summary>
        public void MeldeJobGestartet(Guid jobId, object? truckKontext)
        {
            if (jobsById.TryGetValue(jobId, out var job))
            {
                job.Status = TransportJobStatus.Unterwegs;
                job.TruckKontext = truckKontext;
                JobGestartet?.Invoke(job);
            }
        }

        /// <summary>
        /// Markiert einen Job als abgeschlossen und entfernt ihn aus dem Index.
        /// </summary>
        public void MeldeJobAbgeschlossen(Guid jobId, int gelieferteMenge)
        {
            if (!jobsById.TryGetValue(jobId, out var job))
                return;

            job.Status = TransportJobStatus.Abgeschlossen;
            job.TruckKontext = null;
            jobsById.Remove(jobId);
            JobAbgeschlossen?.Invoke(job);
        }

        /// <summary>
        /// Markiert einen Job als fehlgeschlagen und entfernt ihn aus dem Index.
        /// </summary>
        public void MeldeJobFehlgeschlagen(Guid jobId)
        {
            if (!jobsById.TryGetValue(jobId, out var job))
                return;

            job.Status = TransportJobStatus.Fehlgeschlagen;
            job.TruckKontext = null;
            jobsById.Remove(jobId);
            JobFehlgeschlagen?.Invoke(job);
        }

        /// <summary>
        /// Bricht alle Jobs ab, die den angegebenen Knoten als Quelle oder Ziel nutzen.
        /// </summary>
        public void CancelJobsForNode(Node node)
        {
            if (node == null)
                return;

            int count = jobQueue.Count;
            for (int i = 0; i < count; i++)
            {
                var job = jobQueue.Dequeue();
                if (ReferenceEquals(job.LieferantKontext, node) || ReferenceEquals(job.ZielKontext, node))
                {
                    job.Status = TransportJobStatus.Fehlgeschlagen;
                    jobsById.Remove(job.JobId);
                    JobFehlgeschlagen?.Invoke(job);
                    continue;
                }

                jobQueue.Enqueue(job);
            }

            var laufendeJobs = jobsById.Values
                .Where(j => ReferenceEquals(j.LieferantKontext, node) || ReferenceEquals(j.ZielKontext, node))
                .Select(j => j.JobId)
                .ToList();

            foreach (var laufenderJobId in laufendeJobs)
            {
                MeldeJobFehlgeschlagen(laufenderJobId);
            }
        }

        /// <summary>
        /// Liefert die Job-IDs der aktuellen Queue in Reihenfolge.
        /// </summary>
        public IEnumerable<Guid> HoleJobQueueIds()
        {
            return jobQueue.ToArray().Select(job => job.JobId);
        }

        /// <summary>
        /// Setzt die Reihenfolge der Job-Queue anhand der angegebenen IDs.
        /// </summary>
        public void SetJobQueue(IEnumerable<Guid> jobReihenfolge)
        {
            if (jobReihenfolge == null)
                throw new ArgumentNullException(nameof(jobReihenfolge));

            jobQueue.Clear();
            foreach (var jobId in jobReihenfolge)
            {
                if (jobsById.TryGetValue(jobId, out var job))
                {
                    jobQueue.Enqueue(job);
                }
            }
        }

        /// <summary>
        /// Leert alle Jobs und die Queue.
        /// </summary>
        public void EntferneAlleJobs()
        {
            jobsById.Clear();
            jobQueue.Clear();
        }
    }
}

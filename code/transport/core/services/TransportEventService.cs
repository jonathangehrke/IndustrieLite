// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using IndustrieLite.Transport.Core.Interfaces;
    using IndustrieLite.Transport.Core.Models;

    /// <summary>
    /// Zentralisiert alle Transport-bezogenen Events und stellt Legacy-Kompatibilität bereit.
    /// </summary>
    public class TransportEventService : ITransportEventService, IDisposable
    {
        private readonly object lockObject = new object();
        private readonly List<WeakReference<Action<string, TransportJob>>> handlers = new();

        private readonly List<WeakReference<Action<TransportJob>>> jobGeplantHandlers = new();
        private readonly List<WeakReference<Action<TransportJob>>> jobGestartetHandlers = new();
        private readonly List<WeakReference<Action<TransportJob>>> jobAbgeschlossenHandlers = new();
        private readonly List<WeakReference<Action<TransportJob>>> jobFehlgeschlagenHandlers = new();

        // Aktive Verbindungen zu Core-Services für sauberes Disconnect
        private ITransportJobManager? connectedJobManager;
        private ITransportPlanningService? connectedPlanningService;
        private Action<TransportJob>? dJobGestartet;
        private Action<TransportJob>? dJobAbgeschlossen;
        private Action<TransportJob>? dJobFehlgeschlagen;
        private Action<TransportJob>? dJobGeplant;

        public void PublishJobEvent(string eventType, TransportJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            List<Action<string, TransportJob>> aktuelleHandler;
            List<Action<TransportJob>>? legacyHandlers;

            lock (this.lockObject)
            {
                aktuelleHandler = ExtractAlive(this.handlers, out _);
                legacyHandlers = eventType switch
                {
                    "JobGeplant" => ExtractAlive(this.jobGeplantHandlers, out _),
                    "JobGestartet" => ExtractAlive(this.jobGestartetHandlers, out _),
                    "JobAbgeschlossen" => ExtractAlive(this.jobAbgeschlossenHandlers, out _),
                    "JobFehlgeschlagen" => ExtractAlive(this.jobFehlgeschlagenHandlers, out _),
                    _ => null,
                };
            }

            foreach (var handler in aktuelleHandler)
            {
                handler(eventType, job);
            }

            if (legacyHandlers == null)
            {
                return;
            }

            foreach (var handler in legacyHandlers)
            {
                handler(job);
            }
        }

        public IDisposable SubscribeToJobEvents(Action<string, TransportJob> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            WeakReference<Action<string, TransportJob>> weak;
            lock (this.lockObject)
            {
                weak = new WeakReference<Action<string, TransportJob>>(handler);
                this.handlers.Add(weak);
            }

            return new EventSubscription(() =>
            {
                lock (this.lockObject)
                {
                    RemoveWeak(this.handlers, handler);
                }
            });
        }

        public void ConnectJobManager(ITransportJobManager jobManager)
        {
            if (jobManager == null)
            {
                throw new ArgumentNullException(nameof(jobManager));
            }

            // Bereits bestehende Verbindung lösen
            if (this.connectedJobManager != null)
            {
                this.DisconnectJobManager(this.connectedJobManager);
            }

            this.dJobGestartet = job => this.PublishJobEvent("JobGestartet", job);
            this.dJobAbgeschlossen = job => this.PublishJobEvent("JobAbgeschlossen", job);
            this.dJobFehlgeschlagen = job => this.PublishJobEvent("JobFehlgeschlagen", job);
            jobManager.JobGestartet += this.dJobGestartet;
            jobManager.JobAbgeschlossen += this.dJobAbgeschlossen;
            jobManager.JobFehlgeschlagen += this.dJobFehlgeschlagen;
            this.connectedJobManager = jobManager;
        }

        public void ConnectPlanningService(ITransportPlanningService planningService)
        {
            if (planningService == null)
            {
                throw new ArgumentNullException(nameof(planningService));
            }

            if (this.connectedPlanningService != null)
            {
                this.DisconnectPlanningService(this.connectedPlanningService);
            }

            this.dJobGeplant = job => this.PublishJobEvent("JobGeplant", job);
            planningService.JobGeplant += this.dJobGeplant;
            this.connectedPlanningService = planningService;
        }

        public void DisconnectJobManager(ITransportJobManager jobManager)
        {
            if (jobManager == null)
            {
                return;
            }

            if (!ReferenceEquals(this.connectedJobManager, jobManager))
            {
                return;
            }

            try
            {
                if (this.dJobGestartet != null)
                {
                    jobManager.JobGestartet -= this.dJobGestartet;
                }

                if (this.dJobAbgeschlossen != null)
                {
                    jobManager.JobAbgeschlossen -= this.dJobAbgeschlossen;
                }

                if (this.dJobFehlgeschlagen != null)
                {
                    jobManager.JobFehlgeschlagen -= this.dJobFehlgeschlagen;
                }
            }
            catch
            {
            }
            finally
            {
                this.dJobGestartet = null;
                this.dJobAbgeschlossen = null;
                this.dJobFehlgeschlagen = null;
                this.connectedJobManager = null;
            }
        }

        public void DisconnectPlanningService(ITransportPlanningService planningService)
        {
            if (planningService == null)
            {
                return;
            }

            if (!ReferenceEquals(this.connectedPlanningService, planningService))
            {
                return;
            }

            try
            {
                if (this.dJobGeplant != null)
                {
                    planningService.JobGeplant -= this.dJobGeplant;
                }
            }
            catch
            {
            }
            finally
            {
                this.dJobGeplant = null;
                this.connectedPlanningService = null;
            }
        }

        public void AddLegacyJobGeplantHandler(Action<TransportJob> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (this.lockObject)
            {
                this.jobGeplantHandlers.Add(new WeakReference<Action<TransportJob>>(handler));
            }
        }

        public void RemoveLegacyJobGeplantHandler(Action<TransportJob> handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (this.lockObject)
            {
                RemoveWeak(this.jobGeplantHandlers, handler);
            }
        }

        public void AddLegacyJobGestartetHandler(Action<TransportJob> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (this.lockObject)
            {
                this.jobGestartetHandlers.Add(new WeakReference<Action<TransportJob>>(handler));
            }
        }

        public void RemoveLegacyJobGestartetHandler(Action<TransportJob> handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (this.lockObject)
            {
                RemoveWeak(this.jobGestartetHandlers, handler);
            }
        }

        public void AddLegacyJobAbgeschlossenHandler(Action<TransportJob> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (this.lockObject)
            {
                this.jobAbgeschlossenHandlers.Add(new WeakReference<Action<TransportJob>>(handler));
            }
        }

        public void RemoveLegacyJobAbgeschlossenHandler(Action<TransportJob> handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (this.lockObject)
            {
                RemoveWeak(this.jobAbgeschlossenHandlers, handler);
            }
        }

        public void AddLegacyJobFehlgeschlagenHandler(Action<TransportJob> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (this.lockObject)
            {
                this.jobFehlgeschlagenHandlers.Add(new WeakReference<Action<TransportJob>>(handler));
            }
        }

        public void RemoveLegacyJobFehlgeschlagenHandler(Action<TransportJob> handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (this.lockObject)
            {
                RemoveWeak(this.jobFehlgeschlagenHandlers, handler);
            }
        }

        // -- WeakReference Utilities --
        private static List<Action<string, TransportJob>> ExtractAlive(List<WeakReference<Action<string, TransportJob>>> list, out int removed)
        {
            removed = 0;
            var result = new List<Action<string, TransportJob>>(list.Count);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].TryGetTarget(out var h) && h != null)
                {
                    result.Add(h);
                }
                else
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }
            result.Reverse();
            return result;
        }

        private static List<Action<TransportJob>> ExtractAlive(List<WeakReference<Action<TransportJob>>> list, out int removed)
        {
            removed = 0;
            var result = new List<Action<TransportJob>>(list.Count);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].TryGetTarget(out var h) && h != null)
                {
                    result.Add(h);
                }
                else
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }
            result.Reverse();
            return result;
        }

        private static void RemoveWeak(List<WeakReference<Action<string, TransportJob>>> list, Action<string, TransportJob> target)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].TryGetTarget(out var h) || h == target)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private static void RemoveWeak(List<WeakReference<Action<TransportJob>>> list, Action<TransportJob> target)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].TryGetTarget(out var h) || h == target)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private sealed class EventSubscription : IDisposable
        {
            private readonly Action unsubscribe;
            private bool disposed;

            public EventSubscription(Action unsubscribe)
            {
                this.unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
            }

            public void Dispose()
            {
                if (this.disposed)
                {
                    return;
                }

                this.unsubscribe();
                this.disposed = true;
            }
        }

        public void Dispose()
        {
            // Saubere Trennung von Core-Eventquellen
            if (this.connectedJobManager != null)
            {
                this.DisconnectJobManager(this.connectedJobManager);
            }

            if (this.connectedPlanningService != null)
            {
                this.DisconnectPlanningService(this.connectedPlanningService);
            }

            // Schwache Handler-Listen bereinigen
            lock (this.lockObject)
            {
                this.handlers.Clear();
                this.jobGeplantHandlers.Clear();
                this.jobGestartetHandlers.Clear();
                this.jobAbgeschlossenHandlers.Clear();
                this.jobFehlgeschlagenHandlers.Clear();
            }
        }
    }
}

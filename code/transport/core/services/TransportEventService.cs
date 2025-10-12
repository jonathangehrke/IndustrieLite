// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using IndustrieLite.Transport.Core.Interfaces;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Services
{
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
                throw new ArgumentNullException(nameof(job));

            List<Action<string, TransportJob>> aktuelleHandler;
            List<Action<TransportJob>>? legacyHandlers;

            lock (lockObject)
            {
                aktuelleHandler = ExtractAlive(handlers, out _);
                legacyHandlers = eventType switch
                {
                    "JobGeplant" => ExtractAlive(jobGeplantHandlers, out _),
                    "JobGestartet" => ExtractAlive(jobGestartetHandlers, out _),
                    "JobAbgeschlossen" => ExtractAlive(jobAbgeschlossenHandlers, out _),
                    "JobFehlgeschlagen" => ExtractAlive(jobFehlgeschlagenHandlers, out _),
                    _ => null
                };
            }

            foreach (var handler in aktuelleHandler)
            {
                handler(eventType, job);
            }

            if (legacyHandlers == null)
                return;

            foreach (var handler in legacyHandlers)
            {
                handler(job);
            }
        }

        public IDisposable SubscribeToJobEvents(Action<string, TransportJob> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            WeakReference<Action<string, TransportJob>> weak;
            lock (lockObject)
            {
                weak = new WeakReference<Action<string, TransportJob>>(handler);
                handlers.Add(weak);
            }

            return new EventSubscription(() =>
            {
                lock (lockObject)
                {
                    RemoveWeak(handlers, handler);
                }
            });
        }

        public void ConnectJobManager(ITransportJobManager jobManager)
        {
            if (jobManager == null)
                throw new ArgumentNullException(nameof(jobManager));

            // Bereits bestehende Verbindung lösen
            if (connectedJobManager != null)
                DisconnectJobManager(connectedJobManager);

            dJobGestartet = job => PublishJobEvent("JobGestartet", job);
            dJobAbgeschlossen = job => PublishJobEvent("JobAbgeschlossen", job);
            dJobFehlgeschlagen = job => PublishJobEvent("JobFehlgeschlagen", job);
            jobManager.JobGestartet += dJobGestartet;
            jobManager.JobAbgeschlossen += dJobAbgeschlossen;
            jobManager.JobFehlgeschlagen += dJobFehlgeschlagen;
            connectedJobManager = jobManager;
        }

        public void ConnectPlanningService(ITransportPlanningService planningService)
        {
            if (planningService == null)
                throw new ArgumentNullException(nameof(planningService));

            if (connectedPlanningService != null)
                DisconnectPlanningService(connectedPlanningService);

            dJobGeplant = job => PublishJobEvent("JobGeplant", job);
            planningService.JobGeplant += dJobGeplant;
            connectedPlanningService = planningService;
        }

        public void DisconnectJobManager(ITransportJobManager jobManager)
        {
            if (jobManager == null)
                return;
            if (!ReferenceEquals(connectedJobManager, jobManager))
                return;
            try
            {
                if (dJobGestartet != null) jobManager.JobGestartet -= dJobGestartet;
                if (dJobAbgeschlossen != null) jobManager.JobAbgeschlossen -= dJobAbgeschlossen;
                if (dJobFehlgeschlagen != null) jobManager.JobFehlgeschlagen -= dJobFehlgeschlagen;
            }
            catch { }
            finally
            {
                dJobGestartet = null;
                dJobAbgeschlossen = null;
                dJobFehlgeschlagen = null;
                connectedJobManager = null;
            }
        }

        public void DisconnectPlanningService(ITransportPlanningService planningService)
        {
            if (planningService == null)
                return;
            if (!ReferenceEquals(connectedPlanningService, planningService))
                return;
            try
            {
                if (dJobGeplant != null) planningService.JobGeplant -= dJobGeplant;
            }
            catch { }
            finally
            {
                dJobGeplant = null;
                connectedPlanningService = null;
            }
        }

        public void AddLegacyJobGeplantHandler(Action<TransportJob> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            lock (lockObject)
            {
                jobGeplantHandlers.Add(new WeakReference<Action<TransportJob>>(handler));
            }
        }

        public void RemoveLegacyJobGeplantHandler(Action<TransportJob> handler)
        {
            if (handler == null)
                return;
            lock (lockObject)
            {
                RemoveWeak(jobGeplantHandlers, handler);
            }
        }

        public void AddLegacyJobGestartetHandler(Action<TransportJob> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            lock (lockObject)
            {
                jobGestartetHandlers.Add(new WeakReference<Action<TransportJob>>(handler));
            }
        }

        public void RemoveLegacyJobGestartetHandler(Action<TransportJob> handler)
        {
            if (handler == null)
                return;
            lock (lockObject)
            {
                RemoveWeak(jobGestartetHandlers, handler);
            }
        }

        public void AddLegacyJobAbgeschlossenHandler(Action<TransportJob> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            lock (lockObject)
            {
                jobAbgeschlossenHandlers.Add(new WeakReference<Action<TransportJob>>(handler));
            }
        }

        public void RemoveLegacyJobAbgeschlossenHandler(Action<TransportJob> handler)
        {
            if (handler == null)
                return;
            lock (lockObject)
            {
                RemoveWeak(jobAbgeschlossenHandlers, handler);
            }
        }

        public void AddLegacyJobFehlgeschlagenHandler(Action<TransportJob> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            lock (lockObject)
            {
                jobFehlgeschlagenHandlers.Add(new WeakReference<Action<TransportJob>>(handler));
            }
        }

        public void RemoveLegacyJobFehlgeschlagenHandler(Action<TransportJob> handler)
        {
            if (handler == null)
                return;
            lock (lockObject)
            {
                RemoveWeak(jobFehlgeschlagenHandlers, handler);
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
                if (disposed)
                    return;

                unsubscribe();
                disposed = true;
            }
        }

        public void Dispose()
        {
            // Saubere Trennung von Core-Eventquellen
            if (connectedJobManager != null)
                DisconnectJobManager(connectedJobManager);
            if (connectedPlanningService != null)
                DisconnectPlanningService(connectedPlanningService);

            // Schwache Handler-Listen bereinigen
            lock (lockObject)
            {
                handlers.Clear();
                jobGeplantHandlers.Clear();
                jobGestartetHandlers.Clear();
                jobAbgeschlossenHandlers.Clear();
                jobFehlgeschlagenHandlers.Clear();
            }
        }
    }
}

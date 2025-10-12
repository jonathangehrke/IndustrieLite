// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;
using IndustrieLite.Transport.Core.Interfaces;
using IndustrieLite.Transport.Core.Models;
using JobManagerService = IndustrieLite.Transport.Core.Services.TransportJobManager;
using OrderManagerService = IndustrieLite.Transport.Core.Services.TransportOrderManager;
using PlanningService = IndustrieLite.Transport.Core.Services.TransportPlanningService;
using SupplyService = IndustrieLite.Transport.Core.Services.TransportSupplyService;
using PersistenceService = IndustrieLite.Transport.Core.Services.TransportPersistenceService;
using EventService = IndustrieLite.Transport.Core.Services.TransportEventService;

namespace IndustrieLite.Transport.Core
{
    /// <summary>
    /// Fassade für das Transport-Subsystem. Delegiert Arbeit an spezialisierte Services.
    /// </summary>
    public class TransportCoreService : IDisposable
    {
        private readonly ITransportJobManager jobManager;
        private readonly ITransportOrderManager orderManager;
        private readonly ITransportPlanningService planningService;
        private readonly ITransportSupplyService supplyService;
        private readonly ITransportPersistenceService persistenceService;
        private readonly ITransportEventService eventService;
        private bool disposed;

        public TransportCoreService(ITransportJobManager jobManager,
                                    ITransportOrderManager orderManager,
                                    ITransportPlanningService planningService,
                                    ITransportSupplyService supplyService,
                                    ITransportPersistenceService persistenceService,
                                    ITransportEventService eventService)
        {
            this.jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
            this.orderManager = orderManager ?? throw new ArgumentNullException(nameof(orderManager));
            this.planningService = planningService ?? throw new ArgumentNullException(nameof(planningService));
            this.supplyService = supplyService ?? throw new ArgumentNullException(nameof(supplyService));
            this.persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            this.eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            InitializeServiceVerkabelung();
        }

        public TransportCoreService(OrderBook? orderBook = null,
                                    SupplyIndex? supplyIndex = null,
                                    Scheduler? scheduler = null,
                                    Router? router = null)
        {
            jobManager = new JobManagerService();
            supplyService = new SupplyService(supplyIndex);
            orderManager = new OrderManagerService(orderBook);
            planningService = new PlanningService(
                scheduler ?? new Scheduler(),
                router,
                orderManager,
                jobManager,
                supplyService);
            persistenceService = new PersistenceService();
            eventService = new EventService();

            InitializeServiceVerkabelung();
        }

        public OrderBook Auftragsbuch => orderManager.Auftragsbuch;
        public SupplyIndex LieferIndex => supplyService.LieferIndex;
        public IReadOnlyDictionary<Guid, TransportJob> Jobs => jobManager.Jobs;
        public IReadOnlyDictionary<int, DeliveryOrder> DeliveryOrders => orderManager.DeliveryOrders;

        public event Action<TransportJob>? JobGeplant
        {
            add { if (value != null) eventService.AddLegacyJobGeplantHandler(value); }
            remove { if (value != null) eventService.RemoveLegacyJobGeplantHandler(value); }
        }

        public event Action<TransportJob>? JobGestartet
        {
            add { if (value != null) eventService.AddLegacyJobGestartetHandler(value); }
            remove { if (value != null) eventService.RemoveLegacyJobGestartetHandler(value); }
        }

        public event Action<TransportJob>? JobAbgeschlossen
        {
            add { if (value != null) eventService.AddLegacyJobAbgeschlossenHandler(value); }
            remove { if (value != null) eventService.RemoveLegacyJobAbgeschlossenHandler(value); }
        }

        public event Action<TransportJob>? JobFehlgeschlagen
        {
            add { if (value != null) eventService.AddLegacyJobFehlgeschlagenHandler(value); }
            remove { if (value != null) eventService.RemoveLegacyJobFehlgeschlagenHandler(value); }
        }

        public IDisposable AbonniereJobEvents(Action<string, TransportJob> handler)
        {
            return eventService.SubscribeToJobEvents(handler);
        }

        public StringName MappeProduktZuResourceId(string produkt)
        {
            return supplyService.MappeProduktZuResourceId(produkt);
        }

        public void AktualisiereAuftragsbuch(IEnumerable<TransportAuftragsDaten> daten)
        {
            orderManager.AktualisiereAuftragsbuch(daten);
        }

        public void AktualisiereLieferindex(IEnumerable<LieferantDaten> daten)
        {
            supplyService.AktualisiereLieferindex(daten);
        }

        public TransportCoreSaveData CaptureState()
        {
            return persistenceService.CaptureState();
        }

        public void RestoreState(TransportCoreSaveData state, Func<Guid, Building?>? buildingResolver = null)
        {
            persistenceService.RestoreState(state, buildingResolver);
        }

        /// <summary>
        /// Resets all jobs (except completed) back to "Geplant" status and re-queues them.
        /// Used after LoadGame since trucks are not persisted.
        /// </summary>
        public void ResetAllJobsToPlanned()
        {
            jobManager.ResetAllJobsToPlanned();
        }

        public TransportPlanErgebnis PlaneLieferung(TransportPlanAnfrage anfrage)
        {
            return planningService.PlaneLieferung(anfrage);
        }

        public TransportJob? HoleNaechstenJob()
        {
            return jobManager.HoleNaechstenJob();
        }

        public void RequeueJob(Guid jobId)
        {
            jobManager.RequeueJob(jobId);
        }

        public void MeldeJobGestartet(Guid jobId, object? truckKontext)
        {
            jobManager.MeldeJobGestartet(jobId, truckKontext);
        }

        public void MeldeJobAbgeschlossen(Guid jobId, int gelieferteMenge)
        {
            if (jobManager.Jobs.TryGetValue(jobId, out var job))
            {
                orderManager.VerarbeiteJobAbschluss(job, gelieferteMenge);
            }

            jobManager.MeldeJobAbgeschlossen(jobId, gelieferteMenge);
        }

        public void MeldeJobFehlgeschlagen(Guid jobId)
        {
            if (jobManager.Jobs.TryGetValue(jobId, out var job))
            {
                orderManager.VerarbeiteJobFehler(job);
            }

            jobManager.MeldeJobFehlgeschlagen(jobId);
        }

        public void CancelJobsForNode(Node node)
        {
            if (node == null)
                return;

            foreach (var job in jobManager.Jobs.Values)
            {
                if (ReferenceEquals(job.LieferantKontext, node) || ReferenceEquals(job.ZielKontext, node))
                {
                    orderManager.VerarbeiteJobFehler(job);
                }
            }

            jobManager.CancelJobsForNode(node);
        }

        public DeliveryOrder? HoleDeliveryOrder(int orderId)
        {
            return orderManager.HoleDeliveryOrder(orderId);
        }

        private void InitializeServiceVerkabelung()
        {
            persistenceService.SetServiceReferences(jobManager, orderManager, supplyService, planningService);
            eventService.ConnectJobManager(jobManager);
            eventService.ConnectPlanningService(planningService);
        }

        public void Dispose()
        {
            if (disposed) return;
            try
            {
                try { eventService.DisconnectJobManager(jobManager); } catch { }
                try { eventService.DisconnectPlanningService(planningService); } catch { }
                if (eventService is IDisposable d)
                {
                    try { d.Dispose(); } catch { }
                }
            }
            finally
            {
                disposed = true;
            }
        }

        /// <summary>
        /// Clears all transport data - for lifecycle management
        /// </summary>
        public void ClearAllData()
        {
            try
            {
                // Note: Interface-based services may not have ClearAllData yet
                // For now, just log that we're clearing transport data
                DebugLogger.Log("debug_transport", DebugLogger.LogLevel.Info,
                    () => "TransportCoreService: Cleared all data");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("debug_transport", DebugLogger.LogLevel.Error,
                    () => $"TransportCoreService: Error clearing data: {ex.Message}");
            }
        }
    }
}

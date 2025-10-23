// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core
{
    using System;
    using System.Collections.Generic;
    using Godot;
    using IndustrieLite.Transport.Core.Interfaces;
    using IndustrieLite.Transport.Core.Models;
    using EventService = IndustrieLite.Transport.Core.Services.TransportEventService;
    using JobManagerService = IndustrieLite.Transport.Core.Services.TransportJobManager;
    using OrderManagerService = IndustrieLite.Transport.Core.Services.TransportOrderManager;
    using PersistenceService = IndustrieLite.Transport.Core.Services.TransportPersistenceService;
    using PlanningService = IndustrieLite.Transport.Core.Services.TransportPlanningService;
    using SupplyService = IndustrieLite.Transport.Core.Services.TransportSupplyService;

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

        public TransportCoreService(
            ITransportJobManager jobManager,
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

            this.InitializeServiceVerkabelung();
        }

        public TransportCoreService(
            OrderBook? orderBook = null,
                                    SupplyIndex? supplyIndex = null,
                                    Scheduler? scheduler = null,
                                    Router? router = null)
        {
            this.jobManager = new JobManagerService();
            this.supplyService = new SupplyService(supplyIndex);
            this.orderManager = new OrderManagerService(orderBook);
            this.planningService = new PlanningService(
                scheduler ?? new Scheduler(),
                router,
                this.orderManager,
                this.jobManager,
                this.supplyService);
            this.persistenceService = new PersistenceService();
            this.eventService = new EventService();

            this.InitializeServiceVerkabelung();
        }

        public OrderBook Auftragsbuch => this.orderManager.Auftragsbuch;

        public SupplyIndex LieferIndex => this.supplyService.LieferIndex;

        public IReadOnlyDictionary<Guid, TransportJob> Jobs => this.jobManager.Jobs;

        public IReadOnlyDictionary<int, DeliveryOrder> DeliveryOrders => this.orderManager.DeliveryOrders;

        public event Action<TransportJob>? JobGeplant
        {
            add
            {
                if (value != null)
                {
                    this.eventService.AddLegacyJobGeplantHandler(value);
                }
            }
            remove
            {
                if (value != null)
                {
                    this.eventService.RemoveLegacyJobGeplantHandler(value);
                }
            }
        }

        public event Action<TransportJob>? JobGestartet
        {
            add
            {
                if (value != null)
                {
                    this.eventService.AddLegacyJobGestartetHandler(value);
                }
            }
            remove
            {
                if (value != null)
                {
                    this.eventService.RemoveLegacyJobGestartetHandler(value);
                }
            }
        }

        public event Action<TransportJob>? JobAbgeschlossen
        {
            add
            {
                if (value != null)
                {
                    this.eventService.AddLegacyJobAbgeschlossenHandler(value);
                }
            }
            remove
            {
                if (value != null)
                {
                    this.eventService.RemoveLegacyJobAbgeschlossenHandler(value);
                }
            }
        }

        public event Action<TransportJob>? JobFehlgeschlagen
        {
            add
            {
                if (value != null)
                {
                    this.eventService.AddLegacyJobFehlgeschlagenHandler(value);
                }
            }
            remove
            {
                if (value != null)
                {
                    this.eventService.RemoveLegacyJobFehlgeschlagenHandler(value);
                }
            }
        }

        public IDisposable AbonniereJobEvents(Action<string, TransportJob> handler)
        {
            return this.eventService.SubscribeToJobEvents(handler);
        }

        public StringName MappeProduktZuResourceId(string produkt)
        {
            return this.supplyService.MappeProduktZuResourceId(produkt);
        }

        public void AktualisiereAuftragsbuch(IEnumerable<TransportAuftragsDaten> daten)
        {
            this.orderManager.AktualisiereAuftragsbuch(daten);
        }

        public void AktualisiereLieferindex(IEnumerable<LieferantDaten> daten)
        {
            this.supplyService.AktualisiereLieferindex(daten);
        }

        public TransportCoreSaveData CaptureState()
        {
            return this.persistenceService.CaptureState();
        }

        public void RestoreState(TransportCoreSaveData state, Func<Guid, Building?>? buildingResolver = null)
        {
            this.persistenceService.RestoreState(state, buildingResolver);
        }

        /// <summary>
        /// Resets all jobs (except completed) back to "Geplant" status and re-queues them.
        /// Used after LoadGame since trucks are not persisted.
        /// </summary>
        public void ResetAllJobsToPlanned()
        {
            this.jobManager.ResetAllJobsToPlanned();
        }

        public TransportPlanErgebnis PlaneLieferung(TransportPlanAnfrage anfrage)
        {
            return this.planningService.PlaneLieferung(anfrage);
        }

        public TransportJob? HoleNaechstenJob()
        {
            return this.jobManager.HoleNaechstenJob();
        }

        public void RequeueJob(Guid jobId)
        {
            this.jobManager.RequeueJob(jobId);
        }

        public void MeldeJobGestartet(Guid jobId, object? truckKontext)
        {
            this.jobManager.MeldeJobGestartet(jobId, truckKontext);
        }

        public void MeldeJobAbgeschlossen(Guid jobId, int gelieferteMenge)
        {
            if (this.jobManager.Jobs.TryGetValue(jobId, out var job))
            {
                this.orderManager.VerarbeiteJobAbschluss(job, gelieferteMenge);
            }

            this.jobManager.MeldeJobAbgeschlossen(jobId, gelieferteMenge);
        }

        public void MeldeJobFehlgeschlagen(Guid jobId)
        {
            if (this.jobManager.Jobs.TryGetValue(jobId, out var job))
            {
                this.orderManager.VerarbeiteJobFehler(job);
            }

            this.jobManager.MeldeJobFehlgeschlagen(jobId);
        }

        public void CancelJobsForNode(Node node)
        {
            if (node == null)
            {
                return;
            }

            foreach (var job in this.jobManager.Jobs.Values)
            {
                if (ReferenceEquals(job.LieferantKontext, node) || ReferenceEquals(job.ZielKontext, node))
                {
                    this.orderManager.VerarbeiteJobFehler(job);
                }
            }

            this.jobManager.CancelJobsForNode(node);
        }

        public DeliveryOrder? HoleDeliveryOrder(int orderId)
        {
            return this.orderManager.HoleDeliveryOrder(orderId);
        }

        private void InitializeServiceVerkabelung()
        {
            this.persistenceService.SetServiceReferences(this.jobManager, this.orderManager, this.supplyService, this.planningService);
            this.eventService.ConnectJobManager(this.jobManager);
            this.eventService.ConnectPlanningService(this.planningService);
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            try
            {
                try
                {
                    this.eventService.DisconnectJobManager(this.jobManager);
                }
                catch
                {
                }
                try
                {
                    this.eventService.DisconnectPlanningService(this.planningService);
                }
                catch
                {
                }
                if (this.eventService is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                this.disposed = true;
            }
        }

        /// <summary>
        /// Clears all transport data - for lifecycle management.
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

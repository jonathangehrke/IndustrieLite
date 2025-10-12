// SPDX-License-Identifier: MIT
using System;
using Godot;
using IndustrieLite.Transport.Core.Interfaces;
using IndustrieLite.Transport.Core.Models;

namespace IndustrieLite.Transport.Core.Services
{
    /// <summary>
    /// Übernimmt Save/Load für das Transport-Subsystem.
    /// </summary>
    public class TransportPersistenceService : ITransportPersistenceService
    {
        private ITransportJobManager? jobManager;
        private ITransportOrderManager? orderManager;

        public void SetServiceReferences(ITransportJobManager jobManager,
                                         ITransportOrderManager orderManager,
                                         ITransportSupplyService supplyService,
                                         ITransportPlanningService planningService)
        {
            this.jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
            this.orderManager = orderManager ?? throw new ArgumentNullException(nameof(orderManager));
            _ = supplyService ?? throw new ArgumentNullException(nameof(supplyService));
            _ = planningService ?? throw new ArgumentNullException(nameof(planningService));
        }

        public TransportCoreSaveData CaptureState()
        {
            if (jobManager == null || orderManager == null)
                throw new InvalidOperationException("Services für Persistenz wurden nicht initialisiert.");

            var save = new TransportCoreSaveData();

            foreach (var job in jobManager.Jobs.Values)
            {
                var jobData = new TransportJobSaveData
                {
                    JobId = job.JobId,
                    OrderId = job.OrderId,
                    ResourceId = job.ResourceId.ToString(),
                    Menge = job.Menge,
                    Transportkosten = job.Transportkosten,
                    PreisProEinheit = job.PreisProEinheit,
                    StartPosition = job.StartPosition,
                    ZielPosition = job.ZielPosition,
                    Status = job.Status
                };

                if (job.LieferantKontext is Building supplier && Guid.TryParse(supplier.BuildingId, out var supplierGuid))
                    jobData.SupplierBuildingId = supplierGuid;
                if (job.ZielKontext is Building ziel && Guid.TryParse(ziel.BuildingId, out var zielGuid))
                    jobData.TargetBuildingId = zielGuid;

                save.Jobs.Add(jobData);
            }

            foreach (var jobId in jobManager.HoleJobQueueIds())
            {
                save.JobQueue.Add(jobId);
            }

            foreach (var order in orderManager.DeliveryOrders.Values)
            {
                var orderData = new DeliveryOrderSaveData
                {
                    OrderId = order.OrderId,
                    ResourceId = order.ResourceId.ToString(),
                    Produkt = order.Produkt,
                    Gesamtmenge = order.Gesamtmenge,
                    Remaining = order.Remaining,
                    PreisProEinheit = order.PreisProEinheit,
                    Status = order.Status
                };
                orderData.JobIds.AddRange(order.JobIds);
                save.DeliveryOrders.Add(orderData);
            }

            return save;
        }

        public void RestoreState(TransportCoreSaveData state, Func<Guid, Building?>? buildingResolver = null)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (jobManager == null || orderManager == null)
                throw new InvalidOperationException("Services für Persistenz wurden nicht initialisiert.");

            jobManager.EntferneAlleJobs();
            orderManager.EntferneAlleOrders();

            foreach (var orderData in state.DeliveryOrders)
            {
                var resourceId = string.IsNullOrEmpty(orderData.ResourceId)
                    ? new StringName()
                    : new StringName(orderData.ResourceId);

                var delivery = new DeliveryOrder(
                    orderData.OrderId,
                    resourceId,
                    orderData.Produkt,
                    orderData.Gesamtmenge,
                    orderData.PreisProEinheit,
                    null,
                    null)
                {
                    Remaining = orderData.Remaining,
                    Status = orderData.Status
                };

                foreach (var jobId in orderData.JobIds)
                {
                    delivery.JobIds.Add(jobId);
                }

                orderManager.RegistriereWiederhergestelltenAuftrag(delivery);
            }

            foreach (var jobData in state.Jobs)
            {
                var job = new TransportJob
                {
                    JobId = jobData.JobId == Guid.Empty ? Guid.NewGuid() : jobData.JobId,
                    OrderId = jobData.OrderId,
                    ResourceId = string.IsNullOrEmpty(jobData.ResourceId) ? new StringName() : new StringName(jobData.ResourceId),
                    Menge = jobData.Menge,
                    Transportkosten = jobData.Transportkosten,
                    PreisProEinheit = jobData.PreisProEinheit,
                    StartPosition = jobData.StartPosition,
                    ZielPosition = jobData.ZielPosition,
                    Status = jobData.Status,
                    LieferantKontext = jobData.SupplierBuildingId.HasValue ? buildingResolver?.Invoke(jobData.SupplierBuildingId.Value) : null,
                    ZielKontext = jobData.TargetBuildingId.HasValue ? buildingResolver?.Invoke(jobData.TargetBuildingId.Value) : null
                };

                jobManager.AddJob(job);
            }

            jobManager.SetJobQueue(state.JobQueue);
        }
    }
}

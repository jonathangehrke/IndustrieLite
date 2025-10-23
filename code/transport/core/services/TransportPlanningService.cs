// SPDX-License-Identifier: MIT
namespace IndustrieLite.Transport.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Godot;
    using IndustrieLite.Transport.Core.Interfaces;
    using IndustrieLite.Transport.Core.Models;

    /// <summary>
    /// Kümmert sich um Planung und Kostenberechnung von Transport-Jobs.
    /// </summary>
    public class TransportPlanningService : ITransportPlanningService
    {
        private readonly Scheduler planer;
        private readonly Router? router;
        private readonly ITransportOrderManager orderManager;
        private readonly ITransportJobManager jobManager;
        private readonly ITransportSupplyService supplyService;

        public TransportPlanningService(
            Scheduler scheduler,
                                        Router? router,
                                        ITransportOrderManager orderManager,
                                        ITransportJobManager jobManager,
                                        ITransportSupplyService supplyService)
        {
            this.planer = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.router = router;
            this.orderManager = orderManager ?? throw new ArgumentNullException(nameof(orderManager));
            this.jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
            this.supplyService = supplyService ?? throw new ArgumentNullException(nameof(supplyService));
        }

        public event Action<TransportJob>? JobGeplant;

        public TransportPlanErgebnis PlaneLieferung(TransportPlanAnfrage anfrage)
        {
            if (anfrage == null)
            {
                throw new ArgumentNullException(nameof(anfrage));
            }

            if (anfrage.Auftrag == null)
            {
                throw new ArgumentNullException(nameof(anfrage.Auftrag));
            }

            var lieferantenListe = anfrage.Lieferanten?.ToList() ?? new List<LieferantDaten>();
            if (lieferantenListe.Count == 0)
            {
                return this.ErzeugeFehler(anfrage, "Keine Lieferanten verfügbar");
            }

            double gesamtVerfuegbar = lieferantenListe.Sum(l => l.VerfuegbareMenge);
            if (gesamtVerfuegbar < anfrage.Auftrag.Gesamtmenge)
            {
                return this.ErzeugeFehler(anfrage, $"Nicht genug Bestand: benötigt {anfrage.Auftrag.Gesamtmenge}, verfügbar {gesamtVerfuegbar:F0}");
            }

            this.supplyService.AktualisiereLieferindex(lieferantenListe);
            var suppliers = this.supplyService.LieferIndex.GetSuppliers(anfrage.Auftrag.ResourceId);
            var plan = this.planer.Plan(anfrage.Auftrag.ResourceId, suppliers, anfrage.Auftrag.Gesamtmenge, anfrage.MaxMengeProTruck);

            int geplanteMenge = plan.Sum(p => p.Menge);
            if (geplanteMenge < anfrage.Auftrag.Gesamtmenge)
            {
                return this.ErzeugeFehler(anfrage, $"Planung unvollständig: benötigt {anfrage.Auftrag.Gesamtmenge}, geplant {geplanteMenge}");
            }

            var ergebnis = new TransportPlanErgebnis
            {
                AuftragId = anfrage.Auftrag.AuftragId,
                Erfolgreich = true,
                GeplanteMenge = geplanteMenge,
            };

            var deliveryOrder = this.orderManager.EnsureDeliveryOrder(anfrage.Auftrag);
            deliveryOrder.Status = DeliveryOrderStatus.InTransport;

            foreach (var (lieferant, menge) in plan)
            {
                double kosten = this.BerechneTransportkosten(anfrage, lieferant, menge);
                this.orderManager.Auftragsbuch.Reserve(anfrage.Auftrag.AuftragId, menge);
                this.supplyService.LieferIndex.Reserve(anfrage.Auftrag.ResourceId, lieferant, menge);

                var job = new TransportJob
                {
                    OrderId = anfrage.Auftrag.AuftragId,
                    ResourceId = anfrage.Auftrag.ResourceId,
                    Menge = menge,
                    Transportkosten = kosten,
                    PreisProEinheit = anfrage.Auftrag.PreisProEinheit,
                    StartPosition = lieferant.Position,
                    ZielPosition = anfrage.Auftrag.ZielPosition,
                    LieferantKontext = lieferant.Kontext,
                    ZielKontext = anfrage.Auftrag.ZielReferenz,
                    Status = TransportJobStatus.Geplant,
                };

                deliveryOrder.JobIds.Add(job.JobId);
                this.jobManager.AddJob(job);
                this.JobGeplant?.Invoke(job);

                ergebnis.Jobs.Add(job);
                ergebnis.Gesamtkosten += kosten;
            }

            return ergebnis;
        }

        private TransportPlanErgebnis ErzeugeFehler(TransportPlanAnfrage anfrage, string meldung)
        {
            return new TransportPlanErgebnis
            {
                AuftragId = anfrage.Auftrag?.AuftragId ?? -1,
                Erfolgreich = false,
                Meldung = meldung,
            };
        }

        private double BerechneTransportkosten(TransportPlanAnfrage anfrage, SupplyIndex.Supplier lieferant, int menge)
        {
            if (this.router != null)
            {
                return this.router.ComputeCost(
                    lieferant.Position,
                                          anfrage.Auftrag.ZielPosition,
                                          anfrage.KostenProEinheitProTile,
                                          menge,
                                          anfrage.TileGroesse,
                                          anfrage.TruckFixkosten);
            }

            return DistanceCalculator.GetTransportCost(
                       lieferant.Position,
                       anfrage.Auftrag.ZielPosition,
                       anfrage.KostenProEinheitProTile * menge,
                       anfrage.TileGroesse) + anfrage.TruckFixkosten;
        }
    }
}

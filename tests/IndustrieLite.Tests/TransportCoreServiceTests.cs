// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using Godot;
using Xunit;
using IndustrieLite.Transport.Core;
using IndustrieLite.Transport.Core.Models;
using IndustrieLite.Transport.Core.Services;


public class TransportCoreServiceUnitTests
{
    [Theory(Skip="Benoetigt Godot StringName Runtime (Engine)")]
    [InlineData("Huehner", "chickens")]
    [InlineData("  Chicken  ", "chickens")]
    [InlineData("Getreide", "getreide")]
    public void MappeProduktZuResourceId_NormalisiertNamen(string produkt, string erwarteteId)
    {
        var dienst = new TransportCoreService();

        var resource = dienst.MappeProduktZuResourceId(produkt);

        Assert.Equal(erwarteteId, resource.ToString());
    }

    [Fact(Skip="Benoetigt Godot StringName Runtime (Engine)")]
    public void AktualisiereAuftragsbuch_UebernimmtDatenUndErzeugtDeliveryOrder()
    {
        var dienst = new TransportCoreService();
        var jetzt = new DateTime(2025, 9, 17, 12, 0, 0);
        var quelleKontext = new object();
        var zielKontext = new object();

        var daten = new[]
        {
            new TransportAuftragsDaten
            {
                AuftragId = 42,
                ResourceId = default!,
                Gesamtmenge = 50,
                Restmenge = 25,
                PreisProEinheit = 12.5,
                ErzeugtAm = jetzt,
                GueltigBis = jetzt.AddHours(12),
                IstAkzeptiert = true,
                ZielPosition = new Vector2(8, 4),
                ZielId = "city_a",
                ZielReferenz = zielKontext,
                QuelleReferenz = quelleKontext,
                ProduktName = "Huehner"
            }
        };

        dienst.AktualisiereAuftragsbuch(daten);

        Assert.True(dienst.Auftragsbuch.Contains(42));
        var orderInfo = dienst.Auftragsbuch.Orders[42];
        Assert.Equal(25, orderInfo.Remaining);
        Assert.True(orderInfo.Accepted);
        Assert.Equal(12.5, orderInfo.PricePerUnit);
        Assert.Same(zielKontext, orderInfo.ZielReferenz);
        Assert.Same(quelleKontext, orderInfo.QuelleReferenz);

        var delivery = dienst.HoleDeliveryOrder(42);
        Assert.NotNull(delivery);
        Assert.Equal(DeliveryOrderStatus.Offen, delivery!.Status);
        Assert.Equal(25, delivery.Remaining);
        Assert.Equal(12.5, delivery.PreisProEinheit);
        Assert.Empty(delivery.JobIds);
    }

    [Fact(Skip="Benoetigt Godot StringName Runtime (Engine)")]
    public void PlaneLieferung_OhneLieferanten_GibtFehlerZurueck()
    {
        var dienst = new TransportCoreService();
        var anfrage = new TransportPlanAnfrage
        {
            Auftrag = new TransportAuftragsDaten
            {
                AuftragId = 7,
                ResourceId = default!,
                Gesamtmenge = 30,
                Restmenge = 30,
                PreisProEinheit = 9.5,
                ZielPosition = new Vector2(4, 4),
                ProduktName = "Huehner"
            },
            Lieferanten = Array.Empty<LieferantDaten>(),
            MaxMengeProTruck = 10,
            KostenProEinheitProTile = 0.25,
            TruckFixkosten = 1.0,
            TileGroesse = 1
        };

        var ergebnis = dienst.PlaneLieferung(anfrage);

        Assert.False(ergebnis.Erfolgreich);
        Assert.Equal("Keine Lieferanten verfuegbar", ergebnis.Meldung);
        Assert.Equal(7, ergebnis.AuftragId);
    }

    [Fact(Skip="Benoetigt Godot StringName Runtime (Engine)")]
    public void PlaneLieferung_NichtGenugBestand_GibtFehlermeldung()
    {
        var dienst = new TransportCoreService();
        var lieferanten = new[]
        {
            new LieferantDaten
            {
                LieferantId = "farm_a",
                ResourceId = default!,
                VerfuegbareMenge = 5,
                Position = new Vector2(0, 0)
            }
        };
        var anfrage = new TransportPlanAnfrage
        {
            Auftrag = new TransportAuftragsDaten
            {
                AuftragId = 11,
                ResourceId = default!,
                Gesamtmenge = 20,
                Restmenge = 20,
                PreisProEinheit = 8.0,
                ZielPosition = new Vector2(10, 0),
                ProduktName = "Huehner"
            },
            Lieferanten = lieferanten,
            MaxMengeProTruck = 10,
            KostenProEinheitProTile = 0.3,
            TruckFixkosten = 2.0,
            TileGroesse = 1
        };

        var ergebnis = dienst.PlaneLieferung(anfrage);

        Assert.False(ergebnis.Erfolgreich);
        Assert.Contains("Nicht genug Bestand", ergebnis.Meldung);
    }

    [Fact(Skip="Benoetigt Godot StringName Runtime (Engine)")]
    public void PlaneLieferung_ErzeugtJobsUndReserviertBestand()
    {
        var dienst = new TransportCoreService();
        var planEventZaehler = 0;
        dienst.JobGeplant += _ => planEventZaehler++;

        var lieferanten = new[]
        {
            new LieferantDaten
            {
                LieferantId = "farm_a",
                ResourceId = default!,
                VerfuegbareMenge = 80,
                Position = new Vector2(0, 0),
                Kontext = new object()
            }
        };
        var anfrage = new TransportPlanAnfrage
        {
            Auftrag = new TransportAuftragsDaten
            {
                AuftragId = 15,
                ResourceId = default!,
                Gesamtmenge = 40,
                Restmenge = 40,
                PreisProEinheit = 10.0,
                ZielPosition = new Vector2(10, 0),
                ProduktName = "Huehner",
                ZielReferenz = new object()
            },
            Lieferanten = lieferanten,
            MaxMengeProTruck = 10,
            KostenProEinheitProTile = 0.5,
            TruckFixkosten = 2.0,
            TileGroesse = 1
        };

        var ergebnis = dienst.PlaneLieferung(anfrage);

        Assert.True(ergebnis.Erfolgreich);
        Assert.Equal(40, ergebnis.GeplanteMenge);
        Assert.Equal(4, ergebnis.Jobs.Count);
        Assert.Equal(4, planEventZaehler);

        var jobQueue = HolePrivatesFeld<Queue<TransportJob>>(dienst, "jobQueue");
        Assert.Equal(4, jobQueue.Count);

        var jobsById = HolePrivatesFeld<Dictionary<Guid, TransportJob>>(dienst, "jobsById");
        Assert.Equal(4, jobsById.Count);

        var erstesJob = ergebnis.Jobs[0];
        double erwarteteKosten = DistanceCalculator.GetTransportCost(
            new Vector2(0, 0),
            new Vector2(10, 0),
            anfrage.KostenProEinheitProTile * erstesJob.Menge,
            anfrage.TileGroesse) + anfrage.TruckFixkosten;
        Assert.Equal(erwarteteKosten, erstesJob.Transportkosten, 3);

        var orderInfo = dienst.Auftragsbuch.Orders[15];
        Assert.Equal(0, orderInfo.Remaining);

        var delivery = dienst.HoleDeliveryOrder(15);
        Assert.NotNull(delivery);
        Assert.Equal(DeliveryOrderStatus.InTransport, delivery!.Status);
        Assert.Equal(4, delivery.JobIds.Count);
    }

    [Fact]
    public void HoleNaechstenJob_GibtGeplantenJobZurueck()
    {
        var dienst = new TransportCoreService();
        var jobId = Guid.NewGuid();
        var job = ErzeugeJob(jobId, 7, TransportJobStatus.Geplant);

        var jobQueue = HolePrivatesFeld<Queue<TransportJob>>(dienst, "jobQueue");
        jobQueue.Enqueue(job);
        var jobsById = HolePrivatesFeld<Dictionary<Guid, TransportJob>>(dienst, "jobsById");
        jobsById[job.JobId] = job;

        var ergebnis = dienst.HoleNaechstenJob();

        Assert.NotNull(ergebnis);
        Assert.Equal(jobId, ergebnis!.JobId);
        Assert.Equal(TransportJobStatus.Zugewiesen, ergebnis.Status);
        Assert.Empty(jobQueue);
    }

    [Fact]
    public void RequeueJob_StelltJobWiederBereit()
    {
        var dienst = new TransportCoreService();
        var job = ErzeugeJob(Guid.NewGuid(), 21, TransportJobStatus.Geplant);

        var jobQueue = HolePrivatesFeld<Queue<TransportJob>>(dienst, "jobQueue");
        var jobsById = HolePrivatesFeld<Dictionary<Guid, TransportJob>>(dienst, "jobsById");
        jobQueue.Enqueue(job);
        jobsById[job.JobId] = job;

        var erster = dienst.HoleNaechstenJob();
        Assert.NotNull(erster);
        Assert.Equal(TransportJobStatus.Zugewiesen, erster!.Status);

        dienst.RequeueJob(erster.JobId);

        var zweiter = dienst.HoleNaechstenJob();
        Assert.NotNull(zweiter);
        Assert.Equal(erster.JobId, zweiter!.JobId);
        Assert.Equal(TransportJobStatus.Zugewiesen, zweiter.Status);
    }

    [Fact]
    public void MeldeJobGestartet_LoesstEventAus()
    {
        var dienst = new TransportCoreService();
        var job = ErzeugeJob(Guid.NewGuid(), 3, TransportJobStatus.Geplant);
        var jobsById = HolePrivatesFeld<Dictionary<Guid, TransportJob>>(dienst, "jobsById");
        jobsById[job.JobId] = job;

        TransportJob? gemeldeterJob = null;
        dienst.JobGestartet += j => gemeldeterJob = j;

        dienst.MeldeJobGestartet(job.JobId, new object());

        Assert.NotNull(gemeldeterJob);
        Assert.Equal(TransportJobStatus.Unterwegs, job.Status);
        Assert.Same(job, gemeldeterJob);
    }

    [Fact]
    public void MeldeJobAbgeschlossen_AktualisiertDeliveryOrder()
    {
        var dienst = new TransportCoreService();
        var job = ErzeugeJob(Guid.NewGuid(), 99, TransportJobStatus.Zugewiesen);
        var jobsById = HolePrivatesFeld<Dictionary<Guid, TransportJob>>(dienst, "jobsById");
        jobsById[job.JobId] = job;

        var deliveryOrders = HolePrivatesFeld<Dictionary<int, DeliveryOrder>>(dienst, "deliveryOrders");
        var delivery = new DeliveryOrder(job.OrderId, default!, "Huehner", 20, 10, null, null);
        delivery.JobIds.Add(job.JobId);
        delivery.Remaining = 20;
        deliveryOrders[job.OrderId] = delivery;

        dienst.MeldeJobAbgeschlossen(job.JobId, 20);

        Assert.False(jobsById.ContainsKey(job.JobId));
        Assert.Equal(DeliveryOrderStatus.Abgeschlossen, delivery.Status);
        Assert.Equal(0, delivery.Remaining);
        Assert.Empty(delivery.JobIds);
    }

    [Fact]
    public void CancelJobsForNode_BereinigtQueueUndLaufendeJobs()
    {
        var dienst = new TransportCoreService();
        var node = (Node)FormatterServices.GetUninitializedObject(typeof(Node));

        var wartenderJob = ErzeugeJob(Guid.NewGuid(), 1, TransportJobStatus.Geplant);
        SetEigenschaft(wartenderJob, nameof(TransportJob.LieferantKontext), node);
        var laufenderJob = ErzeugeJob(Guid.NewGuid(), 2, TransportJobStatus.Zugewiesen);
        SetEigenschaft(laufenderJob, nameof(TransportJob.LieferantKontext), node);

        var jobQueue = HolePrivatesFeld<Queue<TransportJob>>(dienst, "jobQueue");
        jobQueue.Enqueue(wartenderJob);
        var jobsById = HolePrivatesFeld<Dictionary<Guid, TransportJob>>(dienst, "jobsById");
        jobsById[laufenderJob.JobId] = laufenderJob;

        var deliveryOrders = HolePrivatesFeld<Dictionary<int, DeliveryOrder>>(dienst, "deliveryOrders");
        var delivery = new DeliveryOrder(laufenderJob.OrderId, default!, "Huehner", 5, 10, null, null);
        delivery.JobIds.Add(laufenderJob.JobId);
        deliveryOrders[laufenderJob.OrderId] = delivery;

        dienst.CancelJobsForNode(node);

        Assert.Empty(jobQueue);
        Assert.False(jobsById.ContainsKey(laufenderJob.JobId));
        Assert.Empty(delivery.JobIds);
    }

    [Fact]
    public void HoleNaechstenJob_MitGrosserFlotte_BleibtPerformant()
    {
        var dienst = new TransportCoreService();
        var jobQueue = HolePrivatesFeld<Queue<TransportJob>>(dienst, "jobQueue");
        var jobsById = HolePrivatesFeld<Dictionary<Guid, TransportJob>>(dienst, "jobsById");

        for (int i = 0; i < 2000; i++)
        {
            var status = i == 0 ? TransportJobStatus.Geplant : TransportJobStatus.Zugewiesen;
            var job = ErzeugeJob(Guid.NewGuid(), i, status);
            jobQueue.Enqueue(job);
            jobsById[job.JobId] = job;
        }

        var stoppuhr = Stopwatch.StartNew();
        var naechster = dienst.HoleNaechstenJob();
        stoppuhr.Stop();

        Assert.NotNull(naechster);
        Assert.True(stoppuhr.ElapsedMilliseconds < 250, $"HoleNaechstenJob dauerte zu lange: {stoppuhr.ElapsedMilliseconds}ms");
        Assert.Equal(TransportJobStatus.Zugewiesen, naechster!.Status);
    }

    private static TransportJob ErzeugeJob(Guid jobId, int orderId, TransportJobStatus status)
    {
        var job = (TransportJob)FormatterServices.GetUninitializedObject(typeof(TransportJob));
        SetEigenschaft(job, nameof(TransportJob.JobId), jobId);
        SetEigenschaft(job, nameof(TransportJob.OrderId), orderId);
        SetEigenschaft(job, nameof(TransportJob.Status), status);
        SetEigenschaft(job, nameof(TransportJob.Menge), 10);
        return job;
    }

    private static TField HolePrivatesFeld<TField>(object ziel, string feldName)
    {
        var feld = ziel.GetType().GetField(feldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (feld == null)
            throw new InvalidOperationException($"Feld {feldName} nicht gefunden.");
        return (TField)feld.GetValue(ziel)!;
    }

    private static void SetEigenschaft<TObj, TValue>(TObj ziel, string eigenschaftsName, TValue wert)
    {
        var prop = ziel!.GetType().GetProperty(eigenschaftsName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop == null)
            throw new InvalidOperationException($"Eigenschaft {eigenschaftsName} nicht gefunden.");
        prop.SetValue(ziel, wert);
    }

    [Fact]
    public void Scheduler_SplitsDeliveryByMaxAndReservesStock()
    {
        // Migrated from legacy TransportSystemTests to test the core Scheduler functionality
        var scheduler = new Scheduler();
        var supplyIndex = new SupplyIndex();

        var lieferantDaten = new[]
        {
            new SupplyIndex.SupplierData
            {
                LieferantId = "farm1",
                ResourceId = new StringName("chickens"),
                Bestand = 25,
                Position = new Vector2(0, 0)
            },
            new SupplyIndex.SupplierData
            {
                LieferantId = "farm2",
                ResourceId = new StringName("chickens"),
                Bestand = 15,
                Position = new Vector2(1, 0)
            }
        };

        supplyIndex.RebuildFromSupplierData(lieferantDaten);
        var suppliers = supplyIndex.GetSuppliers(new StringName("chickens"));

        var plan = scheduler.Plan(new StringName("chickens"), suppliers, 30, maxProTruck: 10);

        // Total amount should be 30, split into max 10 per truck
        int totalPlanned = plan.Sum(p => p.Menge);
        Assert.Equal(30, totalPlanned);

        // Stock should be reserved: farm1 (25 -> 0), farm2 (15 -> 10)
        // Concrete splitting: farm1 delivers 10+10+5, farm2 delivers 5 (at max 10/truck)
        var farm1 = suppliers.First(s => s.LieferantId == "farm1");
        var farm2 = suppliers.First(s => s.LieferantId == "farm2");

        Assert.Equal(0, farm1.Free); // 25 available - 25 reserved = 0 free
        Assert.Equal(10, farm2.Free); // 15 available - 5 reserved = 10 free
        Assert.Equal(25, farm1.Reserved);
        Assert.Equal(5, farm2.Reserved);
    }
}



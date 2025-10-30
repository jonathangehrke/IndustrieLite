// SPDX-License-Identifier: MIT
using System;
using System.Linq;
using Godot;
using IndustrieLite.Transport.Core;
using IndustrieLite.Transport.Core.Models;
using IndustrieLite.Transport.Interfaces;

public partial class TransportEconomyService : Node, ITransportEconomyService
{
    public double CostPerUnitPerTile { get; private set; }

    public double TruckFixedCost { get; private set; }

    public double DefaultPricePerUnit { get; private set; }

    private EconomyManager economyManager = default!;
    private EventHub? eventHub;
    private BuildingManager buildingManager = default!;
    private MarketService? marketService;

    // Delegate fÃ¼r SignaleAktiv und jobsNeuZuPlanen (wird vom Coordinator gesetzt)
    public System.Func<bool>? GetSignaleAktivDelegate { get; set; }

    public System.Action? SetJobsNeuZuPlanenDelegate { get; set; }

    // Delegate fÃ¼r TransportCore-Zugriff
    public System.Func<TransportCoreService?>? GetTransportCoreDelegate { get; set; }

    /// <summary>
    /// Initialisiert den Service mit AbhÃ¤ngigkeiten und Kostenparametern.
    /// </summary>
    public void Initialize(EconomyManager economyManager, EventHub? eventHub, BuildingManager buildingManager,
                          double costPerUnitPerTile, double truckFixedCost, double defaultPricePerUnit, MarketService? marketService = null)
    {
        this.economyManager = economyManager;
        this.eventHub = eventHub;
        this.buildingManager = buildingManager;
        this.CostPerUnitPerTile = costPerUnitPerTile;
        this.TruckFixedCost = truckFixedCost;
        this.DefaultPricePerUnit = defaultPricePerUnit;
        this.marketService = marketService;
    }

    /// <summary>
    /// Liefert den aktuellen Marktpreis fÃ¼r ein Produkt in einer Stadt (Fallback: DefaultPricePerUnit).
    /// </summary>
    /// <returns></returns>
    public double GetCurrentMarketPrice(string product, City city)
    {
        try
        {
            var open = city.Orders.Where(o => !o.Accepted && !o.Delivered && string.Equals(o.Product, product, StringComparison.OrdinalIgnoreCase));
            if (open.Any())
            {
                return open.Average(o => o.PricePerUnit);
            }
        }
        catch
        {
        }
        return this.DefaultPricePerUnit;
    }

    /// <summary>
    /// Verarbeitet die Ankunft eines Trucks (ErlÃ¶s, Kosten, Events, Jobabschluss).
    /// </summary>
    public void ProcessTruckArrival(Truck t)
    {
        var revenue = t.Amount * t.PricePerUnit;
        var cost = t.TransportCost;
        var net = revenue - cost;
        this.economyManager.AddMoney(net);

        var transportCore = this.GetTransportCoreDelegate?.Invoke();
        if (transportCore != null && t.JobId != Guid.Empty)
        {
            transportCore.MeldeJobAbgeschlossen(t.JobId, t.Amount);
        }

        try
        {
            int oid = t.OrderId;
            if (oid != 0)
            {
                foreach (var city in this.buildingManager.Cities)
                {
                    var ord = city.Orders.FirstOrDefault(o => o.Id == oid && !o.Delivered);
                    if (ord != null)
                    {
                        ord.Remaining = Math.Max(0, ord.Remaining - t.Amount);
                        if (ord.Remaining <= 0)
                        {
                            ord.Delivered = true;

                            // Complete market order delivery (resource deduction + money payment)
                            DebugLogger.LogTransport($"TransportEconomyService: Order {ord.Id} delivered, looking for MarketService...");
                                						// Lazy DI fallback: resolve MarketService once via ServiceContainer if not injected
                                						if (this.marketService == null)
                                						{
                                							try
                                							{
                                								var sc = ServiceContainer.Instance;
                                								if (sc != null)
                                								{
                                									this.marketService = sc.GetNamedService<MarketService>(ServiceNames.MarketService);
                                									if (this.marketService != null)
                                									{
                                										DebugLogger.LogTransport("TransportEconomyService: MarketService resolved via ServiceContainer");
                                									}
                                								}
                                							}
                                							catch
                                							{
                                							}
                                						}
                            if (this.marketService != null)
                            {
                                DebugLogger.LogTransport($"TransportEconomyService: MarketService found, calling CompleteMarketOrderDelivery for {ord.Product} x{ord.Amount}");
                                this.marketService.CompleteMarketOrderDelivery(ord);
                            }
                            else
                            {
                                DebugLogger.LogTransport("TransportEconomyService: WARNING - MarketService NOT FOUND in ServiceContainer!");
                            }

                            var signaleAktiv = this.GetSignaleAktivDelegate?.Invoke() ?? true;
                            if (signaleAktiv && this.eventHub != null)
                            {
                                this.eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
                            }
                        }
                        break;
                    }
                }
            }
        }
        catch
        {
        }

        var signaleAktivForCost = this.GetSignaleAktivDelegate?.Invoke() ?? true;
        if (signaleAktivForCost && this.eventHub != null && cost > 0)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.ProductionCostIncurred, (t.SourceNode as Node) ?? (Node)this, "transport", cost, "maintenance");
        }

        // Signal that jobs need to be replanned
        this.SetJobsNeuZuPlanenDelegate?.Invoke();
    }

    /// <summary>
    /// Berechnet Transportkosten anhand Luftliniendistanz.
    /// </summary>
    /// <returns></returns>
    public double CalculateTransportCost(Vector2 start, Vector2 target, int amount)
    {
        try
        {
            double cost = DistanceCalculator.GetTransportCost(start, target, this.CostPerUnitPerTile * amount, this.buildingManager.TileSize) + this.TruckFixedCost;
            return cost;
        }
        catch
        {
            return this.TruckFixedCost;
        }
    }

    /// <summary>
    /// Berechnet Transportkosten anhand eines Pfades.
    /// </summary>
    /// <returns></returns>
    public double CalculateTransportCostWithPath(System.Collections.Generic.List<Vector2> path, int amount)
    {
        try
        {
            var worldLen = DistanceCalculator.GetPathWorldLength(path);
            var tiles = worldLen / this.buildingManager.TileSize;
            return (tiles * this.CostPerUnitPerTile * amount) + this.TruckFixedCost;
        }
        catch
        {
            return this.TruckFixedCost;
        }
    }
}



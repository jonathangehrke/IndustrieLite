// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService.Transport: Orders/AcceptOrder/Profit-Schaetzung.
/// </summary>
public partial class UIService
{
    // === Order/Transport Schaetzungen (UI) ===

    /// <summary>
    /// Schaetzt den Profit eines Auftrags (Erloes minus Transportkosten).
    /// Erwartet ein Dictionary wie aus TransportManager.GetOrders() (city, product, amount, ppu).
    /// </summary>
    /// <returns></returns>
    public double EstimateOrderProfit(Godot.Collections.Dictionary order)
    {
        if (order == null)
        {
            return 0.0;
        }

        try
        {
            // Felder extrahieren (robust gegen fehlende Keys)
            string city = order.ContainsKey("city") ? (string)order["city"] : string.Empty;
            string product = order.ContainsKey("product") ? (string)order["product"] : string.Empty;
            int amount = order.ContainsKey("amount") ? (int)order["amount"] : 0;
            double ppu = order.ContainsKey("ppu") ? (double)order["ppu"] : 0.0;

            double revenue = amount * ppu;

            if (!this.servicesInitialized)
            {
                this.InitializeServices();
            }

            if (this.buildingManager == null)
            {
                return revenue;
            }

            // City finden
            City? targetCity = null;
            foreach (var c in this.buildingManager.Cities)
            {
                if (string.Equals(c.CityName, city, System.StringComparison.OrdinalIgnoreCase))
                {
                    targetCity = c;
                    break;
                }
            }
            if (targetCity == null)
            {
                return revenue;
            }

            // Naechste Quelle fuer Produkt bestimmen (derzeit: Huehner -> ChickenFarm)
            // Wir verwenden die kuerzeste Distanz von irgendeiner Farm zur Stadt.
            var farms = this.buildingManager.GetChickenFarms();
            if (farms == null || farms.Count == 0)
            {
                return revenue;
            }

            var tileSize = this.buildingManager.TileSize;
            Vector2 CityCenter(City c)
                => c.GlobalPosition + new Vector2(c.Size.X * tileSize / 2f, c.Size.Y * tileSize / 2f);
            Vector2 FarmCenter(ChickenFarm f)
                => f.GlobalPosition + new Vector2(f.Size.X * tileSize / 2f, f.Size.Y * tileSize / 2f);

            var cityCenter = CityCenter(targetCity);
            double bestCost = double.MaxValue;
            double basePerTile = (this.transportManager != null ? this.transportManager.CostPerUnitPerTile : 0.05) * amount;

            // Bevorzugt: Kosten anhand des tatsaechlichen Strassenpfads (RoadManager) berechnen
            var roadManager = this.roadManager;
            foreach (var f in farms)
            {
                double cost;
                var start = FarmCenter(f);
                if (roadManager != null)
                {
                    var path = roadManager.GetPath(start, cityCenter);
                    if (path != null && path.Count > 1)
                    {
                        var worldLen = DistanceCalculator.GetPathWorldLength(path);
                        var tiles = worldLen / tileSize;
                        cost = tiles * basePerTile;
                    }
                    else
                    {
                        cost = DistanceCalculator.GetTransportCost(start, cityCenter, basePerTile, tileSize);
                    }
                }
                else
                {
                    // Fallback: Distanz-Schaetzung ohne RoadManager
                    cost = DistanceCalculator.GetTransportCost(start, cityCenter, basePerTile, tileSize);
                }
                if (cost < bestCost)
                {
                    bestCost = cost;
                }
            }

            if (bestCost == double.MaxValue)
            {
                bestCost = 0.0;
            }

            return revenue - bestCost;
        }
        catch
        {
            return 0.0;
        }
    }

    /// <summary>
    /// Get available transport orders.
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<Godot.Collections.Dictionary> GetTransportOrders()
    {
        if (this.transportManager == null)
        {
            this.InitializeServices();
        }

        if (this.transportManager == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: TransportManager not available for GetTransportOrders()");
            return new Godot.Collections.Array<Godot.Collections.Dictionary>();
        }

        var orders = this.transportManager.GetOrders();
        DebugLogger.LogServices(() => $"UIService: GetTransportOrders() returned {orders.Count} orders", this.DebugLogs);
        return orders;
    }

    /// <summary>
    /// Accept a transport/market order by ID.
    /// </summary>
    public void AcceptTransportOrder(int orderId)
    {
        // Try MarketService first (for market orders)
        if (this.marketService != null)
        {
            // Find the order in transport orders (which are actually market orders from cities)
            var orders = this.GetTransportOrders();
            foreach (var orderDict in orders)
            {
                if (orderDict.TryGetValue("id", out var idVariant) && idVariant.AsInt32() == orderId)
                {
                    // Convert to MarketOrder and process with MarketService
                    var product = orderDict.TryGetValue("product", out var prod) ? prod.AsString() : "";
                    var amount = orderDict.TryGetValue("amount", out var amt) ? amt.AsInt32() : 0;
                    var pricePerUnit = orderDict.TryGetValue("ppu", out var price) ? price.AsDouble() : 0.0;

                    var marketOrder = new MarketOrder(product, amount, pricePerUnit);

                    DebugLogger.LogServices(() => $"UIService: Accepting market order {orderId} ({product} x{amount})", this.DebugLogs);

                    if (this.marketService.AcceptMarketOrder(marketOrder))
                    {
                        // Mark order as accepted in TransportManager
                        if (this.transportManager != null)
                        {
                            this.transportManager.AcceptOrder(orderId);
                        }

                        DebugLogger.LogServices(() => $"UIService: Market order {orderId} accepted successfully", this.DebugLogs);
                    }
                    else
                    {
                        DebugLogger.LogServices(() => $"UIService: Market order {orderId} acceptance failed", this.DebugLogs);
                    }

                    // Emit event for UI updates
                    if (this.eventHub != null)
                    {
                        this.eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
                    }
                    return;
                }
            }
        }

        // Fallback: try TransportManager (for actual transport orders)
        if (this.transportManager == null)
        {
            this.InitializeServices();
        }

        if (this.transportManager == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "UIService: Neither MarketService nor TransportManager available for AcceptTransportOrder()");
            return;
        }

        DebugLogger.LogServices(() => $"UIService: Accepting transport order {orderId}", this.DebugLogs);
        var res = this.transportManager.TryAcceptOrder(orderId);
        if (!res.Ok)
        {
            var code = res.ErrorInfo?.Code ?? ErrorIds.TransportPlanningFailedName;
            var msg = res.ErrorInfo?.Message ?? res.Error;
            DebugLogger.Warn("debug_services", "UIAcceptOrderFailed", msg,
                new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "orderId", orderId }, { "code", code } });
            if (this.eventHub == null)
            {
                this.InitializeServices();
            }

            try
            {
                this.eventHub?.EmitSignal(EventHub.SignalName.ToastRequested, $"Auftrag {orderId} fehlgeschlagen: {msg}", "warn");
            }
            catch
            {
            }
        }

        // Emit event for UI updates
        if (this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
        }
    }
}


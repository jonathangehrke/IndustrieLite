// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Service für Markt-Operationen und Profit-Berechnungen
/// Migriert aus MarketPanel.gd - enthält Geschäftslogik für Marktbestellungen
/// </summary>
public partial class MarketService : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    private ResourceManager? resourceManager;
    private TransportManager? transportManager;
    private EconomyManager? economyManager;
    private BuildingManager? buildingManager;
    private LevelManager? levelManager;
    private Database? database;

    // Product name normalization mappings
    private readonly Dictionary<string, string> productNormalization = new()
    {
        { "huehner", ResourceIds.Chickens },
        { "huhn", ResourceIds.Chickens },
        { "hühner", ResourceIds.Chickens },
        { "schwein", ResourceIds.Pig },
        { "pig", ResourceIds.Pig },
        { "ei", ResourceIds.Egg },
        { "egg", ResourceIds.Egg },
        { "getreide", ResourceIds.Grain },
        { "grain", ResourceIds.Grain },
        { "korn", ResourceIds.Grain }
    };

    public override void _Ready()
    {
        // No self-registration - managed by DIContainer (Clean Architecture)
        // Dependencies are injected via Initialize() method
    }

    /// <summary>
    /// Normalizes product names to standard identifiers
    /// </summary>
    public string NormalizeProductName(string productName)
    {
        if (string.IsNullOrEmpty(productName))
            return productName;

        var normalized = productName.Trim().ToLowerInvariant();

        // Zuerst exakte Zuordnung versuchen
        if (productNormalization.TryGetValue(normalized, out var mapped))
            return mapped;

        // Einfache Plural-/Singular-Varianten abfangen (z. B. pigs -> pig, eggs -> egg, grains -> grain)
        if (normalized.EndsWith("s") && normalized.Length > 1)
        {
            var singular = normalized.Substring(0, normalized.Length - 1);
            if (productNormalization.TryGetValue(singular, out mapped))
                return mapped;
        }
        if (normalized.EndsWith("en") && normalized.Length > 2)
        {
            var singularDe = normalized.Substring(0, normalized.Length - 2);
            if (productNormalization.TryGetValue(singularDe, out mapped))
                return mapped;
        }

        return normalized;
    }

    /// <summary>
    /// Checks whether a product is unlocked for the current player level.
    /// Uses GameDatabase resource definition (RequiredLevel) and LevelManager.CurrentLevel.
    /// </summary>
    public bool IsProductUnlocked(string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(productId))
                return true; // Fallback: nichts ausblenden

            var id = NormalizeProductName(productId);

            var currentLevel = levelManager?.CurrentLevel ?? 1;

            if (database?.ResourcesById != null && database.ResourcesById.TryGetValue(id, out var resDef) && resDef != null)
            {
                var unlocked = resDef.RequiredLevel <= currentLevel;
                DebugLogger.LogServices($"MarketService: IsProductUnlocked({id}) -> {unlocked} (req={resDef.RequiredLevel}, cur={currentLevel})");
                return unlocked;
            }

            // Fallback: Wenn keine Database vorhanden, Standardannahme: sichtbar
            DebugLogger.LogServices($"MarketService: Database missing or resource not found for '{id}', defaulting to unlocked");
            return true;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error in IsProductUnlocked('{productId}'): {ex.Message}");
            return true; // fail-open, UI nicht blockieren
        }
    }

    /// <summary>
    /// Estimates the profit for a market order
    /// </summary>
    public float EstimateOrderProfit(MarketOrder order)
    {
        if (order == null)
            return float.NaN;

        try
        {
            var normalizedProduct = NormalizeProductName(order.Product);
            var totalRevenue = order.Amount * (float)order.PricePerUnit;

            // Calculate base production cost (simplified)
            var productionCost = CalculateProductionCost(normalizedProduct, order.Amount);

            // Calculate transport cost
            var transportCost = CalculateTransportCost(order);

            var totalCost = productionCost + transportCost;
            var profit = totalRevenue - totalCost;

            DebugLogger.LogServices($"MarketService: Profit calculation for {order.Product} x{order.Amount}: Revenue={totalRevenue:F2}, Cost={totalCost:F2}, Profit={profit:F2}");

            return profit;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error calculating profit for order: {ex.Message}");
            return float.NaN;
        }
    }

    /// <summary>
    /// Checks resource availability for an order
    /// </summary>
    public ResourceAvailability CheckResourceAvailability(MarketOrder order)
    {
        if (order == null)
            return new ResourceAvailability { IsAvailable = false, AvailableAmount = 0 };

        var normalizedProduct = NormalizeProductName(order.Product);
        var availableAmount = GetTotalResourceAmount(normalizedProduct);
        var isAvailable = availableAmount >= order.Amount;


        return new ResourceAvailability
        {
            IsAvailable = isAvailable,
            AvailableAmount = availableAmount,
            RequiredAmount = order.Amount,
            ResourceId = normalizedProduct
        };
    }

    /// <summary>
    /// Gets total amount of a resource across all buildings
    /// </summary>
    public int GetTotalResourceAmount(string resourceId)
    {
        try
        {
            // Produkt-Ressourcen (verkaufbare Waren) werden ausschliesslich aus Gebaeude-Inventaren ermittelt,
            // damit zentrale Zaehler im ResourceManager (z. B. Startwerte) die UI-Verfuegbarkeit nicht verfaelschen.
            var rid = resourceId.ToLowerInvariant();
            bool istInventarWare = rid == ResourceIds.Chickens || rid == ResourceIds.Pig || rid == ResourceIds.Egg || rid == ResourceIds.Grain;

            // Use injected buildingManager instead of ServiceContainer lookup
            if (istInventarWare && buildingManager != null)
            {
                var total = buildingManager.GetTotalInventoryOfResource(new StringName(resourceId));
                DebugLogger.LogServices($"MarketService: Total (inventory-only) for {resourceId} = {total}");
                return total;
            }

            if (resourceManager == null)
                return 0;

            // Sonstige Ressourcen (power, water, workers, etc.): zentraler ResourceManager + Inventare
            var sum = resourceManager.GetTotalOfResource(new StringName(resourceId));
            DebugLogger.LogServices($"MarketService: Total (resourceManager) for {resourceId} = {sum}");
            return sum;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error getting total for resource {resourceId}: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Calculates production cost for a resource
    /// </summary>
    private float CalculateProductionCost(string resourceId, int amount)
    {
        // Base production costs per unit (simplified model)
        var baseCosts = new Dictionary<string, float>
        {
            { ResourceIds.Chickens, 2.0f },
            { ResourceIds.Pig, 3.5f },
            { ResourceIds.Egg, 0.5f },
            { ResourceIds.Grain, 0.8f }
        };

        if (baseCosts.TryGetValue(resourceId, out var baseCost))
            return baseCost * amount;

        return 1.0f * amount; // Default cost
    }

    /// <summary>
    /// Calculates transport cost for an order
    /// </summary>
    private float CalculateTransportCost(MarketOrder order)
    {
        if (transportManager == null)
            return 0f;

        try
        {
            // Simplified transport cost calculation
            // In der bestehenden MarketOrder gibt es keine CityName Property
            // Verwende einfache Kostenschätzung basierend auf Menge
            return order.Amount * 0.2f; // 0.2 Geld pro Einheit Transport
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error calculating transport cost: {ex.Message}");
        }

        return order.Amount * 0.2f; // Default transport cost
    }

    /// <summary>
    /// Finds a city by name
    /// </summary>
    private City? FindCityByName(string cityName)
    {
        // Use injected buildingManager instead of ServiceContainer lookup
        if (buildingManager == null)
            return null;

        return buildingManager.Cities.FirstOrDefault(c =>
            string.Equals(c.CityName, cityName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Processes market orders and validates them
    /// </summary>
    public List<ValidatedMarketOrder> ValidateMarketOrders(List<MarketOrder> orders)
    {
        var validatedOrders = new List<ValidatedMarketOrder>();

        foreach (var order in orders)
        {
            var availability = CheckResourceAvailability(order);
            var profit = EstimateOrderProfit(order);

            validatedOrders.Add(new ValidatedMarketOrder
            {
                Order = order,
                Availability = availability,
                EstimatedProfit = profit,
                IsValid = availability.IsAvailable && !float.IsNaN(profit)
            });
        }

        return validatedOrders;
    }

    /// <summary>
    /// Checks if a market order can be accepted (availability check only)
    /// Transport system will handle actual resource transfer and payment
    /// </summary>
    public bool AcceptMarketOrder(MarketOrder order)
    {
        if (order == null)
            return false;

        try
        {
            var availability = CheckResourceAvailability(order);
            if (!availability.IsAvailable)
            {
                DebugLogger.LogServices($"MarketService: Cannot accept order - insufficient resources. Required: {order.Amount}, Available: {availability.AvailableAmount}");
                return false;
            }

            // Only check availability - transport system handles the rest
            DebugLogger.LogServices($"MarketService: Order can be accepted - {order.Product} x{order.Amount}. Transport will handle delivery.");
            return true;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error checking order acceptance: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Completes a market order delivery (called by transport system)
    /// Deducts resources and adds money when transport is complete
    /// </summary>
    public bool CompleteMarketOrderDelivery(MarketOrder order)
    {
        if (order == null)
            return false;

        try
        {
            DebugLogger.LogEconomy($"MarketService.CompleteMarketOrderDelivery: START - Product={order.Product}, Amount={order.Amount}");

            // NOTE: Resource deduction happens when loading/starting the transport (TransportOrderManager).
            // Do NOT deduct again here to avoid double consumption.
            var normalizedProduct = NormalizeProductName(order.Product);
            DebugLogger.LogServices($"MarketService: Skipping resource deduction for {normalizedProduct} (handled by transport)");

            // NOTE: Money (net) is added in TransportEconomyService.ProcessTruckArrival.
            // Here we only track GROSS revenue for the level system.
            var revenue = order.Amount * order.PricePerUnit;

            // Track market revenue for level progression
            DebugLogger.LogEconomy($"MarketService.CompleteMarketOrderDelivery: Checking LevelManager - levelManager is {(levelManager == null ? "NULL" : "NOT NULL")}");
            if (levelManager != null)
            {
                DebugLogger.LogServices($"MarketService: Tracking revenue {revenue:F2} for level progression");
                DebugLogger.LogEconomy($"MarketService.CompleteMarketOrderDelivery: Calling levelManager.AddMarketRevenue({revenue:F2})");
                levelManager.AddMarketRevenue(revenue);
            }
            else
            {
                DebugLogger.LogServices("MarketService: WARNING - LevelManager is null, cannot track revenue");
                DebugLogger.LogEconomy("MarketService.CompleteMarketOrderDelivery: ERROR - LevelManager is NULL!");
            }

            DebugLogger.LogServices($"MarketService: Order delivery processed for level tracking - {order.Product} x{order.Amount} (Revenue={revenue:F2})");
            DebugLogger.LogEconomy($"MarketService.CompleteMarketOrderDelivery: COMPLETED (level tracking only)");
            return true;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error completing order delivery: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deducts resources from ResourceManager or building inventories
    /// </summary>
    private bool DeductResources(string resourceId, int amount)
    {
        if (resourceManager == null)
        {
            DebugLogger.LogEconomy("MarketService.DeductResources: ResourceManager is NULL");
            return false;
        }

        try
        {
            var resourceName = new StringName(resourceId);
            DebugLogger.LogEconomy($"MarketService.DeductResources: Trying to deduct {amount} x {resourceId}");

            // Inventory products (grain, chickens, eggs, pigs) are ONLY in building inventories
            var rid = resourceId.ToLowerInvariant();
            bool isInventoryProduct = rid == ResourceIds.Chickens || rid == ResourceIds.Pig || rid == ResourceIds.Egg || rid == ResourceIds.Grain;

            if (isInventoryProduct)
            {
                DebugLogger.LogEconomy($"MarketService.DeductResources: {resourceId} is inventory product, checking building inventories only");
                if (buildingManager != null)
                {
                    var result = DeductFromBuildingInventories(buildingManager, resourceName, amount);
                    DebugLogger.LogEconomy($"MarketService.DeductResources: DeductFromBuildingInventories result={result}");
                    return result;
                }
                else
                {
                    DebugLogger.LogEconomy("MarketService.DeductResources: BuildingManager is NULL");
                    return false;
                }
            }

            // For other resources (power, water, workers), try ResourceManager first
            var availableInRM = resourceManager.GetAvailable(resourceName);
            DebugLogger.LogEconomy($"MarketService.DeductResources: ResourceManager has {availableInRM} available");

            if (availableInRM >= amount)
            {
                var result = resourceManager.ConsumeResource(resourceName, amount);
                DebugLogger.LogEconomy($"MarketService.DeductResources: Consumed from ResourceManager - Success={result}");
                return result;
            }

            return false;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error deducting resources: {ex.Message}");
            DebugLogger.LogEconomy($"MarketService.DeductResources: EXCEPTION - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deducts resources from building inventories
    /// </summary>
    private bool DeductFromBuildingInventories(BuildingManager buildingManager, StringName resourceId, int amount)
    {
        int remaining = amount;
        DebugLogger.LogEconomy($"MarketService.DeductFromBuildingInventories: Need to deduct {amount} x {resourceId}");
        DebugLogger.LogEconomy($"MarketService.DeductFromBuildingInventories: Found {buildingManager.Buildings.Count} buildings");

        int totalAvailable = 0;
        foreach (var building in buildingManager.Buildings)
        {
            if (building is IHasInventory inventoryBuilding && remaining > 0)
            {
                var inventory = inventoryBuilding.GetInventory();
                DebugLogger.LogEconomy($"  Building {building.GetType().Name}: has {inventory.Count} inventory items");

                if (inventory.TryGetValue(resourceId, out var available))
                {
                    totalAvailable += (int)available;
                    DebugLogger.LogEconomy($"    - Has {available} x {resourceId}");
                    int canTake = Math.Min(remaining, (int)available);
                    if (canTake > 0 && inventoryBuilding.ConsumeFromInventory(resourceId, canTake))
                    {
                        remaining -= canTake;
                        DebugLogger.LogEconomy($"    - Consumed {canTake}, remaining needed: {remaining}");
                        DebugLogger.LogServices($"MarketService: Deducted {canTake} {resourceId} from {building.GetType().Name}");
                    }
                    else
                    {
                        DebugLogger.LogEconomy($"    - ConsumeFromInventory FAILED");
                    }
                }
            }
        }

        DebugLogger.LogEconomy($"MarketService.DeductFromBuildingInventories: Total available was {totalAvailable}, remaining needed: {remaining}");
        return remaining == 0;
    }

    /// <summary>
    /// Gets market statistics for UI display
    /// </summary>
    public MarketStatistics GetMarketStatistics()
    {
        var stats = new MarketStatistics();

        try
        {
            // Get resource totals
            stats.TotalChickens = GetTotalResourceAmount(ResourceIds.Chickens);
            stats.TotalPigs = GetTotalResourceAmount(ResourceIds.Pig);
            stats.TotalEggs = GetTotalResourceAmount(ResourceIds.Egg);
            stats.TotalGrain = GetTotalResourceAmount(ResourceIds.Grain);

            // Calculate total value
            stats.TotalValue =
                stats.TotalChickens * 2.0f +
                stats.TotalPigs * 3.5f +
                stats.TotalEggs * 0.5f +
                stats.TotalGrain * 1.0f;

            stats.LastUpdated = DateTime.Now;
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error getting market statistics: {ex.Message}");
        }

        return stats;
    }

    // === GDScript-kompatible Wrapper-Methoden ===

    /// <summary>
    /// GDScript-kompatible Version von ValidateMarketOrders
    /// </summary>
    public Godot.Collections.Array ValidateMarketOrdersForUI(Godot.Collections.Array orders)
    {
        var result = new Godot.Collections.Array();

        try
        {
            foreach (var orderVariant in orders)
            {
                if (orderVariant.AsGodotDictionary() is var orderDict)
                {
                    var product = orderDict.TryGetValue("product", out var prod) ? prod.AsString() : "";
                    var amount = orderDict.TryGetValue("amount", out var amt) ? amt.AsInt32() : 0;
                    var pricePerUnit = orderDict.TryGetValue("ppu", out var price) ? price.AsDouble() : 0.0;

                    var order = new MarketOrder(product, amount, pricePerUnit);

                    var availability = CheckResourceAvailability(order);
                    var profit = EstimateOrderProfit(order);

                    var validatedOrder = new Godot.Collections.Dictionary
                    {
                        ["Order"] = orderDict,
                        ["Availability"] = new Godot.Collections.Dictionary
                        {
                            ["IsAvailable"] = availability.IsAvailable,
                            ["AvailableAmount"] = availability.AvailableAmount,
                            ["RequiredAmount"] = availability.RequiredAmount,
                            ["ResourceId"] = availability.ResourceId
                        },
                        ["EstimatedProfit"] = profit,
                        ["IsValid"] = availability.IsAvailable && !float.IsNaN(profit)
                    };

                    result.Add(validatedOrder);
                }
            }
        }
        catch (System.Exception ex)
        {
            DebugLogger.LogServices($"MarketService: Error in ValidateMarketOrdersForUI: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// GDScript-kompatible Version für Resource-Verfügbarkeit
    /// </summary>
    public Godot.Collections.Dictionary CheckResourceAvailabilityForUI(string product, int amount)
    {
        var order = new MarketOrder(product, amount, 1.0); // Dummy-Preis für Verfügbarkeits-Check

        var availability = CheckResourceAvailability(order);

        return new Godot.Collections.Dictionary
        {
            ["IsAvailable"] = availability.IsAvailable,
            ["AvailableAmount"] = availability.AvailableAmount,
            ["RequiredAmount"] = availability.RequiredAmount,
            ["ResourceId"] = availability.ResourceId
        };
    }

    /// <summary>
    /// GDScript-kompatible Version für Gesamt-Ressourcen-Menge
    /// </summary>
    public int GetTotalResourceAmountForUI(string resourceId)
    {
        return GetTotalResourceAmount(resourceId);
    }
}

// MarketOrder ist bereits in City.cs definiert

/// <summary>
/// Resource availability information
/// </summary>
public class ResourceAvailability
{
    public bool IsAvailable { get; set; }
    public int AvailableAmount { get; set; }
    public int RequiredAmount { get; set; }
    public string ResourceId { get; set; } = string.Empty;
}

/// <summary>
/// Validated market order with availability and profit information
/// </summary>
public class ValidatedMarketOrder
{
    public MarketOrder Order { get; set; } = default!;
    public ResourceAvailability Availability { get; set; } = default!;
    public float EstimatedProfit { get; set; }
    public bool IsValid { get; set; }
}

/// <summary>
/// Market statistics for UI display
/// </summary>
public class MarketStatistics
{
    public int TotalChickens { get; set; }
    public int TotalPigs { get; set; }
    public int TotalEggs { get; set; }
    public int TotalGrain { get; set; }
    public float TotalValue { get; set; }
    public DateTime LastUpdated { get; set; }
}

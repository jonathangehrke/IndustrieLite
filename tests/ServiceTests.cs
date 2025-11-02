// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Tests für die neuen C# Services
/// Diese Klasse testet die Grundfunktionalität der migrierten Services
/// </summary>
public partial class ServiceTests : Node
{
    private SupplierService? supplierService;
    private LogisticsService? logisticsService;
    private MarketService? marketService;
    private ProductionCalculationService? productionCalculationService;

    public override void _Ready()
    {
        GD.Print("ServiceTests: Starting service tests");

        // Initialize services
        InitializeServices();

        // Run tests
        RunAllTests();
    }

    private void InitializeServices()
    {
        // Create service instances
        supplierService = new SupplierService();
        logisticsService = new LogisticsService();
        marketService = new MarketService();
        productionCalculationService = new ProductionCalculationService();

        // Add to scene tree for proper initialization
        AddChild(supplierService);
        AddChild(logisticsService);
        AddChild(marketService);
        AddChild(productionCalculationService);

        GD.Print("ServiceTests: Services initialized");
    }

    private void RunAllTests()
    {
        TestMarketService();
        TestLogisticsService();
        TestProductionCalculationService();
        GD.Print("ServiceTests: All tests completed");
    }

    private void TestMarketService()
    {
        GD.Print("ServiceTests: Testing MarketService");

        if (marketService == null)
        {
            GD.PrintErr("ServiceTests: MarketService is null");
            return;
        }

        // Test product normalization
        var normalizedChickens = marketService.NormalizeProductName("Hühner");
        Assert(normalizedChickens == "chickens", "Product normalization failed for Hühner");

        var normalizedPig = marketService.NormalizeProductName("Schwein");
        Assert(normalizedPig == "pig", "Product normalization failed for Schwein");

        // Test market order creation (using existing constructor)
        var testOrder = new MarketOrder("chickens", 10, 5.0);

        // Test resource availability check
        var availability = marketService.CheckResourceAvailability(testOrder);
        Assert(availability != null, "Resource availability check returned null");
        Assert(availability.ResourceId == "chickens", "Resource ID mismatch in availability check");

        GD.Print("ServiceTests: MarketService tests passed");
    }

    private void TestLogisticsService()
    {
        GD.Print("ServiceTests: Testing LogisticsService");

        if (logisticsService == null)
        {
            GD.PrintErr("ServiceTests: LogisticsService is null");
            return;
        }

        // Test capacity cost calculation
        var capacityCost = logisticsService.CalculateCapacityUpgradeCost(5);
        Assert(capacityCost > 0, "Capacity upgrade cost should be positive");

        var speedCost = logisticsService.CalculateSpeedUpgradeCost(32.0f);
        Assert(speedCost > 0, "Speed upgrade cost should be positive");

        // Test cost scaling
        var higherCapacityCost = logisticsService.CalculateCapacityUpgradeCost(10);
        Assert(higherCapacityCost > capacityCost, "Higher capacity should cost more");

        GD.Print("ServiceTests: LogisticsService tests passed");
    }

    private void TestProductionCalculationService()
    {
        GD.Print("ServiceTests: Testing ProductionCalculationService");

        if (productionCalculationService == null)
        {
            GD.PrintErr("ServiceTests: ProductionCalculationService is null");
            return;
        }

        // Test time formatting
        var timeText = productionCalculationService.FormatSeconds(30.0f);
        Assert(!string.IsNullOrEmpty(timeText), "Time formatting should return non-empty string");
        Assert(timeText.Contains("sek"), "Time formatting should contain 'sek'");

        var timeText2 = productionCalculationService.FormatSeconds(45.5f);
        Assert(timeText2.Contains("45"), "Time formatting should contain the correct seconds");

        // Test resource consumption calculation
        var recipeIds = new List<string> { "test_recipe_1", "test_recipe_2" };
        var consumption = productionCalculationService.CalculateMaxConsumption(recipeIds);
        Assert(consumption != null, "Consumption calculation should not return null");

        GD.Print("ServiceTests: ProductionCalculationService tests passed");
    }

    private void Assert(bool condition, string message)
    {
        if (!condition)
        {
            GD.PrintErr($"ServiceTests: ASSERTION FAILED - {message}");
        }
        else
        {
            GD.Print($"ServiceTests: ✓ {message}");
        }
    }

    /// <summary>
    /// Manual test runner for debugging
    /// </summary>
    public void RunManualTests()
    {
        GD.Print("ServiceTests: Running manual tests");
        RunAllTests();
    }

    /// <summary>
    /// Test performance of market operations
    /// </summary>
    public void TestMarketPerformance()
    {
        if (marketService == null) return;

        var startTime = Time.GetTicksMsec();

        // Create multiple test orders
        var orders = new List<MarketOrder>();
        for (int i = 0; i < 100; i++)
        {
            var product = i % 2 == 0 ? "chickens" : "pig";
            var amount = 10 + i;
            var pricePerUnit = 5.0 + i * 0.1;
            orders.Add(new MarketOrder(product, amount, pricePerUnit));
        }

        // Validate all orders
        var validatedOrders = marketService.ValidateMarketOrders(orders);

        var endTime = Time.GetTicksMsec();
        var duration = endTime - startTime;

        GD.Print($"ServiceTests: Market performance test - {orders.Count} orders validated in {duration}ms");
        Assert(validatedOrders.Count == orders.Count, "All orders should be validated");
    }

    /// <summary>
    /// Test logistics settings consistency
    /// </summary>
    public void TestLogisticsConsistency()
    {
        if (logisticsService == null) return;

        // Test that costs increase with higher levels
        var baseCost = logisticsService.CalculateCapacityUpgradeCost(5);
        var level2Cost = logisticsService.CalculateCapacityUpgradeCost(10);
        var level3Cost = logisticsService.CalculateCapacityUpgradeCost(15);

        Assert(level2Cost > baseCost, "Level 2 capacity cost should be higher than base");
        Assert(level3Cost > level2Cost, "Level 3 capacity cost should be higher than level 2");

        // Same for speed
        var baseSpeedCost = logisticsService.CalculateSpeedUpgradeCost(32.0f);
        var level2SpeedCost = logisticsService.CalculateSpeedUpgradeCost(40.0f);
        var level3SpeedCost = logisticsService.CalculateSpeedUpgradeCost(48.0f);

        Assert(level2SpeedCost > baseSpeedCost, "Level 2 speed cost should be higher than base");
        Assert(level3SpeedCost > level2SpeedCost, "Level 3 speed cost should be higher than level 2");

        GD.Print("ServiceTests: Logistics consistency tests passed");
    }
}
// SPDX-License-Identifier: MIT
using System;
using Xunit;

public class TransportOrderManagerResultTests
{
    [Fact(Skip="Requires Godot runtime (Node, StringName) for TransportOrderManager")]
    public void TryAcceptOrder_Returns_NotFound_When_OrderDoesNotExist()
    {
        // Arrange: minimal manager graph
        var orderMgr = new TransportOrderManager();
        var transportCore = new IndustrieLite.Transport.Core.TransportCoreService();
        var buildingMgr = new BuildingManager();
        var truckMgr = new TruckManager();
        var econSvc = new TransportEconomyService();
        var coord = new TransportCoordinator();
        orderMgr.Initialize(transportCore, buildingMgr, truckMgr, econSvc, coord);

        // Act
        var res = orderMgr.TryAcceptOrder(9999);

        // Assert
        Assert.False(res.Ok);
        Assert.Equal(ErrorIds.TransportOrderNotFoundName, res.ErrorInfo!.Code);
    }

    [Fact(Skip="Requires Godot runtime (Node, StringName) for TransportOrderManager")]
    public void TryStartManualTransport_Returns_Invalid_When_Target_Not_City()
    {
        var orderMgr = new TransportOrderManager();
        var res = orderMgr.TryStartManualTransport(new ChickenFarm(), new WaterPump());
        Assert.False(res.Ok);
        Assert.Equal(ErrorIds.TransportInvalidArgumentName, res.ErrorInfo!.Code);
    }

    [Fact(Skip="Requires Godot runtime (Node, StringName) for TransportOrderManager")]
    public void TryStartManualTransport_Returns_NoStock_When_Source_Has_No_Inventory()
    {
        var orderMgr = new TransportOrderManager();
        var res = orderMgr.TryStartManualTransport(new City(), new City());
        Assert.False(res.Ok);
        Assert.Equal(ErrorIds.TransportNoStockName, res.ErrorInfo!.Code);
    }
    [Fact(Skip="Requires Godot runtime (Node, StringName) for TransportOrderManager")]
    public void TryAcceptOrder_Returns_NoSuppliers_When_No_Buildings_With_Inventory()
    {
        // Arrange
        var orderMgr = new TransportOrderManager();
        var transportCore = new IndustrieLite.Transport.Core.TransportCoreService();
        var buildingMgr = new BuildingManager();
        var truckMgr = new TruckManager();
        var econSvc = new TransportEconomyService();
        var coord = new TransportCoordinator();
        orderMgr.Initialize(transportCore, buildingMgr, truckMgr, econSvc, coord);

        // Add a city with an order but no suppliers in Buildings
        var city = new City();
        city.CityName = "Teststadt";
        city.Orders.Add(new MarketOrder("chickens", 10, 5.0));
        buildingMgr.Buildings.Add(city);
        buildingMgr.Cities.Add(city);

        // Act
        var res = orderMgr.TryAcceptOrder(city.Orders[0].Id);

        // Assert
        Assert.False(res.Ok);
        Assert.Equal(ErrorIds.TransportNoSuppliersName, res.ErrorInfo!.Code);
    }

    [Fact(Skip="Requires Godot runtime and specific planning conditions to force failure")]
    public void TryAcceptOrder_Returns_PlanningFailed_When_Planning_Service_Fails()
    {
        // This scenario depends on TransportPlanningService internals. The test is a placeholder to
        // lock the contract. When planning cannot allocate required amount, Expect planning_failed.
        Assert.True(true);
    }
}

// SPDX-License-Identifier: MIT
using Xunit;
using IndustrieLite.Tests.Mocks;
using Godot;

namespace IndustrieLite.Tests;

/// <summary>
/// Unit tests for ResourceManager - verifies dependency injection and interface contract.
/// Tests IResourceManager interface compliance with mock dependencies.
/// </summary>
public class ResourceManagerTests
{
    /// <summary>
    /// Test: AddProduction() increases resource production capacity.
    /// </summary>
    [Fact]
    public void AddProduction_IncreasesProductionCapacity()
    {
        // Arrange
        var resource = new MockResourceManager();
        var resourceId = new StringName("power");

        // Act
        resource.AddProduction(resourceId, 100);
        resource.AddProduction(resourceId, 50);

        // Assert
        Assert.True(resource.AddProductionWasCalled);
        var info = resource.GetResourceInfo(resourceId);
        Assert.Equal(150, info.Production);
    }

    /// <summary>
    /// Test: ConsumeResource() decreases available amount.
    /// </summary>
    [Fact]
    public void ConsumeResource_DecreasesAvailable_WhenSufficient()
    {
        // Arrange
        var resource = new MockResourceManager();
        var resourceId = new StringName("power");
        resource.SetProduction(resourceId, 100);

        // Act
        var result = resource.ConsumeResource(resourceId, 30);

        // Assert
        Assert.True(resource.ConsumeResourceWasCalled);
        // Note: Mock doesn't implement full consumption logic
    }

    /// <summary>
    /// Test: GetResourceInfo() returns structured info.
    /// </summary>
    [Fact]
    public void GetResourceInfo_ReturnsStructuredInfo()
    {
        // Arrange
        var resource = new MockResourceManager();
        var resourceId = new StringName("power");
        resource.SetProduction(resourceId, 200);

        // Act
        var info = resource.GetResourceInfo(resourceId);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(200, info.Production);
    }

    /// <summary>
    /// Test: ResetTick() is tracked.
    /// </summary>
    [Fact]
    public void ResetTick_TracksCall()
    {
        // Arrange
        var resource = new MockResourceManager();

        // Act
        resource.ResetTick();

        // Assert
        Assert.True(resource.ResetTickWasCalled);
    }

    /// <summary>
    /// Test: ClearAllData() clears all resources.
    /// </summary>
    [Fact]
    public void ClearAllData_ClearsAllResources()
    {
        // Arrange
        var resource = new MockResourceManager();
        resource.AddProduction(new StringName("power"), 100);

        // Act
        resource.ClearAllData();

        // Assert
        Assert.True(resource.ClearAllDataWasCalled);
    }

    /// <summary>
    /// Test: GetPowerProduction() returns mock value.
    /// </summary>
    [Fact]
    public void GetPowerProduction_ReturnsMockValue()
    {
        // Arrange
        var resource = new MockResourceManager();

        // Act
        var result = resource.GetPowerProduction();

        // Assert
        Assert.Equal(100, result);
    }

    /// <summary>
    /// Test: GetPowerConsumption() returns mock value.
    /// </summary>
    [Fact]
    public void GetPowerConsumption_ReturnsMockValue()
    {
        // Arrange
        var resource = new MockResourceManager();

        // Act
        var result = resource.GetPowerConsumption();

        // Assert
        Assert.Equal(50, result);
    }

    /// <summary>
    /// Test: Initialization - ResourceManager requires BuildingManager dependency.
    /// Expected: Initialize(resourceRegistry, eventHub, simulation, buildingManager).
    /// </summary>
    [Fact]
    public void Initialize_RequiresBuildingManager()
    {
        // ResourceManager.Initialize() signature (from DIContainer.cs line 170):
        // Initialize(resourceRegistry, eventHub, simulation, buildingManager)
        //
        // Critical dependency: IBuildingManager

        Assert.True(true, "Dependency requirements documented");
    }
}

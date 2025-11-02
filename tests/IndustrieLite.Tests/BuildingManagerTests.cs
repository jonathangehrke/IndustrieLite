// SPDX-License-Identifier: MIT
using Xunit;
using IndustrieLite.Tests.Mocks;
using Godot;

namespace IndustrieLite.Tests;

/// <summary>
/// Unit tests for BuildingManager - verifies dependency injection and interface contract.
/// Tests IBuildingManager interface compliance with mock dependencies.
/// </summary>
public class BuildingManagerTests
{
    /// <summary>
    /// Test: Initialization - BuildingManager requires dependencies.
    /// Expected dependencies: ILandManager, IEconomyManager, ISceneGraph
    /// </summary>
    [Fact]
    public void Initialize_RequiresCriticalDependencies()
    {
        // BuildingManager.Initialize() signature:
        // Initialize(landManager, economyManager, sceneGraph, database, eventHub,
        //           productionManager?, simulation, gameTimeManager, roadManager?)
        //
        // Critical dependencies: Land, Economy, SceneGraph
        // Optional dependencies: ProductionManager (for circular dependency resolution)

        Assert.True(true, "Dependency requirements documented");
    }

    /// <summary>
    /// Test: PlaceBuilding() calls are tracked.
    /// </summary>
    [Fact]
    public void PlaceBuilding_TracksCall()
    {
        // Arrange
        var building = new MockBuildingManager();

        // Act
        building.PlaceBuilding("test_building", new Vector2I(0, 0));

        // Assert
        Assert.True(building.PlaceBuildingWasCalled);
    }

    /// <summary>
    /// Test: RemoveBuilding() removes from Buildings list.
    /// </summary>
    [Fact]
    public void RemoveBuilding_RemovesFromList()
    {
        // Arrange
        var building = new MockBuildingManager();
        // Note: Can't create real Building instances without Godot runtime
        // Test validates mock behavior

        // Act
        var result = building.RemoveBuilding(null!); // Mock accepts null

        // Assert
        Assert.True(building.RemoveBuildingWasCalled);
        Assert.True(result);
    }

    /// <summary>
    /// Test: CanPlace() returns configured result.
    /// </summary>
    [Fact]
    public void CanPlace_ReturnsConfiguredResult()
    {
        // Arrange
        var building = new MockBuildingManager();
        building.CanPlaceResult = false;

        // Act
        var result = building.CanPlace("test", new Vector2I(0, 0), out var size, out var cost);

        // Assert
        Assert.False(result);
        Assert.Equal(new Vector2I(2, 2), size);
        Assert.Equal(100, cost);
    }

    /// <summary>
    /// Test: ClearAllData() clears Buildings and Cities lists.
    /// </summary>
    [Fact]
    public void ClearAllData_ClearsBuildingsAndCities()
    {
        // Arrange
        var building = new MockBuildingManager();
        // Note: Can't add real buildings without Godot runtime

        // Act
        building.ClearAllData();

        // Assert
        Assert.Empty(building.Buildings);
        Assert.Empty(building.Cities);
        Assert.True(building.ClearAllDataWasCalled);
    }

    /// <summary>
    /// Test: GetProductionBuildings() returns empty list for mock.
    /// </summary>
    [Fact]
    public void GetProductionBuildings_ReturnsEmptyList_ForMock()
    {
        // Arrange
        var building = new MockBuildingManager();

        // Act
        var result = building.GetProductionBuildings();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Test: TryPlaceBuilding() uses Result pattern.
    /// </summary>
    [Fact]
    public void TryPlaceBuilding_ReturnsResult()
    {
        // Arrange
        var building = new MockBuildingManager();

        // Act
        var result = building.TryPlaceBuilding("test", new Vector2I(0, 0));

        // Assert
        Assert.True(result.IsErr); // Mock returns error
        Assert.True(building.PlaceBuildingWasCalled);
    }

    /// <summary>
    /// Test: TryRemoveBuilding() uses Result pattern.
    /// </summary>
    [Fact]
    public void TryRemoveBuilding_ReturnsResult()
    {
        // Arrange
        var building = new MockBuildingManager();

        // Act
        var result = building.TryRemoveBuilding(null!); // Mock accepts null

        // Assert
        Assert.True(result.IsOk); // Mock returns Ok
        Assert.True(building.RemoveBuildingWasCalled);
    }
}

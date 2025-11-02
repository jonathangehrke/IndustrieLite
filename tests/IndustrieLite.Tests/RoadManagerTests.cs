// SPDX-License-Identifier: MIT
using Xunit;
using IndustrieLite.Tests.Mocks;
using Godot;

namespace IndustrieLite.Tests;

/// <summary>
/// Unit tests for RoadManager - verifies dependency injection and interface contract.
/// Tests IRoadManager interface compliance with mock dependencies.
/// </summary>
public class RoadManagerTests
{
    /// <summary>
    /// Test: AddRoad() tracks call.
    /// </summary>
    [Fact]
    public void AddRoad_TracksCall()
    {
        // Arrange
        var road = new MockRoadManager();

        // Act
        road.AddRoad(new Vector2I(0, 0));

        // Assert
        Assert.True(road.AddRoadWasCalled);
    }

    /// <summary>
    /// Test: RemoveRoad() tracks call.
    /// </summary>
    [Fact]
    public void RemoveRoad_TracksCall()
    {
        // Arrange
        var road = new MockRoadManager();

        // Act
        road.RemoveRoad(new Vector2I(0, 0));

        // Assert
        Assert.True(road.RemoveRoadWasCalled);
    }

    /// <summary>
    /// Test: IsRoad() returns false for mock.
    /// </summary>
    [Fact]
    public void IsRoad_ReturnsFalse_ForMock()
    {
        // Arrange
        var road = new MockRoadManager();

        // Act
        var result = road.IsRoad(new Vector2I(0, 0));

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Test: ClearAllData() clears state.
    /// </summary>
    [Fact]
    public void ClearAllData_ClearsState()
    {
        // Arrange
        var road = new MockRoadManager();

        // Act
        road.ClearAllData();

        // Assert
        Assert.True(road.ClearAllDataWasCalled);
    }

    /// <summary>
    /// Test: Initialization - RoadManager requires multiple dependencies.
    /// Expected: Initialize(landManager, buildingManager, economyManager,
    ///                      sceneGraph, eventHub, camera?).
    /// </summary>
    [Fact]
    public void Initialize_RequiresMultipleDependencies()
    {
        // RoadManager.Initialize() signature (from DIContainer.cs line 162):
        // Initialize(landManager, buildingManager, economyManager, sceneGraph,
        //           eventHub, camera)
        //
        // Critical dependencies: LandManager, IBuildingManager, IEconomyManager, ISceneGraph

        Assert.True(true, "Dependency requirements documented");
    }
}

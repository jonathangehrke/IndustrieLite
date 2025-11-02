// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of IRoadManager for testing.
/// </summary>
public class MockRoadManager : IRoadManager
{
    public bool AddRoadWasCalled { get; set; }
    public bool RemoveRoadWasCalled { get; set; }
    public bool ClearAllDataWasCalled { get; set; }

    public bool IsRoad(Vector2I cell) => false;
    public bool CanPlaceRoad(Vector2I cell) => true;
    public bool PlaceRoad(Vector2I cell)
    {
        AddRoadWasCalled = true;
        return true;
    }

    public bool RemoveRoad(Vector2I cell)
    {
        RemoveRoadWasCalled = true;
        return true;
    }

    public Result TryPlaceRoad(Vector2I cell, string? correlationId = null)
    {
        AddRoadWasCalled = true;
        return Result.Ok();
    }

    public Result TryRemoveRoad(Vector2I cell, string? correlationId = null)
    {
        RemoveRoadWasCalled = true;
        return Result.Ok();
    }

    public List<Vector2> GetPath(Vector2 fromWorld, Vector2 toWorld) => new();
    public void ClearAllRoads() { }
    public bool PlaceRoadWithoutCost(Vector2I cell)
    {
        AddRoadWasCalled = true;
        return true;
    }
}

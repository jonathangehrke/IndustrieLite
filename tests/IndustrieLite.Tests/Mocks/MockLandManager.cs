// SPDX-License-Identifier: MIT
using Godot;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of LandManager for testing.
/// Note: LandManager doesn't have an interface yet, this is a minimal stub.
/// </summary>
public class MockLandManager
{
    public bool IsOwnedWasCalled { get; set; }
    public bool OwnResult { get; set; } = true;
    public bool ClearAllDataWasCalled { get; set; }

    public bool IsOwned(Vector2I cell)
    {
        IsOwnedWasCalled = true;
        return OwnResult;
    }

    public bool Own(Vector2I cell, Vector2I size)
    {
        return true;
    }

    public void ClearAllData()
    {
        ClearAllDataWasCalled = true;
    }
}

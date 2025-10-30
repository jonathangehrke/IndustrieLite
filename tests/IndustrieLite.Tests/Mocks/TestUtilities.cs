// SPDX-License-Identifier: MIT
using Godot;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Utility class for common test setup operations.
/// Provides factory methods for creating configured mock instances.
/// </summary>
public static class TestUtilities
{
    /// <summary>
    /// Creates a fully configured MockGameManager with all standard managers registered.
    /// </summary>
    public static MockGameManager CreateFullyConfiguredGameManager()
    {
        var gameManager = new MockGameManager();

        // Register all standard managers
        gameManager.RegisterMockManager("LandManager", new MockLandManager() as Node ?? new Node());
        gameManager.RegisterMockManager("EconomyManager", new MockEconomyManager() as Node ?? new Node());
        gameManager.RegisterMockManager("BuildingManager", new MockBuildingManager() as Node ?? new Node());
        gameManager.RegisterMockManager("ProductionManager", new MockProductionManager() as Node ?? new Node());
        gameManager.RegisterMockManager("ResourceManager", new MockResourceManager() as Node ?? new Node());
        gameManager.RegisterMockManager("RoadManager", new MockRoadManager() as Node ?? new Node());
        gameManager.RegisterMockManager("TransportManager", new MockTransportManager() as Node ?? new Node());

        return gameManager;
    }

    /// <summary>
    /// Creates a MockServiceContainer with standard services registered.
    /// </summary>
    public static MockServiceContainer CreateFullyConfiguredServiceContainer()
    {
        var sc = new MockServiceContainer();

        // Register standard services
        sc.RegisterNamedService(ServiceNames.Database, new MockDatabase());
        sc.RegisterNamedService("GameDatabase", new MockGameDatabase());
        sc.RegisterNamedService(ServiceNames.EventHub, new MockEventHub());
        sc.RegisterNamedService("SceneGraphAdapter", new MockSceneGraph());

        return sc;
    }

    /// <summary>
    /// Creates a minimal MockGameManager with only critical managers.
    /// </summary>
    public static MockGameManager CreateMinimalGameManager()
    {
        var gameManager = new MockGameManager();

        // Only critical managers
        gameManager.RegisterMockManager("LandManager", new MockLandManager() as Node ?? new Node());
        gameManager.RegisterMockManager("EconomyManager", new MockEconomyManager() as Node ?? new Node());
        gameManager.RegisterMockManager("BuildingManager", new MockBuildingManager() as Node ?? new Node());
        gameManager.RegisterMockManager("ResourceManager", new MockResourceManager() as Node ?? new Node());
        gameManager.RegisterMockManager("ProductionManager", new MockProductionManager() as Node ?? new Node());

        return gameManager;
    }
}

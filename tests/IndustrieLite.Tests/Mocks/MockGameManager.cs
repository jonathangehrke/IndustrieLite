// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of GameManager for testing DIContainer.
/// Simulates node tree structure for manager resolution.
/// </summary>
public class MockGameManager : Node
{
    private readonly Dictionary<string, Node> childNodes = new();
    public bool InitializeWasCalled { get; set; }

    /// <summary>
    /// Registers a mock manager as child node for GetNodeOrNull resolution.
    /// </summary>
    public void RegisterMockManager(string name, Node manager)
    {
        childNodes[name] = manager;
    }

    /// <summary>
    /// Simulates GetNodeOrNull for mock managers.
    /// </summary>
    public new T? GetNodeOrNull<T>(string path) where T : class
    {
        if (childNodes.TryGetValue(path, out var node))
        {
            return node as T;
        }
        return null;
    }

    /// <summary>
    /// Simulates Initialize() call from DIContainer.
    /// </summary>
    public void Initialize(EventHub? eventHub)
    {
        InitializeWasCalled = true;
    }
}

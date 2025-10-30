// SPDX-License-Identifier: MIT
using Godot;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of ISceneGraph for testing.
/// Simulates scene graph operations without full Godot runtime.
/// </summary>
public class MockSceneGraph : ISceneGraph
{
    public bool AddChildWasCalled { get; set; }
    public bool RemoveChildWasCalled { get; set; }

    public void AddChild(Node node)
    {
        AddChildWasCalled = true;
    }

    public void RemoveChild(Node child)
    {
        RemoveChildWasCalled = true;
    }

    public Node GetRoot()
    {
        return new Node(); // Return dummy node for mock
    }
}

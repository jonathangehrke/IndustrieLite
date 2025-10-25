// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Port/Interface for scene graph operations.
/// Decouples managers from direct Godot Node tree manipulation.
/// Implements Hexagonal Architecture (Ports & Adapters pattern).
/// </summary>
public interface ISceneGraph
{
    /// <summary>
    /// Add a child node to the scene graph.
    /// </summary>
    void AddChild(Node node);

    /// <summary>
    /// Remove a child node from the scene graph.
    /// </summary>
    void RemoveChild(Node node);

    /// <summary>
    /// Get the root node of the scene graph (for positioning/parenting).
    /// </summary>
    /// <returns></returns>
    Node GetRoot();
}

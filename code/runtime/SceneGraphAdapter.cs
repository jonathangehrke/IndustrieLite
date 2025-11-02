// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Adapter that implements ISceneGraph for Godot's scene tree.
/// This is the concrete implementation that talks to Godot's Node API.
/// Managers depend on ISceneGraph interface, not this concrete class.
/// Implements Hexagonal Architecture (Ports & Adapters pattern).
/// </summary>
public partial class SceneGraphAdapter : Node, ISceneGraph
{
    private Node? targetParent;

    /// <summary>
    /// Set the target parent node for AddChild operations.
    /// If not set, defaults to the root node of the scene tree.
    /// </summary>
    public void SetTargetParent(Node parent)
    {
        this.targetParent = parent;
    }

    /// <inheritdoc/>
    public void AddChild(Node node)
    {
        var parent = this.targetParent ?? this.GetRoot();
        parent.AddChild(node);
        DebugLogger.LogServices($"[SceneGraphAdapter] AddChild: {node.Name} → {parent.Name}");
    }

    /// <inheritdoc/>
    public new void RemoveChild(Node node)
    {
        if (GodotObject.IsInstanceValid(node) && node.GetParent() != null)
        {
            node.GetParent().RemoveChild(node);
            node.QueueFree();
            DebugLogger.LogServices($"[SceneGraphAdapter] RemoveChild: {node.Name}");
        }
    }

    /// <inheritdoc/>
    public Node GetRoot()
    {
        // Return the scene root (typically the Root node in project)
        return this.GetTree().Root;
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Register with ServiceContainer as ISceneGraph implementation
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService("SceneGraphAdapter", this);
                DebugLogger.LogServices("[SceneGraphAdapter] Registered in ServiceContainer");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_services", "SceneGraphAdapterRegisterFailed", ex.Message);
            }
        }

        DebugLogger.LogServices("[SceneGraphAdapter] Initialized - Ready to handle scene graph operations");
    }
}

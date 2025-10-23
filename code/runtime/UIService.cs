// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService Entry-Point (Partial-Klasse)
/// Die fachliche Implementierung ist in code/runtime/ui/* aufgeteilt.
/// </summary>
public partial class UIService : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Singleton;
}


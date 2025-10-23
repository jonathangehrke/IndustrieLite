// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// UIService.ClickHandling: Click-Routing in die Spielwelt.
/// </summary>
public partial class UIService
{
    /// <summary>
    /// Handle UI click at world position.
    /// </summary>
    public void HandleWorldClick(Vector2I gridPosition)
    {
        this.inputManager?.HandleClick(gridPosition);
    }
}

// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Interface for the Road Manager - handles road placement, removal, and pathfinding.
/// </summary>
public interface IRoadManager
{
    /// <summary>
    /// Checks if the specified cell contains a road.
    /// </summary>
    bool IsRoad(Vector2I cell);

    /// <summary>
    /// Checks if a road can be placed at the specified cell.
    /// </summary>
    bool CanPlaceRoad(Vector2I cell);

    /// <summary>
    /// Places a road at the specified cell.
    /// </summary>
    bool PlaceRoad(Vector2I cell);

    /// <summary>
    /// Removes a road at the specified cell.
    /// </summary>
    bool RemoveRoad(Vector2I cell);

    /// <summary>
    /// Result variant: Places a road at the specified cell with validation and logging.
    /// </summary>
    Result TryPlaceRoad(Vector2I cell, string? correlationId = null);

    /// <summary>
    /// Result variant: Removes a road at the specified cell with validation and logging.
    /// </summary>
    Result TryRemoveRoad(Vector2I cell, string? correlationId = null);

    /// <summary>
    /// Gets a path from one world position to another.
    /// </summary>
    List<Vector2> GetPath(Vector2 fromWorld, Vector2 toWorld);

    /// <summary>
    /// Clears all roads (for NewGame).
    /// </summary>
    void ClearAllRoads();

    /// <summary>
    /// Places a road without cost deduction (for Load/Restore).
    /// </summary>
    bool PlaceRoadWithoutCost(Vector2I cell);
}

// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Adapter von RoadManager auf IRoadReadModel-Port.
/// </summary>
public sealed class RoadReadModelPort : IRoadReadModel
{
    private readonly RoadManager inner;

    public RoadReadModelPort(RoadManager inner)
    {
        this.inner = inner;
    }

    public bool IsRoad(Vector2I cell) => this.inner.IsRoad(cell);
}


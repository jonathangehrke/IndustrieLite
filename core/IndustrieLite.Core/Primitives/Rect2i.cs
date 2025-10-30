// SPDX-License-Identifier: MIT
namespace IndustrieLite.Core.Primitives;

/// <summary>
/// Godot-freies integerbasiertes Rechteck mit Position + Größe.
/// </summary>
public readonly struct Rect2i
{
    public Int2 Position { get; }
    public Int2 Size { get; }

    public Rect2i(Int2 position, Int2 size)
    {
        Position = position;
        Size = size;
    }

    public bool Intersects(Rect2i other)
    {
        var (ax, ay) = (Position.X, Position.Y);
        var (aw, ah) = (Size.X, Size.Y);
        var (bx, by) = (other.Position.X, other.Position.Y);
        var (bw, bh) = (other.Size.X, other.Size.Y);

        return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
    }
}


// SPDX-License-Identifier: MIT
namespace IndustrieLite.Core.Primitives;

/// <summary>
/// Godot-freier 2D-Integer-Vektor.
/// </summary>
public readonly record struct Int2(int X, int Y)
{
    public static readonly Int2 Zero = new(0, 0);
}


// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Port-Interface: Nur-Lese-Modell für Straßenzellen.
/// </summary>
public interface IRoadReadModel
{
    /// <summary>
    /// True, wenn an der Zelle eine Straße liegt.
    /// </summary>
    bool IsRoad(Vector2I cell);
}


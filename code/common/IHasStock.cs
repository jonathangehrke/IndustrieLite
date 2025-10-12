// SPDX-License-Identifier: MIT
using System;

/// <summary>
/// Minimales Interface fuer Einheiten mit Bestand (Stock),
/// um Logik unabhaengig von Godot Nodes testbar zu machen.
/// </summary>
[Obsolete("IHasStock ist legacy - bitte IHasInventory verwenden.")]
public interface IHasStock
{
    int Stock { get; }
}

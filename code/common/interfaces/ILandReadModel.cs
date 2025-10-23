// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Read-Only Interface fuer Land-Abfragen (Anti-Zyklen).
/// </summary>
public interface ILandReadModel
{
    int GetGridW();

    int GetGridH();

    bool IsOwned(Vector2I cell);
}


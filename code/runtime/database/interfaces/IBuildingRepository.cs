// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Spezialisierte Abfragen fuer Gebaeudedefinitionen.
/// </summary>
public interface IBuildingRepository : IDataRepository<BuildingDef>
{
    IReadOnlyCollection<BuildingDef> GetBuildable();

    Godot.Collections.Dictionary GetBuildableCatalog();

    Godot.Collections.Array<Godot.Collections.Dictionary> GetBuildablesByCategory(string category);

    Godot.Collections.Array<BuildingDef> GetGodotArray();
}


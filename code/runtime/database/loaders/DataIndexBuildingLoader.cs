// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Laedt Gebaeude ueber das DataIndex Autoload.
/// </summary>
public sealed class DataIndexBuildingLoader : IDataLoader<BuildingDef>
{
    /// <inheritdoc/>
    public string LoaderName => nameof(DataIndexBuildingLoader);

    /// <inheritdoc/>
    public int Priority => 0;

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<BuildingDef>> LoadAsync(SceneTree sceneTree)
    {
        // Try to get DataIndex from ServiceContainer first
        Node? dataIndex = null;
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            dataIndex = sc.GetNamedService<Node>("DataIndex");
        }

        if (dataIndex == null)
        {
            DebugLogger.LogServices("DataIndexBuildingLoader: Kein DataIndex gefunden");
            return Task.FromResult<IReadOnlyCollection<BuildingDef>>(System.Array.Empty<BuildingDef>());
        }

        try
        {
            var ergebnis = new List<BuildingDef>();
            var gebaeudeVar = dataIndex.Call("get_buildings");
            if (gebaeudeVar.VariantType == Variant.Type.Nil)
            {
                return Task.FromResult<IReadOnlyCollection<BuildingDef>>(System.Array.Empty<BuildingDef>());
            }

            var gebaeudeArray = (Godot.Collections.Array)gebaeudeVar;
            foreach (Variant eintrag in gebaeudeArray)
            {
                var resource = eintrag.AsGodotObject();
                if (resource is BuildingDef def && !string.IsNullOrEmpty(def.Id))
                {
                    ergebnis.Add(def);
                }
            }

            DebugLogger.LogServices(() => $"DataIndexBuildingLoader: {ergebnis.Count} Gebaeude geladen");
            return Task.FromResult<IReadOnlyCollection<BuildingDef>>(ergebnis);
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices(() => $"DataIndexBuildingLoader: Fehler {ex.Message}");
            return Task.FromResult<IReadOnlyCollection<BuildingDef>>(System.Array.Empty<BuildingDef>());
        }
    }
}


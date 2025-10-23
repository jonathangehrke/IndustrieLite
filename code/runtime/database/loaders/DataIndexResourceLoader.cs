// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Laedt Ressourcen ueber den DataIndex.
/// </summary>
public sealed class DataIndexResourceLoader : IDataLoader<GameResourceDef>
{
    public string LoaderName => nameof(DataIndexResourceLoader);

    public int Priority => 0;

    public Task<IReadOnlyCollection<GameResourceDef>> LoadAsync(SceneTree sceneTree)
    {
        var dataIndex = sceneTree.Root.GetNodeOrNull("/root/DataIndex");
        if (dataIndex == null)
        {
            DebugLogger.LogServices("DataIndexResourceLoader: Kein DataIndex gefunden");
            return Task.FromResult<IReadOnlyCollection<GameResourceDef>>(System.Array.Empty<GameResourceDef>());
        }

        try
        {
            var ergebnis = new List<GameResourceDef>();
            var ressourcenVar = dataIndex.Call("get_resources");
            if (ressourcenVar.VariantType == Variant.Type.Nil)
            {
                return Task.FromResult<IReadOnlyCollection<GameResourceDef>>(System.Array.Empty<GameResourceDef>());
            }

            var ressourcenArray = (Godot.Collections.Array)ressourcenVar;
            foreach (Variant eintrag in ressourcenArray)
            {
                var resource = eintrag.AsGodotObject();
                if (resource is GameResourceDef def && !string.IsNullOrEmpty(def.Id))
                {
                    ergebnis.Add(def);
                }
            }

            DebugLogger.LogServices(() => $"DataIndexResourceLoader: {ergebnis.Count} Ressourcen geladen");
            return Task.FromResult<IReadOnlyCollection<GameResourceDef>>(ergebnis);
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices(() => $"DataIndexResourceLoader: Fehler {ex.Message}");
            return Task.FromResult<IReadOnlyCollection<GameResourceDef>>(System.Array.Empty<GameResourceDef>());
        }
    }
}


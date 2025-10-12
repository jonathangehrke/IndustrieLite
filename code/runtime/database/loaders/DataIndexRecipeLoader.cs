// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Laedt Rezepte ueber den DataIndex.
/// </summary>
public sealed class DataIndexRecipeLoader : IDataLoader<RecipeDef>
{
    public string LoaderName => nameof(DataIndexRecipeLoader);
    public int Priority => 0;

    public Task<IReadOnlyCollection<RecipeDef>> LoadAsync(SceneTree sceneTree)
    {
        var dataIndex = sceneTree.Root.GetNodeOrNull("/root/DataIndex");
        if (dataIndex == null)
        {
            DebugLogger.LogServices("DataIndexRecipeLoader: Kein DataIndex gefunden");
            return Task.FromResult<IReadOnlyCollection<RecipeDef>>(System.Array.Empty<RecipeDef>());
        }

        try
        {
            var ergebnis = new List<RecipeDef>();
            var rezepteVar = dataIndex.Call("get_recipes");
            if (rezepteVar.VariantType == Variant.Type.Nil)
            {
                return Task.FromResult<IReadOnlyCollection<RecipeDef>>(System.Array.Empty<RecipeDef>());
            }

            var rezepteArray = (Godot.Collections.Array)rezepteVar;
            foreach (Variant eintrag in rezepteArray)
            {
                var resource = eintrag.AsGodotObject();
                if (resource is RecipeDef def && !string.IsNullOrEmpty(def.Id))
                {
                    ergebnis.Add(def);
                }
            }

            DebugLogger.LogServices(() => $"DataIndexRecipeLoader: {ergebnis.Count} Rezepte geladen");
            return Task.FromResult<IReadOnlyCollection<RecipeDef>>(ergebnis);
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices(() => $"DataIndexRecipeLoader: Fehler {ex.Message}");
            return Task.FromResult<IReadOnlyCollection<RecipeDef>>(System.Array.Empty<RecipeDef>());
        }
    }
}


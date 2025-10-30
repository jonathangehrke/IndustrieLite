// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Laedt Rezepte ueber den DataIndex.
/// </summary>
public sealed class DataIndexRecipeLoader : IDataLoader<RecipeDef>
{
    /// <inheritdoc/>
    public string LoaderName => nameof(DataIndexRecipeLoader);

    /// <inheritdoc/>
    public int Priority => 0;

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<RecipeDef>> LoadAsync(SceneTree sceneTree)
    {
        // Try ServiceContainer first, then fall back to /root/DataIndex for export builds
        Node? dataIndex = null;
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            dataIndex = sc.GetNamedService<Node>("DataIndex");
        }
        dataIndex ??= sceneTree?.Root?.GetNodeOrNull("/root/DataIndex");

        if (dataIndex == null)
        {
            DebugLogger.LogServices("DataIndexRecipeLoader: Kein DataIndex gefunden (ServiceContainer und /root/DataIndex leer)");
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
                RecipeDef? def = null;
                if (resource is RecipeDef typed && !string.IsNullOrEmpty(typed.Id))
                {
                    def = typed;
                }
                else if (resource is Resource res && !string.IsNullOrEmpty(res.ResourcePath))
                {
                    try { def = ResourceLoader.Load<RecipeDef>(res.ResourcePath); }
                    catch { def = null; }
                }
                if (def != null && !string.IsNullOrEmpty(def.Id))
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


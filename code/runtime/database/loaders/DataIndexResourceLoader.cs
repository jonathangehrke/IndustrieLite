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
    /// <inheritdoc/>
    public string LoaderName => nameof(DataIndexResourceLoader);

    /// <inheritdoc/>
    public int Priority => 0;

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<GameResourceDef>> LoadAsync(SceneTree sceneTree)
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
                    // Fix: Icons direkt laden (Export-Workaround)
                    // ExtResource-Referenzen in .tres werden im Export nicht aufgelöst
                    if (def.Icon == null)
                    {
                        string? iconPath = def.Id switch
                        {
                            "workers" => "res://assets/resources/arbeiter.png",
                            "power" => "res://assets/resources/Energie.png",
                            "grain" => "res://assets/resources/Getreide.png",
                            "chickens" => "res://assets/resources/Huhn.png",
                            "egg" => "res://assets/resources/Korb Eier.png",
                            "pig" => "res://assets/resources/Schwein.png",
                            "water" => "res://assets/resources/Wasser.png",
                            _ => null
                        };

                        if (!string.IsNullOrEmpty(iconPath) && ResourceLoader.Exists(iconPath))
                        {
                            def.Icon = ResourceLoader.Load<Texture2D>(iconPath);
                            if (def.Icon != null)
                            {
                                GD.Print($"[DataIndexResourceLoader] Icon geladen: {def.Id} → {iconPath}");
                            }
                            else
                            {
                                GD.PrintErr($"[DataIndexResourceLoader] Icon FEHLT: {def.Id} → {iconPath}");
                            }
                        }
                    }
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


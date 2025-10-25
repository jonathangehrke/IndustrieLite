// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Scannt Rezept-Definitionen als Fallback aus dem Dateisystem.
/// </summary>
public sealed class FileSystemRecipeLoader : IDataLoader<RecipeDef>
{
    private readonly string basisPfad;

    public FileSystemRecipeLoader(string basisPfad = "res://data/recipes")
    {
        this.basisPfad = basisPfad;
    }

    /// <inheritdoc/>
    public string LoaderName => nameof(FileSystemRecipeLoader);

    /// <inheritdoc/>
    public int Priority => 10;

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<RecipeDef>> LoadAsync(SceneTree sceneTree)
    {
        var daten = this.LadeAusOrdner(this.basisPfad);
        if (daten.Count > 0)
        {
            DebugLogger.LogServices(() => $"FileSystemRecipeLoader: {daten.Count} Rezepte gefunden");
        }
        return Task.FromResult<IReadOnlyCollection<RecipeDef>>(daten);
    }

    private IReadOnlyCollection<RecipeDef> LadeAusOrdner(string ordnerPfad)
    {
        var ergebnis = new List<RecipeDef>();
        var dir = DirAccess.Open(ordnerPfad);
        if (dir == null)
        {
            DebugLogger.LogDatabase(() => $"FileSystemRecipeLoader: Kann Ordner nicht oeffnen {ordnerPfad}");
            return ergebnis;
        }

        dir.ListDirBegin();
        while (true)
        {
            var datei = dir.GetNext();
            if (string.IsNullOrEmpty(datei))
            {
                break;
            }
            if (dir.CurrentIsDir())
            {
                continue;
            }
            if (!(datei.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)
               || datei.EndsWith(".res",  StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var pfad = $"{ordnerPfad}/{datei}";
            var resource = ResourceLoader.Load<RecipeDef>(pfad);
            if (resource != null && !string.IsNullOrEmpty(resource.Id))
            {
                ergebnis.Add(resource);
            }
        }
        dir.ListDirEnd();
        dir.Dispose();
        return ergebnis;
    }
}



// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Scannt Ressourcen-Definitionen aus dem Dateisystem.
/// </summary>
public sealed class FileSystemResourceLoader : IDataLoader<GameResourceDef>
{
    private readonly string basisPfad;

    public FileSystemResourceLoader(string basisPfad = "res://data/resources")
    {
        this.basisPfad = basisPfad;
    }

    /// <inheritdoc/>
    public string LoaderName => nameof(FileSystemResourceLoader);

    /// <inheritdoc/>
    public int Priority => 10;

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<GameResourceDef>> LoadAsync(SceneTree sceneTree)
    {
        var daten = this.LadeAusOrdner(this.basisPfad);
        if (daten.Count > 0)
        {
            DebugLogger.LogServices(() => $"FileSystemResourceLoader: {daten.Count} Ressourcen gefunden");
        }
        return Task.FromResult<IReadOnlyCollection<GameResourceDef>>(daten);
    }

    private IReadOnlyCollection<GameResourceDef> LadeAusOrdner(string ordnerPfad)
    {
        var ergebnis = new List<GameResourceDef>();
        var dir = DirAccess.Open(ordnerPfad);
        if (dir == null)
        {
            DebugLogger.LogDatabase(() => $"FileSystemResourceLoader: Kann Ordner nicht oeffnen {ordnerPfad}");
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
            var resource = ResourceLoader.Load<GameResourceDef>(pfad);
            if (resource != null && !string.IsNullOrEmpty(resource.Id))
            {
                // Fix: Icons nachsetzen falls ExtResource nicht aufgelöst (Export-Workaround)
                if (resource.Icon == null)
                {
                    string? iconPath = resource.Id switch
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
                        resource.Icon = ResourceLoader.Load<Texture2D>(iconPath);
                    }
                }
                ergebnis.Add(resource);
            }
        }
        dir.ListDirEnd();
        dir.Dispose();
        return ergebnis;
    }
}



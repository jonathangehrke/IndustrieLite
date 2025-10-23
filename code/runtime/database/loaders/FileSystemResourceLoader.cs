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

    public string LoaderName => nameof(FileSystemResourceLoader);

    public int Priority => 10;

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
        if (!DirAccess.DirExistsAbsolute(ordnerPfad))
        {
            DebugLogger.LogDatabase(() => $"FileSystemResourceLoader: Ordner fehlt {ordnerPfad}");
            return ergebnis;
        }

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
            if (!datei.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var pfad = $"{ordnerPfad}/{datei}";
            var resource = ResourceLoader.Load<GameResourceDef>(pfad);
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



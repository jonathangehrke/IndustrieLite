// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Scannt das Dateisystem nach BuildingDef .tres-Dateien.
/// </summary>
public sealed class FileSystemBuildingLoader : IDataLoader<BuildingDef>
{
    private readonly string basisPfad;

    public FileSystemBuildingLoader(string basisPfad = "res://data/buildings")
    {
        this.basisPfad = basisPfad;
    }

    /// <inheritdoc/>
    public string LoaderName => nameof(FileSystemBuildingLoader);

    /// <inheritdoc/>
    public int Priority => 10;

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<BuildingDef>> LoadAsync(SceneTree sceneTree)
    {
        var daten = this.LadeAusOrdner(this.basisPfad);
        if (daten.Count > 0)
        {
            DebugLogger.LogServices(() => $"FileSystemBuildingLoader: {daten.Count} Gebaeude gefunden");
        }
        return Task.FromResult<IReadOnlyCollection<BuildingDef>>(daten);
    }

    private IReadOnlyCollection<BuildingDef> LadeAusOrdner(string ordnerPfad)
    {
        var ergebnis = new List<BuildingDef>();
        if (!DirAccess.DirExistsAbsolute(ordnerPfad))
        {
            DebugLogger.LogDatabase(() => $"FileSystemBuildingLoader: Ordner fehlt {ordnerPfad}");
            return ergebnis;
        }

        var dir = DirAccess.Open(ordnerPfad);
        if (dir == null)
        {
            DebugLogger.LogDatabase(() => $"FileSystemBuildingLoader: Kann Ordner nicht oeffnen {ordnerPfad}");
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
            var resource = ResourceLoader.Load<BuildingDef>(pfad);
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



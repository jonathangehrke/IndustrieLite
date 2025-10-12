// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Repository fuer Ressourcen-Definitionen.
/// </summary>
public sealed class ResourceRepository : BaseRepository<GameResourceDef>, IResourceRepository
{
    public ResourceRepository(Func<bool> legacyErlaubt)
    {
        ladeReihenfolge.Add(new DataIndexResourceLoader());
        ladeReihenfolge.Add(new FileSystemResourceLoader());
        ladeReihenfolge.Add(new LegacyResourceLoader(legacyErlaubt));
    }

    protected override string GetId(GameResourceDef item) => item.Id;

    protected override string? GetKategorie(GameResourceDef item) => item.Category;

    public IReadOnlyCollection<GameResourceDef> GetByType(string type)
    {
        return GetByCategory(type);
    }
}



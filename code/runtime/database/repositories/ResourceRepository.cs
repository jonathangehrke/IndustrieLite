// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Repository fuer Ressourcen-Definitionen.
/// </summary>
public sealed class ResourceRepository : BaseRepository<GameResourceDef>, IResourceRepository
{
    public ResourceRepository(Func<bool> legacyErlaubt)
    {
        this.ladeReihenfolge.Add(new DataIndexResourceLoader());
        this.ladeReihenfolge.Add(new FileSystemResourceLoader());
        this.ladeReihenfolge.Add(new LegacyResourceLoader(legacyErlaubt));
    }

    /// <inheritdoc/>
    protected override string GetId(GameResourceDef item) => item.Id;

    /// <inheritdoc/>
    protected override string? GetKategorie(GameResourceDef item) => item.Category;

    /// <inheritdoc/>
    public IReadOnlyCollection<GameResourceDef> GetByType(string type)
    {
        return this.GetByCategory(type);
    }
}



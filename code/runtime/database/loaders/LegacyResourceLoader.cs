// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Legacy-Fallback fuer Ressourcen.
/// </summary>
public sealed class LegacyResourceLoader : IDataLoader<GameResourceDef>
{
    private readonly Func<bool> legacyErlaubt;

    public LegacyResourceLoader(Func<bool> legacyErlaubt)
    {
        this.legacyErlaubt = legacyErlaubt;
    }

    public string LoaderName => nameof(LegacyResourceLoader);
    public int Priority => 100;

    public Task<IReadOnlyCollection<GameResourceDef>> LoadAsync(SceneTree sceneTree)
    {
        if (!IsFallbackAktiv())
        {
            return Task.FromResult<IReadOnlyCollection<GameResourceDef>>(System.Array.Empty<GameResourceDef>());
        }

        var ressourcen = new List<GameResourceDef>
        {
            new GameResourceDef(ResourceIds.Power, "Strom", "basis"),
            new GameResourceDef(ResourceIds.Water, "Wasser", "basis"),
            new GameResourceDef(ResourceIds.Chickens, "Huehner", "produktion"),
            new GameResourceDef(ResourceIds.Grain, "Getreide", "produktion"),
            new GameResourceDef(ResourceIds.Pig, "Schwein", "produktion"),
            new GameResourceDef(ResourceIds.Egg, "Eier", "produktion")
        };

        DebugLogger.LogServices(() => $"LegacyResourceLoader: {ressourcen.Count} Legacy-Ressourcen erstellt");
        return Task.FromResult<IReadOnlyCollection<GameResourceDef>>(ressourcen);
    }

    private bool IsFallbackAktiv() => OS.IsDebugBuild() || legacyErlaubt();
}



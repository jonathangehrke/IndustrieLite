// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Repository fuer Gebaeude inklusive Legacy- und UI-spezifischer Abfragen.
/// </summary>
public sealed class BuildingRepository : BaseRepository<BuildingDef>, IBuildingRepository
{
    private readonly Godot.Collections.Array<BuildingDef> godotListe = new();

    public BuildingRepository(Func<bool> legacyErlaubt)
    {
        ladeReihenfolge.Add(new DataIndexBuildingLoader());
        ladeReihenfolge.Add(new FileSystemBuildingLoader());
        ladeReihenfolge.Add(new LegacyBuildingLoader(legacyErlaubt));
    }

    protected override string GetId(BuildingDef item) => item.Id;

    protected override string? GetKategorie(BuildingDef item) => item.Category;

    protected override void NachCacheAktualisierung(IReadOnlyCollection<BuildingDef> items)
    {
        godotListe.Clear();
        foreach (var def in items)
        {
            godotListe.Add(def);
        }
    }

    protected override BuildingDef? ResolveLegacyId(string id)
    {
        return eintraegeNachId.Values.FirstOrDefault(def => def.LegacyIds.Contains(id));
    }

    public IReadOnlyCollection<BuildingDef> GetBuildable()
    {
        return eintraegeNachId.Values.Where(def => def.Cost > 0 && !def.Tags.Contains("non-buildable")).ToList();
    }

    public Godot.Collections.Dictionary GetBuildableCatalog()
    {
        var katalog = new Godot.Collections.Dictionary();
        foreach (var def in GetBuildable())
        {
            var kategorie = string.IsNullOrEmpty(def.Category) ? "uncategorized" : def.Category;
            if (!katalog.ContainsKey(kategorie))
            {
                katalog[kategorie] = new Godot.Collections.Array<BuildingDef>();
            }
            ((Godot.Collections.Array<BuildingDef>)katalog[kategorie]).Add(def);
        }
        return katalog;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetBuildablesByCategory(string category)
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (string.IsNullOrEmpty(category) || string.Equals(category, "buildable", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var def in GetBuildable())
            {
                result.Add(KonvertiereZuDictionary(def));
            }
            return result;
        }

        foreach (var def in eintraegeNachId.Values)
        {
            if (!string.Equals(def.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            result.Add(KonvertiereZuDictionary(def));
        }
        return result;
    }

    public Godot.Collections.Array<BuildingDef> GetGodotArray()
    {
        return new Godot.Collections.Array<BuildingDef>(godotListe);
    }

    private Godot.Collections.Dictionary KonvertiereZuDictionary(BuildingDef def)
    {
        var dict = new Godot.Collections.Dictionary
        {
            { "id", def.Id },
            { "label", string.IsNullOrEmpty(def.DisplayName) ? def.Id : def.DisplayName },
            { "cost", def.Cost },
        };
        if (def.Icon != null)
        {
            dict["icon"] = def.Icon;
        }
        return dict;
    }
}



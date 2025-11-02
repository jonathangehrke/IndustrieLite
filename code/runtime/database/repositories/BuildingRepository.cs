// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Repository fuer Gebaeude inklusive Legacy- und UI-spezifischer Abfragen.
/// </summary>
public sealed class BuildingRepository : BaseRepository<BuildingDef>, IBuildingRepository
{
    private readonly Godot.Collections.Array<BuildingDef> godotListe = new();

    public BuildingRepository(Func<bool> legacyErlaubt)
    {
        this.ladeReihenfolge.Add(new DataIndexBuildingLoader());
        this.ladeReihenfolge.Add(new FileSystemBuildingLoader());
        this.ladeReihenfolge.Add(new LegacyBuildingLoader(legacyErlaubt));
    }

    /// <inheritdoc/>
    protected override string GetId(BuildingDef item) => item.Id;

    /// <inheritdoc/>
    protected override string? GetKategorie(BuildingDef item) => NormalizeCategory(item.Category);

    /// <summary>
    /// Normalisiert Kategorienamen (englisch → deutsch) für Robustheit.
    /// </summary>
    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        var normalized = category.Trim();
        return normalized switch
        {
            "infrastructure" => "Infrastruktur",
            "power" => "Energie",
            "water" => "Infrastruktur",
            "production" => "Produktion",
            "residential" => "Wohnen",
            "commercial" => "Städte",
            "staedte" => "Städte",
            "cities" => "Städte",
            "produktion" => "Produktion",
            "energie" => "Energie",
            "wohnen" => "Wohnen",
            _ => normalized
        };
    }

    /// <inheritdoc/>
    protected override void NachCacheAktualisierung(IReadOnlyCollection<BuildingDef> items)
    {
        this.godotListe.Clear();
        foreach (var def in items)
        {
            this.godotListe.Add(def);
        }
    }

    /// <inheritdoc/>
    protected override BuildingDef? ResolveLegacyId(string id)
    {
        foreach (var def in this.eintraegeNachId.Values)
        {
            if (def.LegacyIds != null)
            {
                foreach (var legacy in def.LegacyIds)
                {
                    if (string.Equals(legacy, id, StringComparison.Ordinal))
                    {
                        return def;
                    }
                }
            }
        }
        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<BuildingDef> GetBuildable()
    {
        var list = new List<BuildingDef>();
        foreach (var def in this.eintraegeNachId.Values)
        {
            if (def.Cost <= 0)
            {
                continue;
            }
            bool hasNonBuildable = false;
            if (def.Tags != null)
            {
                foreach (var tag in def.Tags)
                {
                    if (string.Equals(tag, "non-buildable", StringComparison.Ordinal))
                    {
                        hasNonBuildable = true;
                        break;
                    }
                }
            }
            if (!hasNonBuildable)
            {
                list.Add(def);
            }
        }
        return list;
    }

    /// <inheritdoc/>
    public Godot.Collections.Dictionary GetBuildableCatalog()
    {
        var katalog = new Godot.Collections.Dictionary();
        foreach (var def in this.GetBuildable())
        {
            var kategorie = NormalizeCategory(def.Category);
            if (string.IsNullOrEmpty(kategorie))
            {
                kategorie = "uncategorized";
            }
            if (!katalog.ContainsKey(kategorie))
            {
                katalog[kategorie] = new Godot.Collections.Array<BuildingDef>();
            }
            ((Godot.Collections.Array<BuildingDef>)katalog[kategorie]).Add(def);
        }
        return katalog;
    }

    /// <inheritdoc/>
    public Godot.Collections.Array<Godot.Collections.Dictionary> GetBuildablesByCategory(string category)
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (string.IsNullOrEmpty(category) || string.Equals(category, "buildable", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var def in this.GetBuildable())
            {
                result.Add(this.KonvertiereZuDictionary(def));
            }
            return result;
        }

        // Normalisiere beide Kategorien für zuverlässigen Vergleich
        var normalizedCategory = NormalizeCategory(category);
        foreach (var def in this.eintraegeNachId.Values)
        {
            var defCategory = NormalizeCategory(def.Category);
            if (!string.Equals(defCategory, normalizedCategory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            result.Add(this.KonvertiereZuDictionary(def));
        }
        return result;
    }

    /// <inheritdoc/>
    public Godot.Collections.Array<BuildingDef> GetGodotArray()
    {
        return new Godot.Collections.Array<BuildingDef>(this.godotListe);
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



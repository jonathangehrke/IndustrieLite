// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Adapter, der die alte Database-API gegen das neue GameDatabase-System spiegelt.
/// </summary>
public partial class DatabaseLegacyAdapter : Node
{
    private static readonly IReadOnlyDictionary<string, BuildingDef> LeereGebaeude = new Dictionary<string, BuildingDef>();
    private static readonly IReadOnlyDictionary<string, GameResourceDef> LeereRessourcen = new Dictionary<string, GameResourceDef>();
    private static readonly IReadOnlyDictionary<string, RecipeDef> LeereRezepte = new Dictionary<string, RecipeDef>();

    private IGameDatabase? aktuelleDatenbank;
    private bool gespeichertesLegacyFlag;

    protected IGameDatabase? AktiveDatenbank => aktuelleDatenbank;

    protected bool LegacyFallbackErlaubt
    {
        get => aktuelleDatenbank?.AllowLegacyFallbackInRelease ?? gespeichertesLegacyFlag;
        set
        {
            gespeichertesLegacyFlag = value;
            if (aktuelleDatenbank != null)
            {
                aktuelleDatenbank.AllowLegacyFallbackInRelease = value;
            }
        }
    }

    protected void VerbindeMitGameDatabase(IGameDatabase database)
    {
        aktuelleDatenbank = database ?? throw new ArgumentNullException(nameof(database));
        aktuelleDatenbank.AllowLegacyFallbackInRelease = LegacyFallbackErlaubt;
    }

    public IReadOnlyDictionary<string, BuildingDef> BuildingsById => aktuelleDatenbank != null
        ? ErzeugeDictionary(aktuelleDatenbank.Buildings, def => def.Id)
        : LeereGebaeude;

    public Godot.Collections.Array<BuildingDef> BuildingsList => aktuelleDatenbank?.Buildings is IBuildingRepository buildingRepo
        ? buildingRepo.GetGodotArray()
        : new Godot.Collections.Array<BuildingDef>();

    public IReadOnlyDictionary<string, GameResourceDef> ResourcesById => aktuelleDatenbank != null
        ? ErzeugeDictionary(aktuelleDatenbank.Resources, res => res.Id)
        : LeereRessourcen;

    public IReadOnlyDictionary<string, RecipeDef> RecipesById => aktuelleDatenbank != null
        ? ErzeugeDictionary(aktuelleDatenbank.Recipes, recipe => recipe.Id)
        : LeereRezepte;

    public BuildingDef? GetBuilding(string id)
    {
        if (aktuelleDatenbank == null)
        {
            return null;
        }
        var result = aktuelleDatenbank.Buildings.TryGet(id);
        return result.Ok ? result.Value : null;
    }

    public Godot.Collections.Array<BuildingDef> GetBuildingsByCategory(string category)
    {
        if (aktuelleDatenbank?.Buildings == null)
        {
            return new Godot.Collections.Array<BuildingDef>();
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return ((IBuildingRepository)aktuelleDatenbank.Buildings).GetGodotArray();
        }

        var ergebnis = new Godot.Collections.Array<BuildingDef>();
        foreach (var def in aktuelleDatenbank.Buildings.GetByCategory(category))
        {
            ergebnis.Add(def);
        }
        return ergebnis;
    }

    public Godot.Collections.Array<BuildingDef> GetBuildableBuildings()
    {
        if (aktuelleDatenbank?.Buildings is IBuildingRepository repo)
        {
            var array = new Godot.Collections.Array<BuildingDef>();
            foreach (var def in repo.GetBuildable())
            {
                array.Add(def);
            }
            return array;
        }
        return new Godot.Collections.Array<BuildingDef>();
    }

    public Godot.Collections.Dictionary GetBuildableCatalog()
    {
        if (aktuelleDatenbank?.Buildings is IBuildingRepository repo)
        {
            return repo.GetBuildableCatalog();
        }
        return new Godot.Collections.Dictionary();
    }

    public RecipeDef? GetRecipe(string id)
    {
        if (aktuelleDatenbank == null)
        {
            return null;
        }
        return aktuelleDatenbank.Recipes.GetById(id);
    }

    public IReadOnlyCollection<RecipeDef> GetAllRecipes()
    {
        if (aktuelleDatenbank == null)
        {
            return Array.Empty<RecipeDef>();
        }
        return aktuelleDatenbank.Recipes.GetAll();
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetBuildablesByCategory(string category = "buildable")
    {
        if (aktuelleDatenbank?.Buildings is IBuildingRepository repo)
        {
            return repo.GetBuildablesByCategory(category);
        }
        return new Godot.Collections.Array<Godot.Collections.Dictionary>();
    }

    public Result<BuildingDef> TryGetBuilding(string id)
    {
        if (aktuelleDatenbank == null)
        {
            return Result<BuildingDef>.Fail("GameDatabase nicht initialisiert.");
        }
        return aktuelleDatenbank.Buildings.TryGet(id);
    }

    protected void LogMigrationStatus()
    {
        if (aktuelleDatenbank == null)
        {
            DebugLogger.LogServices("DatabaseLegacyAdapter: GameDatabase nicht gesetzt");
            return;
        }

        DebugLogger.LogServices("=== Datenbank-Status ===");
        DebugLogger.LogServices(() => $"Ressourcen: {aktuelleDatenbank.Resources.GetAll().Count}");
        DebugLogger.LogServices(() => $"Gebaeude: {aktuelleDatenbank.Buildings.GetAll().Count}");
        DebugLogger.LogServices(() => $"Rezepte: {aktuelleDatenbank.Recipes.GetAll().Count}");
        DebugLogger.LogServices("=== Status Ende ===");
    }

    private static IReadOnlyDictionary<string, T> ErzeugeDictionary<T>(IDataRepository<T> repository, Func<T, string> keySelector) where T : Resource
    {
        var dict = new Dictionary<string, T>();
        foreach (var item in repository.GetAll())
        {
            var key = keySelector(item);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            dict[key] = item;
        }
        return dict;
    }
}





// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Adapter, der die alte Database-API gegen das neue GameDatabase-System spiegelt.
/// </summary>
public partial class DatabaseLegacyAdapter : Node
{
    private static readonly IReadOnlyDictionary<string, BuildingDef> LeereGebaeude = new Dictionary<string, BuildingDef>(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, GameResourceDef> LeereRessourcen = new Dictionary<string, GameResourceDef>(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, RecipeDef> LeereRezepte = new Dictionary<string, RecipeDef>(StringComparer.Ordinal);

    private IGameDatabase? aktuelleDatenbank;
    private bool gespeichertesLegacyFlag;

    protected IGameDatabase? AktiveDatenbank => this.aktuelleDatenbank;

    protected bool LegacyFallbackErlaubt
    {
        get => this.aktuelleDatenbank?.AllowLegacyFallbackInRelease ?? this.gespeichertesLegacyFlag;
        set
        {
            this.gespeichertesLegacyFlag = value;
            if (this.aktuelleDatenbank != null)
            {
                this.aktuelleDatenbank.AllowLegacyFallbackInRelease = value;
            }
        }
    }

    protected void VerbindeMitGameDatabase(IGameDatabase database)
    {
        this.aktuelleDatenbank = database ?? throw new ArgumentNullException(nameof(database));
        this.aktuelleDatenbank.AllowLegacyFallbackInRelease = this.LegacyFallbackErlaubt;
    }

    public IReadOnlyDictionary<string, BuildingDef> BuildingsById => this.aktuelleDatenbank != null
        ? ErzeugeDictionary(this.aktuelleDatenbank.Buildings, def => def.Id)
        : LeereGebaeude;

    public Godot.Collections.Array<BuildingDef> BuildingsList => this.aktuelleDatenbank?.Buildings is IBuildingRepository buildingRepo
        ? buildingRepo.GetGodotArray()
        : new Godot.Collections.Array<BuildingDef>();

    public IReadOnlyDictionary<string, GameResourceDef> ResourcesById => this.aktuelleDatenbank != null
        ? ErzeugeDictionary(this.aktuelleDatenbank.Resources, res => res.Id)
        : LeereRessourcen;

    public IReadOnlyDictionary<string, RecipeDef> RecipesById => this.aktuelleDatenbank != null
        ? ErzeugeDictionary(this.aktuelleDatenbank.Recipes, recipe => recipe.Id)
        : LeereRezepte;

    public BuildingDef? GetBuilding(string id)
    {
        if (this.aktuelleDatenbank == null)
        {
            return null;
        }
        var result = this.aktuelleDatenbank.Buildings.TryGet(id);
        return result.Ok ? result.Value : null;
    }

    public Godot.Collections.Array<BuildingDef> GetBuildingsByCategory(string category)
    {
        if (this.aktuelleDatenbank?.Buildings == null)
        {
            return new Godot.Collections.Array<BuildingDef>();
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return ((IBuildingRepository)this.aktuelleDatenbank.Buildings).GetGodotArray();
        }

        var ergebnis = new Godot.Collections.Array<BuildingDef>();
        foreach (var def in this.aktuelleDatenbank.Buildings.GetByCategory(category))
        {
            ergebnis.Add(def);
        }
        return ergebnis;
    }

    public Godot.Collections.Array<BuildingDef> GetBuildableBuildings()
    {
        if (this.aktuelleDatenbank?.Buildings is IBuildingRepository repo)
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
        if (this.aktuelleDatenbank?.Buildings is IBuildingRepository repo)
        {
            return repo.GetBuildableCatalog();
        }
        return new Godot.Collections.Dictionary();
    }

    public RecipeDef? GetRecipe(string id)
    {
        if (this.aktuelleDatenbank == null)
        {
            return null;
        }
        return this.aktuelleDatenbank.Recipes.GetById(id);
    }

    public IReadOnlyCollection<RecipeDef> GetAllRecipes()
    {
        if (this.aktuelleDatenbank == null)
        {
            return Array.Empty<RecipeDef>();
        }
        return this.aktuelleDatenbank.Recipes.GetAll();
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetBuildablesByCategory(string category = "buildable")
    {
        if (this.aktuelleDatenbank?.Buildings is IBuildingRepository repo)
        {
            return repo.GetBuildablesByCategory(category);
        }
        return new Godot.Collections.Array<Godot.Collections.Dictionary>();
    }

    public Result<BuildingDef> TryGetBuilding(string id)
    {
        if (this.aktuelleDatenbank == null)
        {
            return Result<BuildingDef>.Fail("GameDatabase nicht initialisiert.");
        }
        return this.aktuelleDatenbank.Buildings.TryGet(id);
    }

    protected void LogMigrationStatus()
    {
        if (this.aktuelleDatenbank == null)
        {
            DebugLogger.LogServices("DatabaseLegacyAdapter: GameDatabase nicht gesetzt");
            return;
        }

        DebugLogger.LogServices("=== Datenbank-Status ===");
        DebugLogger.LogServices(() => $"Ressourcen: {this.aktuelleDatenbank.Resources.GetAll().Count}");
        DebugLogger.LogServices(() => $"Gebaeude: {this.aktuelleDatenbank.Buildings.GetAll().Count}");
        DebugLogger.LogServices(() => $"Rezepte: {this.aktuelleDatenbank.Recipes.GetAll().Count}");
        DebugLogger.LogServices("=== Status Ende ===");
    }

    private static IReadOnlyDictionary<string, T> ErzeugeDictionary<T>(IDataRepository<T> repository, Func<T, string> keySelector)
        where T : Resource
    {
        var dict = new Dictionary<string, T>(StringComparer.Ordinal);
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





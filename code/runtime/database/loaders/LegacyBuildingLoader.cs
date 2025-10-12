// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Legacy-Fallback fuer Gebaeudedefinitionen, damit der Build auch ohne Datenpakete lauffaehig bleibt.
/// </summary>
public sealed class LegacyBuildingLoader : IDataLoader<BuildingDef>
{
    private readonly Func<bool> legacyErlaubt;

    public LegacyBuildingLoader(Func<bool> legacyErlaubt)
    {
        this.legacyErlaubt = legacyErlaubt;
    }

    public string LoaderName => nameof(LegacyBuildingLoader);
    public int Priority => 100;

    public Task<IReadOnlyCollection<BuildingDef>> LoadAsync(SceneTree sceneTree)
    {
        if (!IsFallbackAktiv())
        {
            return Task.FromResult<IReadOnlyCollection<BuildingDef>>(System.Array.Empty<BuildingDef>());
        }

        var legacyBuildings = ErzeugeLegacyGebaeude();
        DebugLogger.LogServices(() => $"LegacyBuildingLoader: {legacyBuildings.Count} Legacy-Gebaeude erstellt");
        return Task.FromResult<IReadOnlyCollection<BuildingDef>>(legacyBuildings);
    }

    private bool IsFallbackAktiv() => OS.IsDebugBuild() || legacyErlaubt();

    private List<BuildingDef> ErzeugeLegacyGebaeude()
    {
        var gebaeude = new List<BuildingDef>
        {
            new BuildingDef(BuildingIds.House, "Haus", 2, 2, 400.0) { Category = "residential" },
            new BuildingDef(BuildingIds.SolarPlant, "Solaranlage", 3, 2, 800.0) { Category = "power" },
            new BuildingDef(BuildingIds.WaterPump, "Wasserpumpe", 2, 2, 600.0) { Category = "water" },
            new BuildingDef(BuildingIds.ChickenFarm, "Huehnerfarm", 3, 3, 2500.0) { Category = "production" },
            new BuildingDef("pig_farm", "Schweinestall", 3, 3, 5000.0) { Category = "production" },
            new BuildingDef("grain_farm", "Bauernhof", 3, 3, 1000.0) { Category = "production" },
            new BuildingDef(BuildingIds.City, "Stadt", 4, 4, 0.0) { Category = "commercial" },
            new BuildingDef("road", "Strasse", 1, 1, 50.0) { Category = "infrastructure" }
        };

        foreach (var def in gebaeude)
        {
            switch (def.Id)
            {
                case BuildingIds.ChickenFarm:
                    def.DefaultRecipeId = RecipeIds.ChickenProduction;
                    def.AvailableRecipes.Add(RecipeIds.ChickenProduction);
                    def.WorkersRequired = 2;
                    break;
                case BuildingIds.SolarPlant:
                    def.DefaultRecipeId = RecipeIds.PowerGeneration;
                    def.AvailableRecipes.Add(RecipeIds.PowerGeneration);
                    break;
                case BuildingIds.WaterPump:
                    def.DefaultRecipeId = RecipeIds.WaterProduction;
                    def.AvailableRecipes.Add(RecipeIds.WaterProduction);
                    break;
                case "grain_farm":
                    def.DefaultRecipeId = "grain_production";
                    def.AvailableRecipes.Add("grain_production");
                    def.WorkersRequired = 2;
                    break;
                case "pig_farm":
                    def.DefaultRecipeId = "pig_production";
                    def.AvailableRecipes.Add("pig_production");
                    def.WorkersRequired = 3;
                    break;
            }
        }

        foreach (var def in gebaeude)
        {
            switch (def.Id)
            {
                case "house":
                    def.LegacyIds.Add("House");
                    break;
                case "solar_plant":
                    def.LegacyIds.Add("Solar");
                    break;
                case "water_pump":
                    def.LegacyIds.Add("Water");
                    break;
                case "chicken_farm":
                    def.LegacyIds.Add("ChickenFarm");
                    break;
                case "pig_farm":
                    def.LegacyIds.Add("PigFarm");
                    break;
                case "grain_farm":
                    def.LegacyIds.Add("Farm");
                    break;
                case "city":
                    def.LegacyIds.Add("City");
                    break;
                case "road":
                    def.LegacyIds.Add("Road");
                    break;
            }
        }

        SetzeFallbackIcons(gebaeude);
        return gebaeude;
    }

    private void SetzeFallbackIcons(IEnumerable<BuildingDef> gebaeude)
    {
        foreach (var def in gebaeude)
        {
            switch (def.Id)
            {
                case "house":
                    def.Icon = LadeIcon("res://assets/buildings/haus.png");
                    break;
                case "chicken_farm":
                    def.Icon = LadeIcon("res://assets/buildings/Huehnerstall.png");
                    break;
                case "pig_farm":
                    def.Icon = LadeIcon("res://assets/buildings/Schweinestall.png");
                    break;
                case "grain_farm":
                    def.Icon = LadeIcon("res://assets/buildings/Bauernhof.png");
                    break;
                case "city":
                    def.Icon = LadeIcon("res://assets/buildings/stadt.png");
                    break;
                case "road":
                    def.Icon = LadeIcon("res://assets/tiles/strasse.png");
                    break;
            }
        }
    }

    private Texture2D? LadeIcon(string pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad))
        {
            return null;
        }
        if (!ResourceLoader.Exists(pfad))
        {
            DebugLogger.LogDatabase(() => $"LegacyBuildingLoader: Icon fehlt {pfad}");
            return null;
        }
        var icon = ResourceLoader.Load<Texture2D>(pfad);
        if (icon == null)
        {
            DebugLogger.LogDatabase(() => $"LegacyBuildingLoader: Icon konnte nicht geladen werden {pfad}");
        }
        return icon;
    }
}





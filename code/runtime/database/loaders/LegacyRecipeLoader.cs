// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Legacy-Fallback fuer Rezepte.
/// </summary>
public sealed class LegacyRecipeLoader : IDataLoader<RecipeDef>
{
    private readonly Func<bool> legacyErlaubt;

    public LegacyRecipeLoader(Func<bool> legacyErlaubt)
    {
        this.legacyErlaubt = legacyErlaubt;
    }

    public string LoaderName => nameof(LegacyRecipeLoader);

    public int Priority => 100;

    public Task<IReadOnlyCollection<RecipeDef>> LoadAsync(SceneTree sceneTree)
    {
        if (!this.IsFallbackAktiv())
        {
            return Task.FromResult<IReadOnlyCollection<RecipeDef>>(System.Array.Empty<RecipeDef>());
        }

        var rezepte = this.ErzeugeLegacyRezepte();
        DebugLogger.LogServices(() => $"LegacyRecipeLoader: {rezepte.Count} Legacy-Rezepte erstellt");
        return Task.FromResult<IReadOnlyCollection<RecipeDef>>(rezepte);
    }

    private bool IsFallbackAktiv() => OS.IsDebugBuild() || this.legacyErlaubt();

    private List<RecipeDef> ErzeugeLegacyRezepte()
    {
        var rezepte = new List<RecipeDef>();

        var rChicken = new RecipeDef(RecipeIds.ChickenProduction, 60.0f)
        {
            DisplayName = "Huehnerproduktion",
            PowerRequirement = 2.0f,
            WaterRequirement = 2.0f,
            ProductionCost = 0.5f,
            MaintenanceCost = 2.0f,
        };
        rChicken.Outputs.Add(new Amount(ResourceIds.Chickens, 60.0f));
        // Wichtig: Getreide als Input definieren, damit die UI Bedarfe korrekt anzeigt
        // und Lieferantenauswahl (Bauernhof -> Hühnerstall) ermöglicht
        rChicken.Inputs.Add(new Amount(ResourceIds.Grain, 60.0f));
        rezepte.Add(rChicken);

        var rPower = new RecipeDef(RecipeIds.PowerGeneration, 60.0f)
        {
            DisplayName = "Stromerzeugung",
            PowerRequirement = 0.0f,
            WaterRequirement = 0.0f,
            ProductionCost = 0.05f,
            MaintenanceCost = 0.1f,
        };
        rPower.Outputs.Add(new Amount(ResourceIds.Power, 480.0f));
        rezepte.Add(rPower);

        var rWater = new RecipeDef(RecipeIds.WaterProduction, 60.0f)
        {
            DisplayName = "Wasserfoerderung",
            PowerRequirement = 0.0f,
            WaterRequirement = 0.0f,
            ProductionCost = 0.05f,
            MaintenanceCost = 0.1f,
        };
        rWater.Outputs.Add(new Amount(ResourceIds.Water, 480.0f));
        rezepte.Add(rWater);

        var rGrain = new RecipeDef("grain_production", 60.0f)
        {
            DisplayName = "Getreideanbau",
            ProductionCost = 0.2f,
            MaintenanceCost = 1.0f,
            PowerRequirement = 1.0f,
            WaterRequirement = 4.0f,
        };
        rGrain.Outputs.Add(new Amount(ResourceIds.Grain, 120.0f));
        rezepte.Add(rGrain);

        var rPig = new RecipeDef("pig_production", 60.0f)
        {
            DisplayName = "Schweineproduktion",
            ProductionCost = 0.6f,
            MaintenanceCost = 2.5f,
            PowerRequirement = 4.0f,
            WaterRequirement = 4.0f,
        };
        rPig.Outputs.Add(new Amount(ResourceIds.Pig, 30.0f));
        rezepte.Add(rPig);

        return rezepte;
    }
}



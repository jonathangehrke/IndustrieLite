// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Repository fuer Rezepte inklusive Verknuepfung zu Gebaeuden.
/// </summary>
public sealed class RecipeRepository : BaseRepository<RecipeDef>, IRecipeRepository
{
    private readonly Func<IBuildingRepository?> buildingRepositoryProvider;

    public RecipeRepository(Func<IBuildingRepository?> buildingRepositoryProvider, Func<bool> legacyErlaubt)
    {
        this.buildingRepositoryProvider = buildingRepositoryProvider ?? (() => null);
        ladeReihenfolge.Add(new DataIndexRecipeLoader());
        ladeReihenfolge.Add(new FileSystemRecipeLoader());
        ladeReihenfolge.Add(new LegacyRecipeLoader(legacyErlaubt));
    }

    protected override string GetId(RecipeDef item) => item.Id;

    public IReadOnlyCollection<RecipeDef> GetForBuilding(string buildingId)
    {
        if (string.IsNullOrWhiteSpace(buildingId))
        {
            return Array.Empty<RecipeDef>();
        }

        var buildingRepo = buildingRepositoryProvider();
        if (buildingRepo == null)
        {
            return Array.Empty<RecipeDef>();
        }

        var lookup = buildingRepo.TryGet(buildingId);
        var gebaeude = lookup.Ok ? lookup.Value : buildingRepo.GetById(buildingId);
        if (gebaeude == null)
        {
            return Array.Empty<RecipeDef>();
        }

        var result = new List<RecipeDef>();
        foreach (var recipeId in gebaeude.AvailableRecipes)
        {
            if (eintraegeNachId.TryGetValue(recipeId, out var recipe))
            {
                result.Add(recipe);
            }
        }

        if (result.Count == 0 && !string.IsNullOrEmpty(gebaeude.DefaultRecipeId) && eintraegeNachId.TryGetValue(gebaeude.DefaultRecipeId, out var defaultRecipe))
        {
            result.Add(defaultRecipe);
        }

        return result;
    }
}



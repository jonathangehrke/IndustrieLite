// SPDX-License-Identifier: MIT
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Zentrales Datenbanksystem basierend auf modularen Repositories.
/// </summary>
public partial class GameDatabase : Node, IGameDatabase
{
    /// <inheritdoc/>
    [Export]
    public bool AllowLegacyFallbackInRelease { get; set; }

    /// <inheritdoc/>
    public IBuildingRepository Buildings { get; private set; } = default!;

    /// <inheritdoc/>
    public IResourceRepository Resources { get; private set; } = default!;

    /// <inheritdoc/>
    public IRecipeRepository Recipes { get; private set; } = default!;

    /// <inheritdoc/>
    public bool IsInitialized { get; private set; }

    private Task? initialisierungTask;

    /// <inheritdoc/>
    public override void _Ready()
    {
        var legacyFlag = new Func<bool>(() => this.AllowLegacyFallbackInRelease);

        this.Buildings = new BuildingRepository(legacyFlag);
        this.Resources = new ResourceRepository(legacyFlag);
        this.Recipes = new RecipeRepository(() => this.Buildings, legacyFlag);

        ServiceContainer.Instance?.RegisterNamedService("GameDatabase", this);
        // Typed-Registration entfernt (Autoload registriert sich nur Named)
        this.CallDeferred(nameof(this.StartInitialisierung));
    }

    private async void StartInitialisierung()
    {
        await this.InitializeAsync();
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        if (this.initialisierungTask != null)
        {
            await this.initialisierungTask;
            return;
        }

        this.initialisierungTask = this.LadeDatenAsync();
        await this.initialisierungTask;
    }

    private async Task LadeDatenAsync()
    {
        var tree = this.GetTree();
        if (tree == null)
        {
            DebugLogger.LogServices("GameDatabase: SceneTree nicht verfuegbar");
            return;
        }

        await Task.WhenAll(
            this.Buildings.LoadDataAsync(tree),
            this.Resources.LoadDataAsync(tree),
            this.Recipes.LoadDataAsync(tree));

        this.IsInitialized = true;
        DebugLogger.LogServices(() =>
            $"GameDatabase: Initialisiert - {this.Buildings.GetAll().Count} Gebaeude, {this.Resources.GetAll().Count} Ressourcen, {this.Recipes.GetAll().Count} Rezepte");

        // Run data validation after loading
        this.ValidateData();
    }

    /// <summary>
    /// Validates all loaded data and logs results.
    /// Called automatically after data loading.
    /// </summary>
    private void ValidateData()
    {
        DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
            () => "=== Starting Data Validation ===");

        // Validate Resources first (needed for Recipe/Building validation)
        var resourceResults = DataValidator.ValidateAllResources(this.Resources);
        DataValidator.LogValidationResults("Resource", resourceResults);

        // Validate Recipes (needs Resources)
        var recipeResults = RecipeValidator.ValidateAllRecipes(this.Recipes, this.Resources);
        RecipeValidator.LogValidationResults(recipeResults);

        // Validate Buildings (needs Recipes)
        var buildingResults = DataValidator.ValidateAllBuildings(this.Buildings, this.Recipes);
        DataValidator.LogValidationResults("Building", buildingResults);

        // Summary
        int totalErrors = resourceResults.Sum(r => r.Value.Errors.Count) +
                         recipeResults.Sum(r => r.Value.Errors.Count) +
                         buildingResults.Sum(r => r.Value.Errors.Count);

        int totalWarnings = resourceResults.Sum(r => r.Value.Warnings.Count) +
                           recipeResults.Sum(r => r.Value.Warnings.Count) +
                           buildingResults.Sum(r => r.Value.Warnings.Count);

        if (totalErrors > 0)
        {
            DebugLogger.Log("data_validation", DebugLogger.LogLevel.Error,
                () => $"=== Data Validation FAILED: {totalErrors} error(s), {totalWarnings} warning(s) ===");
        }
        else if (totalWarnings > 0)
        {
            DebugLogger.Log("data_validation", DebugLogger.LogLevel.Warn,
                () => $"=== Data Validation PASSED with {totalWarnings} warning(s) ===");
        }
        else
        {
            DebugLogger.Log("data_validation", DebugLogger.LogLevel.Info,
                () => "=== Data Validation PASSED: No errors or warnings ===");
        }
    }
}



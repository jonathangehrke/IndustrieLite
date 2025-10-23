// SPDX-License-Identifier: MIT
using System;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Zentrales Datenbanksystem basierend auf modularen Repositories.
/// </summary>
public partial class GameDatabase : Node, IGameDatabase
{
    [Export]
    public bool AllowLegacyFallbackInRelease { get; set; }

    public IBuildingRepository Buildings { get; private set; } = default!;

    public IResourceRepository Resources { get; private set; } = default!;

    public IRecipeRepository Recipes { get; private set; } = default!;

    public bool IsInitialized { get; private set; }

    private Task? initialisierungTask;

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
    }
}



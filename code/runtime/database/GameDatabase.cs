// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Threading.Tasks;

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
        var legacyFlag = new Func<bool>(() => AllowLegacyFallbackInRelease);

        Buildings = new BuildingRepository(legacyFlag);
        Resources = new ResourceRepository(legacyFlag);
        Recipes = new RecipeRepository(() => Buildings, legacyFlag);

        ServiceContainer.Instance?.RegisterNamedService("GameDatabase", this);
        // Typed-Registration entfernt (Autoload registriert sich nur Named)

        CallDeferred(nameof(StartInitialisierung));
    }

    private async void StartInitialisierung()
    {
        await InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        if (initialisierungTask != null)
        {
            await initialisierungTask;
            return;
        }

        initialisierungTask = LadeDatenAsync();
        await initialisierungTask;
    }

    private async Task LadeDatenAsync()
    {
        var tree = GetTree();
        if (tree == null)
        {
            DebugLogger.LogServices("GameDatabase: SceneTree nicht verfuegbar");
            return;
        }

        await Task.WhenAll(
            Buildings.LoadDataAsync(tree),
            Resources.LoadDataAsync(tree),
            Recipes.LoadDataAsync(tree)
        );

        IsInitialized = true;
        DebugLogger.LogServices(() =>
            $"GameDatabase: Initialisiert - {Buildings.GetAll().Count} Gebaeude, {Resources.GetAll().Count} Ressourcen, {Recipes.GetAll().Count} Rezepte");
    }
}



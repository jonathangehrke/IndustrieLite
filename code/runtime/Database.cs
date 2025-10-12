// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Legacy-Kompatibilitaetslayer fuer bestehende Aufrufer der Database-API.
/// </summary>
#pragma warning disable CA1050
public partial class Database : DatabaseLegacyAdapter, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Singleton;
    [Export]
    public bool AllowLegacyFallbackInRelease
    {
        get => LegacyFallbackErlaubt;
        set => LegacyFallbackErlaubt = value;
    }

    private GameDatabase? gameDatabase;

    public override void _Ready()
    {
        ServiceContainer.Instance?.RegisterNamedService("Database", this);
        ServiceContainer.Instance?.RegisterNamedService("DatabaseLegacy", this);
        // Typed-Registration entfernt (Autoload registriert sich nur Named)

        gameDatabase = new GameDatabase
        {
            AllowLegacyFallbackInRelease = LegacyFallbackErlaubt,
            Name = nameof(GameDatabase)
        };

        AddChild(gameDatabase);
        VerbindeMitGameDatabase(gameDatabase);

        CallDeferred(nameof(StartInitialisierung));
    }

    private async void StartInitialisierung()
    {
        if (gameDatabase == null)
        {
            return;
        }

        await gameDatabase.InitializeAsync();
        LogMigrationStatus();
    }
}
#pragma warning restore CA1050



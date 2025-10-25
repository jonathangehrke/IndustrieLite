// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Legacy-Kompatibilitaetslayer fuer bestehende Aufrufer der Database-API.
/// </summary>
#pragma warning disable CA1050
public partial class Database : DatabaseLegacyAdapter, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Singleton;

    [Export]
    public bool AllowLegacyFallbackInRelease
    {
        get => this.LegacyFallbackErlaubt;
        set => this.LegacyFallbackErlaubt = value;
    }

    private GameDatabase? gameDatabase;

    /// <inheritdoc/>
    public override void _Ready()
    {
        ServiceContainer.Instance?.RegisterNamedService("Database", this);
        ServiceContainer.Instance?.RegisterNamedService("DatabaseLegacy", this);
        // Typed-Registration entfernt (Autoload registriert sich nur Named)
        this.gameDatabase = new GameDatabase
        {
            AllowLegacyFallbackInRelease = this.LegacyFallbackErlaubt,
            Name = nameof(GameDatabase),
        };

        this.AddChild(this.gameDatabase);
        this.VerbindeMitGameDatabase(this.gameDatabase);

        this.CallDeferred(nameof(this.StartInitialisierung));
    }

    private async void StartInitialisierung()
    {
        if (this.gameDatabase == null)
        {
            return;
        }

        await this.gameDatabase.InitializeAsync();
        this.LogMigrationStatus();
    }
}
#pragma warning restore CA1050



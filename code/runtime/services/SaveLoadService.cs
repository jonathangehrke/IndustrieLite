// SPDX-License-Identifier: MIT
using System.Threading;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Fassade fuer Save/Load-Ablauf. Delegiert an SaveManager / LoadManager / Validator.
/// </summary>
public partial class SaveLoadService : Node
{
    private ServiceContainer? serviceContainer;
    private SaveManager saveManager = default!;
    private LoadManager loadManager = default!;
    private readonly SaveDataValidator validator = new SaveDataValidator();
    private bool registered;

    public SaveLoadService()
    {
        this.Initialisiere(ServiceContainer.Instance);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        this.Initialisiere(ServiceContainer.Instance);
        this.RegistriereBeimServiceContainer();
    }

    private void Initialisiere(ServiceContainer? container)
    {
        this.serviceContainer = container ?? ServiceContainer.Instance;
        this.saveManager = new SaveManager(this.serviceContainer);
        this.loadManager = new LoadManager(this.serviceContainer);
    }

    private void RegistriereBeimServiceContainer()
    {
        if (this.registered)
        {
            return;
        }

        var container = this.serviceContainer ?? ServiceContainer.Instance;
        if (container == null)
        {
            return;
        }

        container.RegisterNamedService(nameof(SaveLoadService), this);
        // Typed-Registration entfernt (nur Named)
        this.registered = true;
    }

    public void SaveGame(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, TransportManager? transport = null)
    {
        // Synchrone API bleibt aus Kompatibilitaetsgruenden bestehen
        this.saveManager.SaveGame(fileName, land, buildings, economy, transport);
    }

    public async Task SaveGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, TransportManager? transport = null)
    {
        // Echte asynchrone Implementierung (kein Task.Run)
        await this.saveManager.SaveGameAsync(fileName, land, buildings, economy, transport).ConfigureAwait(false);
    }

    public async Task SaveGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, CancellationToken cancellationToken, TransportManager? transport = null)
    {
        await this.saveManager.SaveGameAsync(fileName, land, buildings, economy, transport, cancellationToken).ConfigureAwait(false);
    }

    public void LoadGame(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, TransportManager? transport = null)
    {
        // Synchrone API bleibt aus Kompatibilitaetsgruenden bestehen
        this.loadManager.LoadGame(fileName, land, buildings, economy, production, map, transport);
    }

    public async Task LoadGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, TransportManager? transport = null)
    {
        // Echte asynchrone Implementierung (kein Task.Run)
        await this.loadManager.LoadGameAsync(fileName, land, buildings, economy, production, map, transport).ConfigureAwait(false);
    }

    public async Task LoadGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, CancellationToken cancellationToken, TransportManager? transport = null)
    {
        await this.loadManager.LoadGameAsync(fileName, land, buildings, economy, production, map, transport, cancellationToken).ConfigureAwait(false);
    }

    public SaveData LoadFromFile(string fileName)
    {
        return this.loadManager.LoadFromFile(fileName);
    }

    public bool ValidateSchema(SaveData data, out string errorMessage)
    {
        return this.validator.ValidateSchema(data, out errorMessage);
    }

    public void ValidateFileIntegrity(string filePath)
    {
        this.validator.ValidateFileIntegrity(filePath);
    }

    public bool RoundTripSemanticsEqual(LandManager land, BuildingManager buildings, EconomyManager economy, out string diffInfo)
    {
        return this.validator.RoundTripSemanticsEqual(land, buildings, economy, out diffInfo);
    }
}

// SPDX-License-Identifier: MIT
using Godot;
using System.Threading.Tasks;
using System.Threading;

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
        Initialisiere(ServiceContainer.Instance);
    }

    public override void _Ready()
    {
        Initialisiere(ServiceContainer.Instance);
        RegistriereBeimServiceContainer();
    }

    private void Initialisiere(ServiceContainer? container)
    {
        serviceContainer = container ?? ServiceContainer.Instance;
        saveManager = new SaveManager(serviceContainer);
        loadManager = new LoadManager(serviceContainer);
    }

    private void RegistriereBeimServiceContainer()
    {
        if (registered)
        {
            return;
        }

        var container = serviceContainer ?? ServiceContainer.Instance;
        if (container == null)
        {
            return;
        }

        container.RegisterNamedService(nameof(SaveLoadService), this);
        // Typed-Registration entfernt (nur Named)
        registered = true;
    }

    public void SaveGame(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, TransportManager? transport = null)
    {
        // Synchrone API bleibt aus Kompatibilitaetsgruenden bestehen
        saveManager.SaveGame(fileName, land, buildings, economy, transport);
    }

    public async Task SaveGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, TransportManager? transport = null)
    {
        // Echte asynchrone Implementierung (kein Task.Run)
        await saveManager.SaveGameAsync(fileName, land, buildings, economy, transport).ConfigureAwait(false);
    }

    public async Task SaveGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, CancellationToken cancellationToken, TransportManager? transport = null)
    {
        await saveManager.SaveGameAsync(fileName, land, buildings, economy, transport, cancellationToken).ConfigureAwait(false);
    }

    public void LoadGame(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, TransportManager? transport = null)
    {
        // Synchrone API bleibt aus Kompatibilitaetsgruenden bestehen
        loadManager.LoadGame(fileName, land, buildings, economy, production, map, transport);
    }

    public async Task LoadGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, TransportManager? transport = null)
    {
        // Echte asynchrone Implementierung (kein Task.Run)
        await loadManager.LoadGameAsync(fileName, land, buildings, economy, production, map, transport).ConfigureAwait(false);
    }

    public async Task LoadGameAsync(string fileName, LandManager land, BuildingManager buildings, EconomyManager economy, ProductionManager? production, Map? map, CancellationToken cancellationToken, TransportManager? transport = null)
    {
        await loadManager.LoadGameAsync(fileName, land, buildings, economy, production, map, transport, cancellationToken).ConfigureAwait(false);
    }

    public SaveData LoadFromFile(string fileName)
    {
        return loadManager.LoadFromFile(fileName);
    }

    public bool ValidateSchema(SaveData data, out string errorMessage)
    {
        return validator.ValidateSchema(data, out errorMessage);
    }

    public void ValidateFileIntegrity(string filePath)
    {
        validator.ValidateFileIntegrity(filePath);
    }

    public bool RoundTripSemanticsEqual(LandManager land, BuildingManager buildings, EconomyManager economy, out string diffInfo)
    {
        return validator.RoundTripSemanticsEqual(land, buildings, economy, out diffInfo);
    }
}

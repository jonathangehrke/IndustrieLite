// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

/// <summary>
/// Zentrale Koordination fuer UI-Anfragen und Manager-zu-Manager Interaktionen.
/// </summary>
public partial class ManagerCoordinator : Node
{
    private GameManager? _gameManager;
    private LandManager? _landManager;
    private BuildingManager? _buildingManager;
    private TransportManager? _transportManager;
    private EconomyManager? _economyManager;
    private InputManager? _inputManager;
    private ResourceManager? _resourceManager;
    private ProductionManager? _productionManager;
    private GameClockManager? _gameClockManager;
    private GameLifecycleManager? _lifecycleManager;
    private Node? _devFlags;  // Injected dependency - no more Service Locator

    /// <summary>
    /// Aktualisiert interne Referenzen basierend auf dem aktuellen GameManager.
    /// </summary>
    public void AktualisiereReferenzen(GameManager gameManager, Node? devFlags = null)
    {
        _gameManager = gameManager;
        _landManager = gameManager.LandManager;
        _buildingManager = gameManager.BuildingManager;
        _transportManager = gameManager.TransportManager;
        _economyManager = gameManager.EconomyManager;
        _inputManager = gameManager.InputManager;
        _resourceManager = gameManager.ResourceManager;
        _productionManager = gameManager.ProductionManager;
        _gameClockManager = gameManager.GameClockManager;
        _lifecycleManager = gameManager.GetNodeOrNull<GameLifecycleManager>("GameLifecycleManager");
        _devFlags = devFlags;  // Store injected DevFlags
    }

    private bool IstInitialisiert()
    {
        return _landManager != null && _buildingManager != null && _transportManager != null &&
               _economyManager != null && _inputManager != null && _productionManager != null &&
               _gameClockManager != null;
    }

    public double GetMoney()
    {
        return _economyManager?.GetMoney() ?? 0.0;
    }

    public void ToggleBuyLandMode(bool enabled)
    {
        _inputManager?.SetMode(enabled ? InputManager.InputMode.BuyLand : InputManager.InputMode.None);
    }

    public void ToggleSellLandMode(bool enabled)
    {
        _inputManager?.SetMode(enabled ? InputManager.InputMode.SellLand : InputManager.InputMode.None);
    }

    public void SetBuildMode(string type)
    {
        if (_inputManager == null)
        {
            return;
        }
        _inputManager.SetMode(InputManager.InputMode.Build, type);
    }

    public void ToggleTransportMode(bool enabled)
    {
        _inputManager?.SetMode(enabled ? InputManager.InputMode.Transport : InputManager.InputMode.None);
    }

    public void ToggleDemolishMode(bool enabled)
    {
        _inputManager?.SetMode(enabled ? InputManager.InputMode.Demolish : InputManager.InputMode.None);
    }

    public bool IsBuyLandModeActive()
    {
        return _inputManager?.CurrentMode == InputManager.InputMode.BuyLand;
    }

    public bool IsSellLandModeActive()
    {
        return _inputManager?.CurrentMode == InputManager.InputMode.SellLand;
    }

    public bool IsTransportModeActive()
    {
        return _inputManager?.CurrentMode == InputManager.InputMode.Transport;
    }

    public void HandleClick(Vector2I cell)
    {
        _inputManager?.HandleClick(cell);
    }

    public bool CanBuyLand(Vector2I cell)
    {
        if (_landManager == null || _economyManager == null)
        {
            return false;
        }
        return _landManager.CanBuyLand(cell, _economyManager.GetMoney());
    }

    public bool CanSellLand(Vector2I cell)
    {
        if (_landManager == null || _buildingManager == null)
        {
            return false;
        }
        return _landManager.CanSellLand(cell, _buildingManager);
    }

    public bool IsOwned(Vector2I cell)
    {
        return _landManager != null && _landManager.IsOwned(cell);
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders()
    {
        return _transportManager?.GetOrders() ?? new Godot.Collections.Array<Godot.Collections.Dictionary>();
    }

    public void AcceptOrder(int id)
    {
        _transportManager?.AcceptOrder(id);
    }

    public List<ChickenFarm> GetChickenFarms()
    {
        return _buildingManager?.GetChickenFarms() ?? new List<ChickenFarm>();
    }

    public Godot.Collections.Array<ChickenFarm> GetChickenFarmsForUI()
    {
        return _buildingManager?.GetChickenFarmsForUI() ?? new Godot.Collections.Array<ChickenFarm>();
    }

    public int GetTotalChickens()
    {
        if (_buildingManager == null)
        {
            return 0;
        }
        int total = 0;
        foreach (var farm in _buildingManager.GetChickenFarms())
        {
            total += farm.Stock;
        }
        return total;
    }

    public void SetGameTimeScale(double scale)
    {
        _gameClockManager?.SetTimeScale(scale);
    }

    public void ToggleGamePause()
    {
        _gameClockManager?.TogglePause();
    }

    public bool IsGamePaused()
    {
        return _gameClockManager != null && _gameClockManager.IsPaused;
    }

    public void SetProductionTickRate(double rate)
    {
        _productionManager?.SetProduktionsTickRate(rate);
    }

    public double GetProductionTickRate()
    {
        return _productionManager != null ? _productionManager.ProduktionsTickRate : 0.0;
    }

    public void PauseGame(bool pause)
    {
        if (_gameManager?.GetTree() == null)
        {
            return;
        }
        _gameManager.GetTree().Paused = pause;
        DebugLogger.LogServices(pause ? "Game paused" : "Game resumed");
    }

    public void NewGame()
    {
        if (!IstInitialisiert())
        {
            DebugLogger.LogLifecycle("ManagerCoordinator: Services noch nicht bereit, NewGame wird uebersprungen.");
            return;
        }
        _lifecycleManager?.NewGame();
    }

    public void SaveGame(string fileName = "savegame.json")
    {
        _lifecycleManager?.SaveGame(fileName);
    }

    public void LoadGame(string fileName = "savegame.json")
    {
        _lifecycleManager?.LoadGame(fileName);
    }

    public System.Threading.Tasks.Task SaveGameAsync(string fileName = "savegame.json")
    {
        return _lifecycleManager != null ? _lifecycleManager.SaveGameAsync(fileName) : System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task SaveGameAsync(string fileName, System.Threading.CancellationToken cancellationToken)
    {
        return _lifecycleManager != null ? _lifecycleManager.SaveGameAsync(fileName, cancellationToken) : System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task LoadGameAsync(string fileName = "savegame.json")
    {
        return _lifecycleManager != null ? _lifecycleManager.LoadGameAsync(fileName) : System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task LoadGameAsync(string fileName, System.Threading.CancellationToken cancellationToken)
    {
        return _lifecycleManager != null ? _lifecycleManager.LoadGameAsync(fileName, cancellationToken) : System.Threading.Tasks.Task.CompletedTask;
    }

    public void SetLogLevel(int level)
    {
        int min = (int)DebugLogger.LogLevel.Trace;
        int max = (int)DebugLogger.LogLevel.Error;
        if (level < min) level = min;
        if (level > max) level = max;
        DebugLogger.SetMinLevel((DebugLogger.LogLevel)level);
    }

    public void EnableAllDebugLogs(bool enable)
    {
        if (_devFlags == null)
        {
            return;
        }
        try
        {
            _devFlags.Set("debug_all", enable);
        }
        catch
        {
        }
    }

    public void SetDebugFlag(string flagName, bool enable)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return;
        }
        if (_devFlags == null)
        {
            return;
        }
        try
        {
            _devFlags.Set(flagName, enable);
        }
        catch
        {
        }
    }
}

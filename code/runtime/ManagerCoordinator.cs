// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Zentrale Koordination fuer UI-Anfragen und Manager-zu-Manager Interaktionen.
/// </summary>
public partial class ManagerCoordinator : Node
{
    private GameManager? gameManager;
    private LandManager? landManager;
    private BuildingManager? buildingManager;
    private TransportManager? transportManager;
    private EconomyManager? economyManager;
    private InputManager? inputManager;
    private ResourceManager? resourceManager;
    private ProductionManager? productionManager;
    private GameClockManager? gameClockManager;
    private GameLifecycleManager? lifecycleManager;
    private Node? devFlags;  // Injected dependency - no more Service Locator

    /// <summary>
    /// Aktualisiert interne Referenzen basierend auf dem aktuellen GameManager.
    /// </summary>
    public void AktualisiereReferenzen(GameManager gameManager, Node? devFlags = null)
    {
        this.gameManager = gameManager;
        this.landManager = gameManager.LandManager;
        this.buildingManager = gameManager.BuildingManager;
        this.transportManager = gameManager.TransportManager;
        this.economyManager = gameManager.EconomyManager;
        this.inputManager = gameManager.InputManager;
        this.resourceManager = gameManager.ResourceManager;
        this.productionManager = gameManager.ProductionManager;
        this.gameClockManager = gameManager.GameClockManager;
        this.lifecycleManager = gameManager.GetNodeOrNull<GameLifecycleManager>("GameLifecycleManager");
        this.devFlags = devFlags;  // Store injected DevFlags
    }

    private bool IstInitialisiert()
    {
        return this.landManager != null && this.buildingManager != null && this.transportManager != null &&
               this.economyManager != null && this.inputManager != null && this.productionManager != null &&
               this.gameClockManager != null;
    }

    public double GetMoney()
    {
        return this.economyManager?.GetMoney() ?? 0.0;
    }

    public void ToggleBuyLandMode(bool enabled)
    {
        this.inputManager?.SetMode(enabled ? InputManager.InputMode.BuyLand : InputManager.InputMode.None);
    }

    public void ToggleSellLandMode(bool enabled)
    {
        this.inputManager?.SetMode(enabled ? InputManager.InputMode.SellLand : InputManager.InputMode.None);
    }

    public void SetBuildMode(string type)
    {
        if (this.inputManager == null)
        {
            return;
        }
        this.inputManager.SetMode(InputManager.InputMode.Build, type);
    }

    public void ToggleTransportMode(bool enabled)
    {
        this.inputManager?.SetMode(enabled ? InputManager.InputMode.Transport : InputManager.InputMode.None);
    }

    public void ToggleDemolishMode(bool enabled)
    {
        this.inputManager?.SetMode(enabled ? InputManager.InputMode.Demolish : InputManager.InputMode.None);
    }

    public bool IsBuyLandModeActive()
    {
        return this.inputManager?.CurrentMode == InputManager.InputMode.BuyLand;
    }

    public bool IsSellLandModeActive()
    {
        return this.inputManager?.CurrentMode == InputManager.InputMode.SellLand;
    }

    public bool IsTransportModeActive()
    {
        return this.inputManager?.CurrentMode == InputManager.InputMode.Transport;
    }

    public void HandleClick(Vector2I cell)
    {
        this.inputManager?.HandleClick(cell);
    }

    public bool CanBuyLand(Vector2I cell)
    {
        if (this.landManager == null || this.economyManager == null)
        {
            return false;
        }
        return this.landManager.CanBuyLand(cell, this.economyManager.GetMoney());
    }

    public bool CanSellLand(Vector2I cell)
    {
        if (this.landManager == null || this.buildingManager == null)
        {
            return false;
        }
        return this.landManager.CanSellLand(cell, this.buildingManager);
    }

    public bool IsOwned(Vector2I cell)
    {
        return this.landManager != null && this.landManager.IsOwned(cell);
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetOrders()
    {
        return this.transportManager?.GetOrders() ?? new Godot.Collections.Array<Godot.Collections.Dictionary>();
    }

    public void AcceptOrder(int id)
    {
        this.transportManager?.AcceptOrder(id);
    }

    [System.Obsolete("Use GetProductionBuildings() instead")]
    public List<Building> GetChickenFarms()
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        return this.buildingManager?.GetChickenFarms() ?? new List<Building>();
        #pragma warning restore CS0618
    }

    [System.Obsolete("Use GetProductionBuildingsForUI() instead")]
    public Godot.Collections.Array<Building> GetChickenFarmsForUI()
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        return this.buildingManager?.GetChickenFarmsForUI() ?? new Godot.Collections.Array<Building>();
        #pragma warning restore CS0618
    }

    public List<IProductionBuilding> GetProductionBuildings()
    {
        return this.buildingManager?.GetProductionBuildings() ?? new List<IProductionBuilding>();
    }

    public Godot.Collections.Array<Building> GetProductionBuildingsForUI()
    {
        return this.buildingManager?.GetProductionBuildingsForUI() ?? new Godot.Collections.Array<Building>();
    }

    public int GetTotalChickens()
    {
        if (this.buildingManager == null)
        {
            return 0;
        }
        // Use BuildingManager's inventory totals instead of direct farm access
        return this.buildingManager.GetTotalInventoryOfResource(new StringName("chickens"));
    }

    public void SetGameTimeScale(double scale)
    {
        this.gameClockManager?.SetTimeScale(scale);
    }

    public void ToggleGamePause()
    {
        this.gameClockManager?.TogglePause();
    }

    public bool IsGamePaused()
    {
        return this.gameClockManager != null && this.gameClockManager.IsPaused;
    }

    public void SetProductionTickRate(double rate)
    {
        this.productionManager?.SetProduktionsTickRate(rate);
    }

    public double GetProductionTickRate()
    {
        return this.productionManager != null ? this.productionManager.ProduktionsTickRate : 0.0;
    }

    public void PauseGame(bool pause)
    {
        if (this.gameManager?.GetTree() == null)
        {
            return;
        }
        this.gameManager.GetTree().Paused = pause;
        DebugLogger.LogServices(pause ? "Game paused" : "Game resumed");
    }

    public void NewGame()
    {
        if (!this.IstInitialisiert())
        {
            DebugLogger.LogLifecycle("ManagerCoordinator: Services noch nicht bereit, NewGame wird uebersprungen.");
            return;
        }
        this.lifecycleManager?.NewGame();
    }

    public void SaveGame(string fileName = "savegame.json")
    {
        this.lifecycleManager?.SaveGame(fileName);
    }

    public void LoadGame(string fileName = "savegame.json")
    {
        this.lifecycleManager?.LoadGame(fileName);
    }

    public System.Threading.Tasks.Task SaveGameAsync(string fileName = "savegame.json")
    {
        return this.lifecycleManager != null ? this.lifecycleManager.SaveGameAsync(fileName) : System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task SaveGameAsync(string fileName, System.Threading.CancellationToken cancellationToken)
    {
        return this.lifecycleManager != null ? this.lifecycleManager.SaveGameAsync(fileName, cancellationToken) : System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task LoadGameAsync(string fileName = "savegame.json")
    {
        return this.lifecycleManager != null ? this.lifecycleManager.LoadGameAsync(fileName) : System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task LoadGameAsync(string fileName, System.Threading.CancellationToken cancellationToken)
    {
        return this.lifecycleManager != null ? this.lifecycleManager.LoadGameAsync(fileName, cancellationToken) : System.Threading.Tasks.Task.CompletedTask;
    }

    public void SetLogLevel(int level)
    {
        int min = (int)DebugLogger.LogLevel.Trace;
        int max = (int)DebugLogger.LogLevel.Error;
        if (level < min)
        {
            level = min;
        }

        if (level > max)
        {
            level = max;
        }

        DebugLogger.SetMinLevel((DebugLogger.LogLevel)level);
    }

    public void EnableAllDebugLogs(bool enable)
    {
        if (this.devFlags == null)
        {
            return;
        }
        try
        {
            this.devFlags.Set("debug_all", enable);
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
        if (this.devFlags == null)
        {
            return;
        }
        try
        {
            this.devFlags.Set(flagName, enable);
        }
        catch
        {
        }
    }
}

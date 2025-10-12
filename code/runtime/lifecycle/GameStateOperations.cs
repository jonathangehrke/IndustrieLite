// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Helper-Klasse für Game-State-Operationen im GameLifecycleManager.
/// Kapselt NewGame, SaveGame, LoadGame Logik ohne Node-Dependencies.
/// </summary>
internal class GameStateOperations
{
    private readonly Node ownerNode;

    public GameStateOperations(Node ownerNode)
    {
        this.ownerNode = ownerNode ?? throw new ArgumentNullException(nameof(ownerNode));
    }

    /// <summary>
    /// Start a new game with default settings
    /// </summary>
    public void ExecuteNewGame(ServiceResolver.ServiceReferences services)
    {
        DebugLogger.LogLifecycle("GameStateOperations: Starting new game");

        // Reset economy to starting money
        if (services.EconomyManager != null)
        {
            services.EconomyManager.SetMoney(services.EconomyManager.StartingMoney);
        }

        // Clear existing game state
        ClearGameState(services);

        // Initialize new game state
        InitializeNewGame(services);

        // GameTime zurücksetzen auf Startdatum
        var scGtm = ServiceContainer.Instance;
        GameTimeManager? gtm = null;
        if (scGtm != null)
            scGtm.TryGetNamedService<GameTimeManager>("GameTimeManager", out gtm);
        gtm?.ResetToStart();

        // LevelManager zurücksetzen (Level 1, Revenue 0)
        var sc = ServiceContainer.Instance;
        LevelManager? levelManager = null;
        if (sc != null)
            sc.TryGetNamedService<LevelManager>("LevelManager", out levelManager);
        if (levelManager != null)
        {
            levelManager.Reset();
            DebugLogger.LogLifecycle("GameStateOperations: LevelManager reset to Level 1");
        }

        // InputManager zurücksetzen (Mode auf None)
        InputManager? inputManager = null;
        if (sc != null)
            sc.TryGetNamedService<InputManager>("InputManager", out inputManager);
        if (inputManager != null)
        {
            inputManager.SetMode(InputManager.InputMode.None);
            DebugLogger.LogLifecycle("GameStateOperations: InputManager mode reset to None");
        }

        // CameraController auf Start-Position zurücksetzen
        CameraController? cameraController = null;
        if (sc != null)
            sc.TryGetNamedService<CameraController>("CameraController", out cameraController);
        if (cameraController != null && services.LandManager != null && services.BuildingManager != null)
        {
            int worldW = services.LandManager.GridW * services.BuildingManager.TileSize;
            int worldH = services.LandManager.GridH * services.BuildingManager.TileSize;
            cameraController.JumpToImmediate(new Vector2(worldW / 2f, worldH / 2f));
            cameraController.SetZoomImmediate(1.0f);
            DebugLogger.LogLifecycle("GameStateOperations: CameraController reset to center position");
        }

        // Emit event for UI updates via ServiceContainer (kein /root)
        var scEh = ServiceContainer.Instance;
        EventHub? eventHub = null;
        if (scEh != null)
            scEh.TryGetNamedService<EventHub>("EventHub", out eventHub);
        if (eventHub != null)
        {
            eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, services.EconomyManager?.GetMoney() ?? 0.0);
        }

        // Refresh map display
        if (services.Map != null)
        {
            services.Map.QueueRedraw();
        }

        DebugLogger.LogLifecycle("GameStateOperations: New game started successfully");
    }

    /// <summary>
    /// Save current game state to file
    /// </summary>
    public void ExecuteSaveGame(string filePath, ServiceResolver.ServiceReferences services)
    {
        if (services.SaveLoadService == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: SaveLoadService nicht bereit - SaveGame abgebrochen");
            return;
        }

        DebugLogger.LogLifecycle(() => $"GameStateOperations: Saving game to {filePath}");

        try
        {
            // Use existing SaveLoadService API
            services.SaveLoadService.SaveGame(filePath, services.LandManager!, services.BuildingManager!,
                                             services.EconomyManager!, services.TransportManager);
            DebugLogger.LogLifecycle("GameStateOperations: Game saved successfully");
        }
        catch (System.Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"GameStateOperations: Failed to save game - {ex.Message}");
        }
    }

    /// <summary>
    /// Save current game state to file (asynchron)
    /// </summary>
    public async Task ExecuteSaveGameAsync(string filePath, ServiceResolver.ServiceReferences services)
    {
        if (services.SaveLoadService == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: SaveLoadService nicht bereit - SaveGame abgebrochen");
            return;
        }

        DebugLogger.LogLifecycle(() => $"GameStateOperations: Saving game (async) to {filePath}");

        try
        {
            await services.SaveLoadService.SaveGameAsync(filePath, services.LandManager!, services.BuildingManager!,
                                                         services.EconomyManager!, services.TransportManager).ConfigureAwait(false);
            DebugLogger.LogLifecycle("GameStateOperations: Game saved successfully (async)");
        }
        catch (System.Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"GameStateOperations: Failed to save game (async) - {ex.Message}");
        }
    }

    public async Task ExecuteSaveGameAsync(string filePath, ServiceResolver.ServiceReferences services, System.Threading.CancellationToken cancellationToken)
    {
        if (services.SaveLoadService == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: SaveLoadService nicht bereit - SaveGame abgebrochen");
            return;
        }

        DebugLogger.LogLifecycle(() => $"GameStateOperations: Saving game (async) to {filePath}");

        try
        {
            await services.SaveLoadService.SaveGameAsync(filePath, services.LandManager!, services.BuildingManager!,
                                                         services.EconomyManager!, cancellationToken, services.TransportManager).ConfigureAwait(false);
            DebugLogger.LogLifecycle("GameStateOperations: Game saved successfully (async)");
        }
        catch (System.Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"GameStateOperations: Failed to save game (async) - {ex.Message}");
        }
    }

    /// <summary>
    /// Load game state from file
    /// </summary>
    public void ExecuteLoadGame(string filePath, ServiceResolver.ServiceReferences services)
    {
        DebugLogger.LogLifecycle(() => $"GameStateOperations: Loading game from {filePath}");

        if (services.SaveLoadService == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: SaveLoadService nicht bereit - LoadGame abgebrochen");
            return;
        }

        if (services.Map == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: Map nicht verfügbar - LoadGame abgebrochen");
            return;
        }

        try
        {
            // Use existing SaveLoadService API
            services.SaveLoadService.LoadGame(filePath, services.LandManager!, services.BuildingManager!,
                                            services.EconomyManager!, services.ProductionManager!,
                                            services.Map, services.TransportManager);

            // Immediate redraw
            services.Map.RequestRedraw();
            services.Map.QueueRedraw();

            // Deferred backup redraw for edge cases
            _ = RepaintDeferredAsync(services.Map);

            // Nach dem Laden Trucks zurücksetzen und Jobs neu starten
            services.TransportManager?.RestartPendingJobs();
            DebugLogger.LogLifecycle("GameStateOperations: Game loaded successfully");
        }
        catch (System.Exception ex)
        {
            var innerMessage = ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : string.Empty;
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"GameStateOperations: Failed to load game - {ex.Message}{innerMessage}");
            if (ex.InnerException != null)
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => ex.InnerException.ToString());
            }
        }
    }

    /// <summary>
    /// Load game state from file (asynchron)
    /// </summary>
    public async Task ExecuteLoadGameAsync(string filePath, ServiceResolver.ServiceReferences services)
    {
        DebugLogger.LogLifecycle(() => $"GameStateOperations: Loading game (async) from {filePath}");

        if (services.SaveLoadService == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: SaveLoadService nicht bereit - LoadGame abgebrochen");
            return;
        }

        if (services.Map == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: Map nicht verfuegbar - LoadGame abgebrochen");
            return;
        }

        try
        {
            await services.SaveLoadService.LoadGameAsync(filePath, services.LandManager!, services.BuildingManager!,
                                                        services.EconomyManager!, services.ProductionManager!,
                                                        services.Map, services.TransportManager).ConfigureAwait(false);

            services.Map.RequestRedraw();
            services.Map.QueueRedraw();

            _ = RepaintDeferredAsync(services.Map);
            services.TransportManager?.RestartPendingJobs();
            DebugLogger.LogLifecycle("GameStateOperations: Game loaded successfully (async)");
        }
        catch (System.Exception ex)
        {
            var innerMessage = ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : string.Empty;
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"GameStateOperations: Failed to load game (async) - {ex.Message}{innerMessage}");
            if (ex.InnerException != null)
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => ex.InnerException.ToString());
            }
        }
    }

    public async Task ExecuteLoadGameAsync(string filePath, ServiceResolver.ServiceReferences services, System.Threading.CancellationToken cancellationToken)
    {
        DebugLogger.LogLifecycle(() => $"GameStateOperations: Loading game (async) from {filePath}");

        if (services.SaveLoadService == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: SaveLoadService nicht bereit - LoadGame abgebrochen");
            return;
        }

        if (services.Map == null)
        {
            DebugLogger.LogLifecycle("GameStateOperations: Map nicht verfuegbar - LoadGame abgebrochen");
            return;
        }

        try
        {
            await services.SaveLoadService.LoadGameAsync(filePath, services.LandManager!, services.BuildingManager!,
                                                         services.EconomyManager!, services.ProductionManager!,
                                                         services.Map, cancellationToken, services.TransportManager).ConfigureAwait(false);

            services.Map.RequestRedraw();
            services.Map.QueueRedraw();

            _ = RepaintDeferredAsync(services.Map);
            services.TransportManager?.RestartPendingJobs();
            DebugLogger.LogLifecycle("GameStateOperations: Game loaded successfully (async)");
        }
        catch (System.Exception ex)
        {
            var innerMessage = ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : string.Empty;
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"GameStateOperations: Failed to load game (async) - {ex.Message}{innerMessage}");
            if (ex.InnerException != null)
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error, () => ex.InnerException.ToString());
            }
        }
    }

    /// <summary>
    /// Clear all existing game state
    /// </summary>
    private void ClearGameState(ServiceResolver.ServiceReferences services)
    {
        // Transport zuerst stoppen, um Ticks auf entsorgte Nodes zu vermeiden
        services.TransportManager?.ClearAllData();

        // Clear buildings (using existing API)
        if (services.BuildingManager != null)
        {
            var toRemove = new List<Building>(services.BuildingManager.Buildings);
            foreach (var building in toRemove)
            {
                if (building is IProducer producer)
                {
                    services.ProductionManager?.UnregisterProducer(producer);
                }
                building.QueueFree();
            }
            services.BuildingManager.Buildings.Clear();
            services.BuildingManager.Cities.Clear();
        }

        // Reset production and resource capacities
        services.ProductionManager?.ClearAllData();
        services.ResourceManager?.ClearAllData();

        // Reset cached totals nur wenn neue Produktion aktiv ist
        if (services.ProductionManager != null && services.ProductionManager.UseNewProduction)
        {
            var scPs = ServiceContainer.Instance;
            ProductionSystem? ps = null;
            if (scPs != null)
                scPs.TryGetNamedService<ProductionSystem>("ProductionSystem", out ps);
            if (ps != null)
            {
                ps.Reset();
            }
        }

        // Clear roads (using existing API)
        if (services.RoadManager != null)
        {
            services.RoadManager.ClearAllRoads();
        }

        // Reset land ownership (using existing API)
        if (services.LandManager != null)
        {
            services.LandManager.ResetAllLandFalse();
        }

        DebugLogger.LogLifecycle("GameStateOperations: Game state cleared");
    }

    /// <summary>
    /// Initialize new game with starting conditions
    /// </summary>
    private void InitializeNewGame(ServiceResolver.ServiceReferences services)
    {
        using (Simulation.EnterDeterministicTestScope())
        {
            // Place starting city
            PlaceStartingCity(services);

            // Set up initial land ownership around starting city
            SetupInitialLandOwnership(services);

            // Initialize starting resources
            InitializeStartingResources(services);
        }
    }

    /// <summary>
    /// Place the starting city in center of map
    /// </summary>
    private void PlaceStartingCity(ServiceResolver.ServiceReferences services)
    {
        if (services.LandManager == null || services.BuildingManager == null) return;

        int sx = services.LandManager.GridW / 2 - 5;
        int sy = services.LandManager.GridH / 2 - 4;
        var cityPosition = new Vector2I(sx + 12, sy + 2);

        using (Simulation.EnterDeterministicTestScope())
        {
            var placedCity = services.BuildingManager.PlaceBuilding("city", cityPosition);
            if (placedCity != null)
            {
                DebugLogger.LogLifecycle(() => $"GameStateOperations: Starting city placed at {cityPosition}");
            }
            else
            {
                DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                    () => $"GameStateOperations: FAILED to place starting city at {cityPosition}!");
            }
        }
    }

    /// <summary>
    /// Set up initial land ownership around starting city
    /// </summary>
    private void SetupInitialLandOwnership(ServiceResolver.ServiceReferences services)
    {
        if (services.LandManager == null) return;

        // Startgebiet inkl. Flag setzen (verkaufs-geschützt)
        services.LandManager.InitializeStartRegion();
        DebugLogger.LogLifecycle("GameStateOperations: Initial land ownership set up (StartRegion)");
    }

    /// <summary>
    /// Initialize starting resources
    /// </summary>
    private void InitializeStartingResources(ServiceResolver.ServiceReferences services)
    {
        if (services.ResourceManager == null) return;

        foreach (var pair in GameConstants.Startup.InitialResources)
        {
            services.ResourceManager.SetProduction(pair.Key, pair.Value);
            services.ResourceManager.GetResourceInfo(pair.Key).Available = pair.Value;
        }

        DebugLogger.LogLifecycle("GameStateOperations: Starting resources initialized (GameConstants.Startup.InitialResources)");
    }

    /// <summary>
    /// Deferred map redraw for edge cases where immediate redraw might not work
    /// Waits 2 frames before executing backup redraw
    /// </summary>
    private async System.Threading.Tasks.Task RepaintDeferredAsync(Map map)
    {
        try
        {
            // Wait 2 frames to ensure everything is properly initialized
            await ownerNode.ToSignal(ownerNode.GetTree(), SceneTree.SignalName.ProcessFrame); // Frame 1
            await ownerNode.ToSignal(ownerNode.GetTree(), SceneTree.SignalName.ProcessFrame); // Frame 2

            map.QueueRedraw();
            DebugLogger.LogLifecycle("GameStateOperations: Deferred map redraw executed successfully");
        }
        catch (System.Exception ex)
        {
            DebugLogger.Log("debug_lifecycle", DebugLogger.LogLevel.Error,
                () => $"GameStateOperations RepaintDeferredAsync failed: {ex.Message}");
        }
    }
}

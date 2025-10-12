// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Manages game lifecycle operations: New Game, Save, Load
/// Refactored to use helper classes for better separation of concerns
/// </summary>
public partial class GameLifecycleManager : Node
{
    // Helper classes (internal, no Node dependencies)
    private readonly ServiceResolver serviceResolver;
    private readonly GameStateOperations gameStateOps;
    private readonly DevFeatureSetup devFeatureSetup;

    // Current service references
    private ServiceResolver.ServiceReferences? currentServices;

    // Lifecycle-Flags (keeping existing behavior)
    private bool servicesReady = false;
    private bool initializeScheduled = false;
    private string? pendingLoadFilePath;
    private bool pendingNewGame = false;

    public GameLifecycleManager()
    {
        // Initialize helper classes
        serviceResolver = new ServiceResolver(this);
        gameStateOps = new GameStateOperations(this);
        devFeatureSetup = new DevFeatureSetup(this);
    }
    
    public override void _Ready()
    {
        ScheduleInitializeServices();

        // WORKAROUND: Start NewGame automatically after a short delay
        CallDeferred(nameof(AutoStartNewGame));
    }

    private void ScheduleInitializeServices()
    {
        if (initializeScheduled)
        {
            return;
        }

        initializeScheduled = true;
        CallDeferred(nameof(InitializeServices));
    }

    private void InitializeServices()
    {
        initializeScheduled = false;

        if (servicesReady)
        {
            return;
        }

        // Try to resolve all services using the helper
        currentServices = serviceResolver.TryResolveServices();
        if (currentServices == null)
        {
            DebugLogger.LogLifecycle("GameLifecycleManager: Services nicht bereit - erneuter Versuch");
            ScheduleInitializeServices();
            return;
        }

        servicesReady = true;
        DebugLogger.LogLifecycle("GameLifecycleManager: Services initialized via ServiceResolver");
        DebugLogger.LogLifecycle(() => $"GameLifecycleManager: All basic services ready: {currentServices.AreAllServicesReady()}");

        // Handle pending operations
        if (pendingNewGame)
        {
            pendingNewGame = false;
            CallDeferred(nameof(NewGame));
        }

        if (!string.IsNullOrEmpty(pendingLoadFilePath))
        {
            var file = pendingLoadFilePath;
            pendingLoadFilePath = null;
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: Verzoegertes LoadGame fuer {file} wird ausgefuehrt");
            CallDeferred(nameof(LoadGameInternal), file);
        }
    }
    
    private bool EnsureServicesReadyFor(string operation)
    {
        if (servicesReady && currentServices != null && currentServices.AreAllServicesReady())
        {
            return true;
        }

        InitializeServices();

        if (servicesReady && currentServices != null && currentServices.AreAllServicesReady())
        {
            return true;
        }

        ScheduleInitializeServices();
        DebugLogger.LogLifecycle(() => $"GameLifecycleManager: {operation} wartet auf Service-Initialisierung");
        return false;
    }

    /// <summary>
    /// Start a new game with default settings
    /// </summary>
    public void NewGame()
    {
        if (!EnsureServicesReadyFor("NewGame"))
        {
            pendingNewGame = true;
            return;
        }

        pendingNewGame = false;

        // Delegate to GameStateOperations helper
        gameStateOps.ExecuteNewGame(currentServices!);
    }
    
    /// <summary>
    /// Save current game state to file
    /// </summary>
    public void SaveGame(string filePath)
    {
        if (!EnsureServicesReadyFor("SaveGame"))
        {
            return;
        }

        // Delegate to GameStateOperations helper
        gameStateOps.ExecuteSaveGame(filePath, currentServices!);
    }
    
    /// <summary>
    /// Save current game state to file (asynchron)
    /// </summary>
    public async Task SaveGameAsync(string filePath)
    {
        if (!EnsureServicesReadyFor("SaveGameAsync"))
        {
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: SaveGameAsync wartet auf Service-Initialisierung");
            return;
        }

        await gameStateOps.ExecuteSaveGameAsync(filePath, currentServices!).ConfigureAwait(false);
    }

    public async Task SaveGameAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!EnsureServicesReadyFor("SaveGameAsync"))
        {
            DebugLogger.LogLifecycle("GameLifecycleManager: SaveGameAsync postponed until services ready");
            return;
        }
        await gameStateOps.ExecuteSaveGameAsync(filePath, currentServices!, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Load game state from file
    /// </summary>
    public void LoadGame(string filePath)
    {
        if (!servicesReady)
        {
            pendingLoadFilePath = filePath;
            ScheduleInitializeServices();
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: LoadGame fuer {filePath} wird nach Service-Init nachgeholt");
            return;
        }

        LoadGameInternal(filePath);
    }

    private void LoadGameInternal(string filePath)
    {
        // Delegate to GameStateOperations helper
        gameStateOps.ExecuteLoadGame(filePath, currentServices!);
    }
    
    /// <summary>
    /// Load game state from file (asynchron)
    /// </summary>
    public async Task LoadGameAsync(string filePath)
    {
        if (!EnsureServicesReadyFor("LoadGameAsync"))
        {
            pendingLoadFilePath = filePath;
            ScheduleInitializeServices();
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: LoadGameAsync fuer {filePath} wird nach Service-Init nachgeholt");
            return;
        }

        await gameStateOps.ExecuteLoadGameAsync(filePath, currentServices!).ConfigureAwait(false);
    }

    public async Task LoadGameAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!EnsureServicesReadyFor("LoadGameAsync"))
        {
            pendingLoadFilePath = filePath;
            ScheduleInitializeServices();
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: LoadGameAsync fuer {filePath} wird nach Service-Init nachgeholt");
            return;
        }
        await gameStateOps.ExecuteLoadGameAsync(filePath, currentServices!, cancellationToken).ConfigureAwait(false);
    }
    

    public async Task StarteErsteSpielrundeAsync()
    {
        if (!EnsureServicesReadyFor("StarteErsteSpielrundeAsync"))
        {
            pendingNewGame = true;
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        NewGame();
        InitialisiereDevFeatures();
    }

    private void InitialisiereDevFeatures()
    {
        // Delegate to DevFeatureSetup helper
        if (currentServices?.GameManager != null)
        {
            devFeatureSetup.InitializeDevFeatures(currentServices.GameManager);
        }
        else
        {
            DebugLogger.LogLifecycle("GameLifecycleManager: GameManager nicht verfügbar für DevFeatures");
        }
    }

    /// <summary>
    /// Clean up for scene restart/shutdown
    /// </summary>
    public override void _ExitTree()
    {
        // Reset internal state
        servicesReady = false;
        pendingNewGame = false;
        pendingLoadFilePath = null;
        currentServices = null;

        DebugLogger.LogLifecycle("GameLifecycleManager: Cleanup complete");
        base._ExitTree();
    }

    /// <summary>
    /// Force reset for scene restart
    /// </summary>
    public void ResetForSceneRestart()
    {
        DebugLogger.LogLifecycle("GameLifecycleManager: Resetting for scene restart");

        // Reset all state
        servicesReady = false;
        initializeScheduled = false;
        pendingNewGame = false;
        pendingLoadFilePath = null;
        currentServices = null;

        DebugLogger.LogLifecycle("GameLifecycleManager: Reset complete");
    }

    /// <summary>
    /// WORKAROUND: Auto-start NewGame to ensure game is playable
    /// </summary>
    private async void AutoStartNewGame()
    {
        DebugLogger.LogLifecycle("GameLifecycleManager: AutoStartNewGame called");

        // Wait a few frames for services to initialize
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        DebugLogger.LogLifecycle("GameLifecycleManager: AutoStartNewGame - attempting to start new game");

        try
        {
            // Try NewGame directly first
            NewGame();
            DebugLogger.LogLifecycle("GameLifecycleManager: AutoStartNewGame - NewGame completed");
        }
        catch (System.Exception ex)
        {
            DebugLogger.LogLifecycle($"GameLifecycleManager: AutoStartNewGame - NewGame failed: {ex.Message}");

            // Try StarteErsteSpielrundeAsync as fallback
            try
            {
                await StarteErsteSpielrundeAsync();
                DebugLogger.LogLifecycle("GameLifecycleManager: AutoStartNewGame - StarteErsteSpielrundeAsync completed");
            }
            catch (System.Exception ex2)
            {
                DebugLogger.LogLifecycle($"GameLifecycleManager: AutoStartNewGame - both methods failed: {ex2.Message}");
            }
        }
    }
}







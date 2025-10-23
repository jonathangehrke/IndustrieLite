// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Manages game lifecycle operations: New Game, Save, Load
/// Refactored to use helper classes for better separation of concerns.
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
        this.serviceResolver = new ServiceResolver(this);
        this.gameStateOps = new GameStateOperations(this);
        this.devFeatureSetup = new DevFeatureSetup(this);
    }

    public override void _Ready()
    {
        this.ScheduleInitializeServices();

        // WORKAROUND: Start NewGame automatically after a short delay
        this.CallDeferred(nameof(this.AutoStartNewGame));
    }

    private void ScheduleInitializeServices()
    {
        if (this.initializeScheduled)
        {
            return;
        }

        this.initializeScheduled = true;
        this.CallDeferred(nameof(this.InitializeServices));
    }

    private void InitializeServices()
    {
        this.initializeScheduled = false;

        if (this.servicesReady)
        {
            return;
        }

        // Try to resolve all services using the helper
        this.currentServices = this.serviceResolver.TryResolveServices();
        if (this.currentServices == null)
        {
            DebugLogger.LogLifecycle("GameLifecycleManager: Services nicht bereit - erneuter Versuch");
            this.ScheduleInitializeServices();
            return;
        }

        this.servicesReady = true;
        DebugLogger.LogLifecycle("GameLifecycleManager: Services initialized via ServiceResolver");
        DebugLogger.LogLifecycle(() => $"GameLifecycleManager: All basic services ready: {this.currentServices.AreAllServicesReady()}");

        // Handle pending operations
        if (this.pendingNewGame)
        {
            this.pendingNewGame = false;
            this.CallDeferred(nameof(this.NewGame));
        }

        if (!string.IsNullOrEmpty(this.pendingLoadFilePath))
        {
            var file = this.pendingLoadFilePath;
            this.pendingLoadFilePath = null;
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: Verzoegertes LoadGame fuer {file} wird ausgefuehrt");
            this.CallDeferred(nameof(this.LoadGameInternal), file);
        }
    }

    private bool EnsureServicesReadyFor(string operation)
    {
        if (this.servicesReady && this.currentServices != null && this.currentServices.AreAllServicesReady())
        {
            return true;
        }

        this.InitializeServices();

        if (this.servicesReady && this.currentServices != null && this.currentServices.AreAllServicesReady())
        {
            return true;
        }

        this.ScheduleInitializeServices();
        DebugLogger.LogLifecycle(() => $"GameLifecycleManager: {operation} wartet auf Service-Initialisierung");
        return false;
    }

    /// <summary>
    /// Start a new game with default settings.
    /// </summary>
    public void NewGame()
    {
        if (!this.EnsureServicesReadyFor("NewGame"))
        {
            this.pendingNewGame = true;
            return;
        }

        this.pendingNewGame = false;

        // Delegate to GameStateOperations helper
        this.gameStateOps.ExecuteNewGame(this.currentServices!);
    }

    /// <summary>
    /// Save current game state to file.
    /// </summary>
    public void SaveGame(string filePath)
    {
        if (!this.EnsureServicesReadyFor("SaveGame"))
        {
            return;
        }

        // Delegate to GameStateOperations helper
        this.gameStateOps.ExecuteSaveGame(filePath, this.currentServices!);
    }

    /// <summary>
    /// Save current game state to file (asynchron).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task SaveGameAsync(string filePath)
    {
        if (!this.EnsureServicesReadyFor("SaveGameAsync"))
        {
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: SaveGameAsync wartet auf Service-Initialisierung");
            return;
        }

        await this.gameStateOps.ExecuteSaveGameAsync(filePath, this.currentServices!).ConfigureAwait(false);
    }

    public async Task SaveGameAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!this.EnsureServicesReadyFor("SaveGameAsync"))
        {
            DebugLogger.LogLifecycle("GameLifecycleManager: SaveGameAsync postponed until services ready");
            return;
        }
        await this.gameStateOps.ExecuteSaveGameAsync(filePath, this.currentServices!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Load game state from file.
    /// </summary>
    public void LoadGame(string filePath)
    {
        if (!this.servicesReady)
        {
            this.pendingLoadFilePath = filePath;
            this.ScheduleInitializeServices();
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: LoadGame fuer {filePath} wird nach Service-Init nachgeholt");
            return;
        }

        this.LoadGameInternal(filePath);
    }

    private void LoadGameInternal(string filePath)
    {
        // Delegate to GameStateOperations helper
        this.gameStateOps.ExecuteLoadGame(filePath, this.currentServices!);
    }

    /// <summary>
    /// Load game state from file (asynchron).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task LoadGameAsync(string filePath)
    {
        if (!this.EnsureServicesReadyFor("LoadGameAsync"))
        {
            this.pendingLoadFilePath = filePath;
            this.ScheduleInitializeServices();
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: LoadGameAsync fuer {filePath} wird nach Service-Init nachgeholt");
            return;
        }

        await this.gameStateOps.ExecuteLoadGameAsync(filePath, this.currentServices!).ConfigureAwait(false);
    }

    public async Task LoadGameAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!this.EnsureServicesReadyFor("LoadGameAsync"))
        {
            this.pendingLoadFilePath = filePath;
            this.ScheduleInitializeServices();
            DebugLogger.LogLifecycle(() => $"GameLifecycleManager: LoadGameAsync fuer {filePath} wird nach Service-Init nachgeholt");
            return;
        }
        await this.gameStateOps.ExecuteLoadGameAsync(filePath, this.currentServices!, cancellationToken).ConfigureAwait(false);
    }

    public async Task StarteErsteSpielrundeAsync()
    {
        if (!this.EnsureServicesReadyFor("StarteErsteSpielrundeAsync"))
        {
            this.pendingNewGame = true;
            return;
        }

        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);
        this.NewGame();
        this.InitialisiereDevFeatures();
    }

    private void InitialisiereDevFeatures()
    {
        // Delegate to DevFeatureSetup helper
        if (this.currentServices?.GameManager != null)
        {
            this.devFeatureSetup.InitializeDevFeatures(this.currentServices.GameManager);
        }
        else
        {
            DebugLogger.LogLifecycle("GameLifecycleManager: GameManager nicht verfügbar für DevFeatures");
        }
    }

    /// <summary>
    /// Clean up for scene restart/shutdown.
    /// </summary>
    public override void _ExitTree()
    {
        // Reset internal state
        this.servicesReady = false;
        this.pendingNewGame = false;
        this.pendingLoadFilePath = null;
        this.currentServices = null;

        DebugLogger.LogLifecycle("GameLifecycleManager: Cleanup complete");
        base._ExitTree();
    }

    /// <summary>
    /// Force reset for scene restart.
    /// </summary>
    public void ResetForSceneRestart()
    {
        DebugLogger.LogLifecycle("GameLifecycleManager: Resetting for scene restart");

        // Reset all state
        this.servicesReady = false;
        this.initializeScheduled = false;
        this.pendingNewGame = false;
        this.pendingLoadFilePath = null;
        this.currentServices = null;

        DebugLogger.LogLifecycle("GameLifecycleManager: Reset complete");
    }

    /// <summary>
    /// WORKAROUND: Auto-start NewGame to ensure game is playable.
    /// </summary>
    private async void AutoStartNewGame()
    {
        DebugLogger.LogLifecycle("GameLifecycleManager: AutoStartNewGame called");

        // Wait a few frames for services to initialize
        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);
        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);
        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);

        DebugLogger.LogLifecycle("GameLifecycleManager: AutoStartNewGame - attempting to start new game");

        try
        {
            // Try NewGame directly first
            this.NewGame();
            DebugLogger.LogLifecycle("GameLifecycleManager: AutoStartNewGame - NewGame completed");
        }
        catch (System.Exception ex)
        {
            DebugLogger.LogLifecycle($"GameLifecycleManager: AutoStartNewGame - NewGame failed: {ex.Message}");

            // Try StarteErsteSpielrundeAsync as fallback
            try
            {
                await this.StarteErsteSpielrundeAsync();
                DebugLogger.LogLifecycle("GameLifecycleManager: AutoStartNewGame - StarteErsteSpielrundeAsync completed");
            }
            catch (System.Exception ex2)
            {
                DebugLogger.LogLifecycle($"GameLifecycleManager: AutoStartNewGame - both methods failed: {ex2.Message}");
            }
        }
    }
}







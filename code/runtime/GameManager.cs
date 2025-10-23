// SPDX-License-Identifier: MIT
using System.Threading.Tasks;
using Godot;

public partial class GameManager : Node2D
{
    [Signal]
    public delegate void CompositionCompletedEventHandler();

    public GameManager()
    {
    }

    // Manager-Referenzen
    public LandManager LandManager { get; private set; } = default!;

    public BuildingManager BuildingManager { get; private set; } = default!;

    public TransportManager TransportManager { get; private set; } = default!;

    public RoadManager RoadManager { get; private set; } = default!;

    public EconomyManager EconomyManager { get; private set; } = default!;

    public InputManager InputManager { get; private set; } = default!;

    public ResourceManager ResourceManager { get; private set; } = default!;

    public ProductionManager ProductionManager { get; private set; } = default!;

    public GameClockManager GameClockManager { get; private set; } = default!;

    public SaveLoadService SaveLoadService { get; private set; } = default!;

    public Simulation Simulation { get; private set; } = default!;

    public ManagerCoordinator ManagerCoordinator { get; private set; } = default!;

    private EventHub? eventHub; // Cached reference to EventHub autoload

    private DIContainer? diContainer;
    private bool initializationComplete;
    private bool serviceContainerRetryLogged;

    public bool IsCompositionComplete { get; private set; }

    /// <summary>
    /// Explicit DI initialization for GameManager to avoid Service Locator usage.
    /// Called by DIContainer to inject EventHub dependency.
    /// </summary>
    public void Initialize(EventHub? eventHub)
    {
        this.eventHub = eventHub;
        DebugLogger.LogServices($"GameManager.Initialize(): EventHub={(eventHub != null ? "OK" : "null")}");
    }

    public override void _EnterTree()
    {
        base._EnterTree();

        // Fruehe Named-Registrierung im ServiceContainer, damit UI/Boot-Checks den GameManager finden
        try
        {
            var sc = ServiceContainer.Instance;
            sc?.RegisterNamedService(ServiceNames.GameManager, this);
        }
        catch
        {
        }

        this.diContainer = this.GetNodeOrNull<DIContainer>("DIContainer");
        if (this.diContainer != null)
        {
            this.diContainer.Initialisiere(this);
        }
        else
        {
            DebugLogger.Error("debug_services", "DIContainerNotFound", "DIContainer nicht gefunden - Service-Registrierung entfaellt.");
        }
    }

    public override void _Ready()
    {
        DebugLogger.Info("debug_services", "GameManagerReady", "_Ready() called");

        // Services bereits in _EnterTree registriert - keine doppelte Registrierung
        DebugLogger.Debug("debug_services", "GameManagerServicesAlreadyRegistered", "Services already registered in _EnterTree");

        // Create GameLifecycleManager immediately in _Ready (not in InitializeAsync)
        var lifecycleManager = this.GetNodeOrNull<GameLifecycleManager>("GameLifecycleManager");
        DebugLogger.Debug("debug_services", "LifecycleManagerLookupReady", lifecycleManager != null ? "found" : "null");
        if (lifecycleManager == null)
        {
            DebugLogger.Info("debug_services", "CreateLifecycleManagerReady", "Creating GameLifecycleManager in _Ready");
            lifecycleManager = new GameLifecycleManager();
            lifecycleManager.Name = "GameLifecycleManager";
            this.AddChild(lifecycleManager);
            DebugLogger.Info("debug_services", "LifecycleManagerCreatedReady", "GameLifecycleManager created and added in _Ready");
        }

        // Direct call to InitializeAsync (no deferred)
        this.InitializeAsync();

        // WORKAROUND: Start NewGame directly here
        this.CallDeferred(nameof(this.StartNewGameDirect));

        // Ensure Toast HUD exists for UI feedback
        var existingToast = this.GetNodeOrNull<ToastHud>("ToastHud");
        if (existingToast == null)
        {
            var toast = new ToastHud();
            toast.Name = "ToastHud";
            this.AddChild(toast);
        }
    }

    private async void InitializeAsync()
    {
        DebugLogger.Debug("debug_services", "GameManagerInitializeAsync", "InitializeAsync() called");
        await this.EnsureInitializedAsync().ConfigureAwait(false);
    }

    private async Task EnsureInitializedAsync()
    {
        DebugLogger.LogServices("GameManager: EnsureInitializedAsync() called");
        if (this.initializationComplete)
        {
            DebugLogger.LogServices("GameManager: Already initialized, skipping");
            return;
        }

        var sc = ServiceContainer.Instance;
        DebugLogger.LogServices($"GameManager: ServiceContainer.Instance={(sc != null ? "OK" : "NULL")}");
        if (sc == null)
        {
            if (!this.serviceContainerRetryLogged)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Warn, () => "GameManager: ServiceContainer nicht bereit - warte auf Initialisierung");
                this.serviceContainerRetryLogged = true;
            }

            var tree = this.GetTree();
            if (tree != null)
            {
                sc = await ServiceContainer.WhenAvailableAsync(tree);
            }
            else
            {
                // Fallback: Direkt lesen, falls kein Tree verfuegbar
                sc = ServiceContainer.Instance;
            }
        }

        this.serviceContainerRetryLogged = false;
        await this.InitializeMitServicesAsync(sc!);
        this.initializationComplete = true;
    }

    private async Task InitializeMitServicesAsync(ServiceContainer serviceContainer)
    {
        try
        {
            InputActionsInitializer.EnsureDefaults();
        }
        catch
        {
        }

        // EventHub is now injected via Initialize() method called by DIContainer
        // No more Service Locator pattern here!
        this.LandManager = this.GetNode<LandManager>("LandManager");
        this.BuildingManager = this.GetNode<BuildingManager>("BuildingManager");
        this.TransportManager = this.GetNode<TransportManager>("TransportManager");
        this.RoadManager = this.GetNode<RoadManager>("RoadManager");
        this.EconomyManager = this.GetNode<EconomyManager>("EconomyManager");
        this.InputManager = this.GetNode<InputManager>("InputManager");
        this.ResourceManager = this.GetNode<ResourceManager>("ResourceManager");
        this.ProductionManager = this.GetNode<ProductionManager>("ProductionManager");
        this.ManagerCoordinator = this.GetNodeOrNull<ManagerCoordinator>("ManagerCoordinator");
        if (this.ManagerCoordinator == null)
        {
            this.ManagerCoordinator = new ManagerCoordinator();
            this.ManagerCoordinator.Name = "ManagerCoordinator";
            this.AddChild(this.ManagerCoordinator);
        }
        this.SaveLoadService = this.GetNodeOrNull<SaveLoadService>("SaveLoadService");
        if (this.SaveLoadService == null)
        {
            this.SaveLoadService = new SaveLoadService();
            this.SaveLoadService.Name = "SaveLoadService";
            this.AddChild(this.SaveLoadService);
        }

        var lifecycleManager = this.GetNodeOrNull<GameLifecycleManager>("GameLifecycleManager");
        DebugLogger.Debug("debug_services", "LifecycleManagerLookupResult", lifecycleManager != null ? "found" : "null");
        if (lifecycleManager == null)
        {
            DebugLogger.Info("debug_services", "CreateLifecycleManager", "Creating new GameLifecycleManager");
            lifecycleManager = new GameLifecycleManager();
            lifecycleManager.Name = "GameLifecycleManager";
            this.AddChild(lifecycleManager);
            DebugLogger.Info("debug_services", "LifecycleManagerCreated", "GameLifecycleManager created and added as child");
        }

        this.ManagerCoordinator?.AktualisiereReferenzen(this);
        // DI-Container initialisiert bereits alle Services in _EnterTree()
        if (this.GameClockManager == null)
        {
            var clockFromService = await serviceContainer.WaitForNamedService<GameClockManager>(ServiceNames.GameClockManager);
            if (clockFromService != null)
            {
                this.GameClockManager = clockFromService;
            }
        }
        this.GameClockManager ??= this.GetNodeOrNull<GameClockManager>("GameClockManager");
        if (this.GameClockManager == null)
        {
            DebugLogger.Error("debug_services", "GameClockManagerMissing", "GameClockManager nicht gefunden - Initialisierung abgebrochen.");
            return;
        }

        if (this.Simulation == null)
        {
            // Try to get Simulation from child nodes (not ServiceContainer runtime lookup)
            this.Simulation = this.GetNodeOrNull<Simulation>("Simulation");
        }
        if (this.Simulation == null)
        {
            var simulationFromService = await serviceContainer.WaitForNamedService<Simulation>(ServiceNames.Simulation);
            if (simulationFromService != null)
            {
                this.Simulation = simulationFromService;
            }
        }
        this.Simulation ??= this.GetNodeOrNull<Simulation>("Simulation");
        if (this.Simulation == null)
        {
            DebugLogger.Error("debug_services", "SimulationMissing", "Simulation nicht gefunden - Initialisierung abgebrochen.");
            return;
        }
        DebugLogger.Debug("debug_services", "UsingSimulationInstance", $"Using Simulation instance", new System.Collections.Generic.Dictionary<string, object?>(System.StringComparer.Ordinal) { { "hash", this.Simulation.GetHashCode() } });

        // GameTimeManager should already exist in scene (added to Main.tscn)
        var gameTimeManager = this.GetNodeOrNull<GameTimeManager>("GameTimeManager");
        if (gameTimeManager != null)
        {
            DebugLogger.Debug("debug_services", "GameTimeManagerFound", "GameTimeManager found in scene");
        }
        else
        {
            DebugLogger.Error("debug_services", "GameTimeManagerMissing", "GameTimeManager not found in scene! Date will not advance!");
        }

        // CityGrowthManager should already exist in scene (added to Main.tscn)
        var cityGrowth = this.GetNodeOrNull<CityGrowthManager>("CityGrowthManager");
        if (cityGrowth == null)
        {
            DebugLogger.Error("debug_services", "CityGrowthManagerMissing", "CityGrowthManager not found in scene!");
        }

        // KRITISCH: KEINE neuen Manager erstellen oder Services neu registrieren!
        // Die Services sind bereits in InitializeBasicServices() registriert
        DebugLogger.Debug("debug_services", "SkippingServiceReregistration", "Skipping service re-registration to avoid overwriting");

        // Markiere Komposition als abgeschlossen und starte Simulation im Anschluss
        this.IsCompositionComplete = true;
        this.EmitSignal(SignalName.CompositionCompleted);
        if (this.Simulation != null && !this.Simulation.IstAktiv)
        {
            DebugLogger.Info("debug_services", "StartingSimulation", "Starting Simulation after composition completed...");
            this.Simulation.Start();
            DebugLogger.Info("debug_services", "SimulationStarted", "Simulation started!");
        }
        else if (this.Simulation == null)
        {
            DebugLogger.Error("debug_services", "SimulationNull", "Simulation is NULL - cannot start!");
        }

        DebugLogger.Debug("debug_services", "SettingTileSize", "Setting BuildingManager.TileSize");
        this.BuildingManager.TileSize = this.LandManager.GridW > 0 ? 32 : 32;

        DebugLogger.Debug("debug_services", "InitializeAsyncEnd", "InitializeAsync method ending - scheduling NewGame");

        // Schedule NewGame to run after InitializeAsync completes
        if (lifecycleManager != null)
        {
            this.CallDeferred(nameof(this.StartNewGameDeferred), lifecycleManager);
        }
        else
        {
            DebugLogger.Error("debug_services", "LifecycleManagerNull", "ERROR - lifecycleManager is null, cannot start game!");
        }
    }

    /// <summary>
    /// Deferred method to start NewGame after all services are initialized.
    /// </summary>
    private async void StartNewGameDeferred(GameLifecycleManager lifecycleManager)
    {
        DebugLogger.Info("debug_services", "StartNewGameDeferred", "StartNewGameDeferred called");

        if (lifecycleManager == null)
        {
            DebugLogger.Error("debug_services", "LifecycleManagerNullInDeferred", "lifecycleManager is null in StartNewGameDeferred");
            return;
        }

        try
        {
            DebugLogger.Debug("debug_services", "CallFirstRoundDeferred", "Calling StarteErsteSpielrundeAsync from deferred method");
            await lifecycleManager.StarteErsteSpielrundeAsync();
            DebugLogger.Info("debug_services", "FirstRoundCompleted", "StarteErsteSpielrundeAsync completed successfully");
        }
        catch (System.Exception ex)
        {
            DebugLogger.Error("debug_services", "FirstRoundFailed", ex.Message);
            DebugLogger.Error("debug_services", "FirstRoundStack", ex.StackTrace ?? string.Empty);
        }
    }

    /// <summary>
    /// WORKAROUND: Start NewGame directly in GameManager (bypass GameLifecycleManager).
    /// </summary>
    private async void StartNewGameDirect()
    {
        DebugLogger.Info("debug_services", "StartNewGameDirect", "StartNewGameDirect called");

        // Wait for services to initialize
        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);
        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);
        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);

        DebugLogger.Info("debug_services", "StartNewGameInit", "Starting game initialization");

        try
        {
            // Set starting money
            if (this.EconomyManager != null)
            {
                this.EconomyManager.SetMoney(this.EconomyManager.StartingMoney);
                DebugLogger.Info("debug_services", "StartMoneySet", $"Set starting money to {this.EconomyManager.StartingMoney}");
            }

            // Reset capacities and production
            this.ProductionManager?.ClearAllData();
            this.ResourceManager?.ClearAllData();

            // Transport-System stoppen, damit keine Ticks auf entsorgte Nodes erfolgen
            this.TransportManager?.ClearAllData();

            // Clear existing buildings cleanly
            if (this.BuildingManager != null)
            {
                this.BuildingManager.ClearAllData();
                DebugLogger.Debug("debug_services", "ClearedBuildings", "Cleared existing buildings via ClearAllData()");
            }

            // Clear existing roads (if any)
            if (this.RoadManager != null)
            {
                this.RoadManager.ClearAllRoads();
                DebugLogger.Debug("debug_services", "ClearedRoads", "Cleared existing roads");
            }

            // Reset land ownership
            if (this.LandManager != null)
            {
                this.LandManager.ResetAllLandFalse();
                this.LandManager.InitializeStartRegion();
                DebugLogger.Debug("debug_services", "LandResetInitStart", "Reset land ownership and initialized start region");
            }

            // Place starting city (within deterministic context)
            if (this.BuildingManager != null && this.LandManager != null)
            {
                int sx = (this.LandManager.GridW / 2) - 5;
                int sy = (this.LandManager.GridH / 2) - 4;
                var cityPosition = new Vector2I(sx + 12, sy + 2);

                using (Simulation.EnterDeterministicTestScope())
                {
                    var city = this.BuildingManager.PlaceBuilding("city", cityPosition);
                    if (city != null)
                    {
                        DebugLogger.Info("debug_services", "StartCityPlaced", $"Starting city placed at {cityPosition}");
                    }
                    else
                    {
                        DebugLogger.Error("debug_services", "StartCityPlaceFailed", $"FAILED to place starting city at {cityPosition}");
                    }
                }
            }

            // Startressourcen setzen (zentral aus GameConstants)
            if (this.ResourceManager != null)
            {
                foreach (var pair in GameConstants.Startup.InitialResources)
                {
                    this.ResourceManager.SetProduction(pair.Key, pair.Value);
                    this.ResourceManager.GetResourceInfo(pair.Key).Available = pair.Value;
                }

                DebugLogger.Info("debug_services", "StartupResourcesSet", "Startressourcen gesetzt");
            }

            // Emit money changed event using cached EventHub reference
            if (this.eventHub != null && this.EconomyManager != null)
            {
                this.eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, this.EconomyManager.GetMoney());
                DebugLogger.Debug("debug_services", "EmittedMoneyChanged", "Emitted MoneyChanged event");
            }

            DebugLogger.Info("debug_services", "StartNewGameDirectCompleted", "StartNewGameDirect completed successfully!");
        }
        catch (System.Exception ex)
        {
            DebugLogger.Error("debug_services", "StartNewGameDirectFailed", ex.Message);
            DebugLogger.Error("debug_services", "StartNewGameDirectStack", ex.StackTrace ?? string.Empty);
        }
    }
}


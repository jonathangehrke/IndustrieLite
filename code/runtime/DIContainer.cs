// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// Verantwortlich fuer Service-Registrierung und NodePath-Verkabelung der Manager.
/// Wird vom GameManager als Kind genutzt, um die DI-Initialisierung zu buendeln.
/// </summary>
public partial class DIContainer : Node
{
    private GameManager? gameManager;

    /// <summary>
    /// Setzt den Bezug zum GameManager und fuehrt sofort die Grundinitialisierung aus.
    /// NEUE ARCHITEKTUR: Synchrone, typisierte DI statt Service-Locator.
    /// </summary>
    public void Initialisiere(GameManager gameManager)
    {
        if (this.gameManager != gameManager)
        {
            this.gameManager = gameManager;
        }

        // Zentrale Composition Root (Phase 3+4)
        this.InitializeAll();
    }

    /// <summary>
    /// NEUE ARCHITEKTUR: Zentrale Composition Root.
    /// Alle Manager-Dependencies werden hier explizit verdrahtet.
    /// </summary>
    private void InitializeAll()
    {
        var scEarly = ServiceContainer.Instance;
        if (scEarly != null) { this.WaitForDatabaseIfMissing(scEarly);
        }

        if (this.gameManager == null)
        {
            DebugLogger.Error("debug_services", "DIInitGameManagerNull", "GameManager ist null");
            return;
        }

        // Lokale Variable um Null-Warnungen zu vermeiden
        var gameManager = this.gameManager;

        DebugLogger.LogServices("DIContainer.InitializeAll: Starte zentrale DI-Verdrahtung");

        // ========== Phase 1: Autoload-Services holen (Read-Only) ==========
        var sc = ServiceContainer.Instance;
        if (sc == null)
        {
            DebugLogger.Error("debug_services", "DIInitServiceContainerNull", "ServiceContainer nicht verfügbar");
            return;
        }

        var database = sc.GetNamedService<Database>(ServiceNames.Database);
        var gameDatabase = sc.GetNamedService<GameDatabase>("GameDatabase");
        var eventHub = sc.GetNamedService<EventHub>(ServiceNames.EventHub);

        // SceneGraphAdapter (Autoload für Hexagonal Architecture - Ports & Adapters)
        var sceneGraphAdapter = sc.GetNamedService<SceneGraphAdapter>("SceneGraphAdapter");
        ISceneGraph? sceneGraph = sceneGraphAdapter;
        if (sceneGraph == null)
        {
            DebugLogger.Error("debug_services", "DIInitSceneGraphNull", "SceneGraphAdapter nicht verfügbar - Manager können keine Nodes hinzufügen");
        }

        // DevFlags: Aus ServiceContainer oder Fallback via Autoload
        var devFlags = sc.GetNamedService<Node>("DevFlags") ?? gameManager.GetNodeOrNull<Node>("/root/DevFlags");

        // DataIndex: Aus ServiceContainer oder Fallback via Autoload
        var dataIndex = sc.GetNamedService<Node>("DataIndex") ?? gameManager.GetNodeOrNull<Node>("/root/DataIndex");

        var uiService = sc.GetNamedService<UIService>(ServiceNames.UIService);

        // ========== Phase 1.5: GameManager initialisieren (EventHub injection) ==========
        gameManager.Initialize(eventHub);
        DebugLogger.LogServices("DIContainer.InitializeAll: GameManager.Initialize() OK");

        // ========== Phase 2: Manager-Instanzen aus GameManager-Tree holen ==========
        var landManager = gameManager.GetNodeOrNull<LandManager>("LandManager");
        var economyManager = gameManager.GetNodeOrNull<EconomyManager>("EconomyManager");
        var buildingManager = gameManager.GetNodeOrNull<BuildingManager>("BuildingManager");
        var transportManager = gameManager.GetNodeOrNull<TransportManager>("TransportManager");
        var roadManager = gameManager.GetNodeOrNull<RoadManager>("RoadManager");
        var inputManager = gameManager.GetNodeOrNull<InputManager>("InputManager");
        var resourceManager = gameManager.GetNodeOrNull<ResourceManager>("ResourceManager");
        var productionManager = gameManager.GetNodeOrNull<ProductionManager>("ProductionManager");
        var gameClockManager = gameManager.GetNodeOrNull<GameClockManager>("GameClockManager");
        var cityGrowthManager = gameManager.GetNodeOrNull<CityGrowthManager>("CityGrowthManager");
        var simulation = gameManager.GetNodeOrNull<Simulation>("Simulation");
        var productionSystem = gameManager.GetNodeOrNull<ProductionSystem>("ProductionSystem");
        var gameTimeManager = gameManager.GetNodeOrNull<GameTimeManager>("GameTimeManager");
        var levelManager = gameManager.GetNodeOrNull<LevelManager>("LevelManager");
        var resourceRegistry = gameManager.GetNodeOrNull<ResourceRegistry>("ResourceRegistry");
        var saveLoadService = gameManager.GetNodeOrNull<SaveLoadService>("SaveLoadService");
        var managerCoordinator = gameManager.GetNodeOrNull<ManagerCoordinator>("ManagerCoordinator");

        // Helper Services (Phase 6)
        var logisticsService = gameManager.GetNodeOrNull<LogisticsService>("LogisticsService");
        var marketService = gameManager.GetNodeOrNull<MarketService>("MarketService");
        var supplierService = gameManager.GetNodeOrNull<SupplierService>("SupplierService");
        var productionCalculationService = gameManager.GetNodeOrNull<ProductionCalculationService>("ProductionCalculationService");
        // ResourceTotalsService is at root level (sibling of GameManager), not under GameManager
        var resourceTotalsService = gameManager.GetNodeOrNull<ResourceTotalsService>("../ResourceTotalsService");
        if (resourceTotalsService == null)
        {
            // Fallback: Try to get from parent
            var root = gameManager.GetParent();
            if (root != null)
            {
                resourceTotalsService = root.GetNodeOrNull<ResourceTotalsService>("ResourceTotalsService");
            }
        }

        // Relative Nodes (außerhalb GameManager)
        var map = gameManager.GetNodeOrNull<Map>("../Map");
        var camera = gameManager.GetNodeOrNull<CameraController>("../Camera");

        // ========== Phase 3: Dependencies explizit injizieren (in Abhängigkeitsreihenfolge) ==========

        // 3.1: EconomyManager (keine Dependencies außer EventHub)
        if (economyManager != null)
        {
            economyManager.Initialize(eventHub);
            DebugLogger.LogServices("DIContainer.InitializeAll: EconomyManager.Initialize() OK");
        }

        // 3.2: LandManager (benötigt EconomyManager)
        if (landManager != null && economyManager != null)
        {
            landManager.Initialize(economyManager, eventHub);
            DebugLogger.LogServices("DIContainer.InitializeAll: LandManager.Initialize() OK");
        }

        // 3.3: RoadManager (benötigt Land, Building, Economy, SceneGraph)
        // NOTE: Needs BuildingManager, so must come after BuildingManager init

        // 3.4: GameTimeManager (benötigt EventHub, Simulation)
        if (gameTimeManager != null && simulation != null)
        {
            gameTimeManager.Initialize(eventHub, simulation);
            DebugLogger.LogServices("DIContainer.InitializeAll: GameTimeManager.Initialize() OK");
        }

        // 3.5: LevelManager (benötigt EventHub)
        if (levelManager != null)
        {
            levelManager.Initialize(eventHub);
            DebugLogger.LogServices("DIContainer.InitializeAll: LevelManager.Initialize() OK");
        }

        // 3.6: BuildingManager (benötigt Land, Economy, SceneGraph - ProductionManager ist optional)
        // IMPORTANT: Initialized WITHOUT ProductionManager to break circular dependency
        if (buildingManager != null && landManager != null && economyManager != null && sceneGraph != null)
        {
            buildingManager.Initialize(landManager, economyManager, sceneGraph, database, eventHub, null, simulation, gameTimeManager, null, dataIndex);
            DebugLogger.LogServices("DIContainer.InitializeAll: BuildingManager.Initialize() OK (without ProductionManager/RoadManager)");
        }

        // 3.6.1: RoadManager (benötigt BuildingManager - now available!)
        if (roadManager != null && landManager != null && buildingManager != null && economyManager != null && sceneGraph != null)
        {
            roadManager.Initialize(landManager, buildingManager, economyManager, sceneGraph, eventHub, camera, dataIndex);
            DebugLogger.LogServices("DIContainer.InitializeAll: RoadManager.Initialize() OK");
        }

        // 3.7: ResourceManager (benötigt BuildingManager - now available!)
        // Circular dependency RESOLVED: BuildingManager initialized first, then ResourceManager
        if (resourceManager != null && buildingManager != null && simulation != null)
        {
            resourceManager.Initialize(resourceRegistry, eventHub, simulation, buildingManager);
            DebugLogger.LogServices("DIContainer.InitializeAll: ResourceManager.Initialize() OK (with BuildingManager)");
        }

        // 3.8: ProductionManager (benötigt ResourceManager)
        if (productionManager != null && resourceManager != null && simulation != null)
        {
            productionManager.Initialize(resourceManager, simulation, productionSystem, devFlags);
            DebugLogger.LogServices("DIContainer.InitializeAll: ProductionManager.Initialize() OK");
        }

        // 3.8.1: Set ProductionManager in BuildingManager (breaks circular dependency)
        // BuildingManager was initialized without ProductionManager, now update it
        if (buildingManager != null && productionManager != null)
        {
            buildingManager.SetProductionManager(productionManager);
            DebugLogger.LogServices("DIContainer.InitializeAll: BuildingManager.SetProductionManager() OK");
        }

        // 3.9: TransportManager (benötigt Building, Road, Economy, Game, SceneGraph, Event, GameTime)
        if (transportManager != null && buildingManager != null && economyManager != null && sceneGraph != null)
        {
            transportManager.Initialize(buildingManager, roadManager, economyManager, gameManager, sceneGraph, eventHub, gameTimeManager);
            DebugLogger.LogServices("DIContainer.InitializeAll: TransportManager.Initialize() OK");
        }

        // 3.10: InputManager (benötigt fast alle Manager)
        if (inputManager != null && landManager != null && buildingManager != null && economyManager != null && transportManager != null && map != null)
        {
            inputManager.Initialize(landManager, buildingManager, economyManager, transportManager, roadManager, map, gameManager, eventHub, camera, simulation, uiService);
            DebugLogger.LogServices("DIContainer.InitializeAll: InputManager.Initialize() OK");
        }

        // 3.11: GameClockManager (Event-basiert, keine Hard-Dependencies)
        if (gameClockManager != null)
        {
            gameClockManager.Initialize(eventHub);
            DebugLogger.LogServices("DIContainer.InitializeAll: GameClockManager.Initialize() OK");
        }

        // 3.12: CityGrowthManager (benötigt EventHub für MonthChanged Signal)
        if (cityGrowthManager != null)
        {
            cityGrowthManager.Initialize(eventHub);
            DebugLogger.LogServices("DIContainer.InitializeAll: CityGrowthManager.Initialize() OK");
        }

        // ========== Phase 3.13: Helper Services (Phase 6 - Explicit DI statt ServiceContainer lookups) ==========

        // 3.13.1: LogisticsService (benötigt EconomyManager, EventHub)
        if (logisticsService != null && economyManager != null)
        {
            logisticsService.Initialize(economyManager, eventHub);
            DebugLogger.LogServices("DIContainer.InitializeAll: LogisticsService.Initialize() OK");
        }

        // 3.11.2: MarketService (benötigt ResourceManager, TransportManager, EconomyManager, BuildingManager, LevelManager)
        if (marketService != null && resourceManager != null && transportManager != null && economyManager != null && buildingManager != null && levelManager != null)
        {
            marketService.Initialize(resourceManager, transportManager, economyManager, buildingManager, levelManager, database);
            DebugLogger.LogServices("DIContainer.InitializeAll: MarketService.Initialize() OK");
        }
        else if (marketService != null)
        {
            DebugLogger.Error("debug_services", "DIInitMarketServiceMissingDeps", $"MarketService cannot initialize - Missing dependencies (ResourceManager: {resourceManager != null}, TransportManager: {transportManager != null}, EconomyManager: {economyManager != null}, BuildingManager: {buildingManager != null}, LevelManager: {levelManager != null})");
        }

        // 3.11.3: SupplierService (benötigt BuildingManager, TransportManager, GameDatabase, EventHub)
        if (supplierService != null && buildingManager != null && transportManager != null && gameDatabase != null)
        {
            supplierService.Initialize(buildingManager, transportManager, gameDatabase, eventHub);
            DebugLogger.LogServices("DIContainer.InitializeAll: SupplierService.Initialize() OK");
        }
        else if (supplierService != null && gameDatabase == null)
        {
            DebugLogger.Error("debug_services", "SupplierServiceGameDatabaseMissing", "SupplierService cannot be initialized: GameDatabase not available");
        }

        // 3.11.4: ProductionCalculationService (benötigt GameDatabase)
        if (productionCalculationService != null && gameDatabase != null)
        {
            productionCalculationService.Initialize(gameDatabase);
            DebugLogger.LogServices("DIContainer.InitializeAll: ProductionCalculationService.Initialize() OK");
        }

        // 3.11.5: ResourceTotalsService (benötigt viele Services)
        if (resourceTotalsService != null)
        {
            resourceTotalsService.Initialize(database, buildingManager, resourceManager, resourceRegistry, eventHub, simulation, devFlags, productionSystem, gameClockManager);
            DebugLogger.LogServices("DIContainer.InitializeAll: ResourceTotalsService.Initialize() OK");
        }
        else
        {
            DebugLogger.Error("debug_services", "DIInitResourceTotalsMissing", "ResourceTotalsService not found! Production totals will not work!");
        }

        // 3.13: UIService (benötigt GameManager + alle Manager + MarketService)
        if (uiService != null && economyManager != null && buildingManager != null && transportManager != null && roadManager != null && inputManager != null && eventHub != null && database != null)
        {
            uiService.Initialize(gameManager, economyManager, buildingManager, transportManager, roadManager, inputManager, eventHub, database, marketService, levelManager, dataIndex);
            DebugLogger.LogServices("DIContainer.InitializeAll: UIService.Initialize() OK");
        }

        // 3.14: ManagerCoordinator (delegiert an Manager)
        if (managerCoordinator != null)
        {
            managerCoordinator.AktualisiereReferenzen(gameManager, devFlags);
            DebugLogger.LogServices("DIContainer.InitializeAll: ManagerCoordinator.AktualisiereReferenzen() OK");
        }

        // 3.15: Map (benötigt GameManager, CameraController)
        if (map != null && camera != null)
        {
            map.Initialize(gameManager, camera);
            DebugLogger.LogServices("DIContainer.InitializeAll: Map.Initialize() OK");
        }

        // ========== Phase 4: Registry für UI-Bridge (Named) + Interface-basiert ==========
        // Backward compatibility: Register as Named for GDScript bridge
        this.RegisterForUI(sc, ServiceNames.GameManager, gameManager);
        this.RegisterForUI(sc, nameof(LandManager), landManager);
        this.RegisterForUI(sc, nameof(EconomyManager), economyManager);
        this.RegisterForUI(sc, nameof(BuildingManager), buildingManager);
        this.RegisterForUI(sc, nameof(TransportManager), transportManager);
        this.RegisterForUI(sc, nameof(RoadManager), roadManager);
        this.RegisterForUI(sc, nameof(InputManager), inputManager);
        this.RegisterForUI(sc, nameof(ResourceManager), resourceManager);
        this.RegisterForUI(sc, nameof(ProductionManager), productionManager);
        this.RegisterForUI(sc, ServiceNames.GameClockManager, gameClockManager);
        this.RegisterForUI(sc, nameof(CityGrowthManager), cityGrowthManager);
        this.RegisterForUI(sc, nameof(GameTimeManager), gameTimeManager);
        this.RegisterForUI(sc, ServiceNames.Simulation, simulation);
        this.RegisterForUI(sc, nameof(ManagerCoordinator), managerCoordinator);
        this.RegisterForUI(sc, nameof(SaveLoadService), saveLoadService);
        this.RegisterForUI(sc, ServiceNames.ResourceRegistry, resourceRegistry);

        // Helper Services (Phase 6)
        this.RegisterForUI(sc, nameof(LogisticsService), logisticsService);
        this.RegisterForUI(sc, nameof(MarketService), marketService);
        this.RegisterForUI(sc, nameof(SupplierService), supplierService);
        this.RegisterForUI(sc, nameof(ProductionCalculationService), productionCalculationService);
        this.RegisterForUI(sc, nameof(ResourceTotalsService), resourceTotalsService);

        // NEW: Interface-based registration for proper DI (testability & decoupling)
        this.RegisterInterface<IEconomyManager>(sc, economyManager);
        this.RegisterInterface<IBuildingManager>(sc, buildingManager);
        this.RegisterInterface<IProductionManager>(sc, productionManager);
        this.RegisterInterface<IResourceManager>(sc, resourceManager);
        this.RegisterInterface<ITransportManager>(sc, transportManager);
        this.RegisterInterface<IRoadManager>(sc, roadManager);

        // ========== Phase 5: Validierung (alle Manager initialisiert?) ==========
        this.ValidateComposition(landManager, economyManager, buildingManager, transportManager, roadManager, inputManager, resourceManager, productionManager, gameClockManager, cityGrowthManager, simulation, uiService);

        DebugLogger.LogServices("DIContainer.InitializeAll: Zentrale DI-Verdrahtung abgeschlossen!");
    }

    /// <summary>
    /// Registriert Service nur Named (für GDScript UI-Bridge).
    /// KEINE Typed-Registration mehr.
    /// </summary>
    private void RegisterForUI(ServiceContainer sc, string name, Node? service)
    {
        if (sc == null || service == null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            sc.RegisterNamedService(name, service);
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices(() => $"DIContainer.RegisterForUI: Fehler bei '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Registriert Service als Interface für typsichere DI.
    /// Ermöglicht Interface-basierte Dependencies und Testbarkeit.
    /// </summary>
    private void RegisterInterface<TInterface>(ServiceContainer sc, TInterface? service)
        where TInterface : class
    {
        if (sc == null || service == null)
        {
            return;
        }

        try
        {
            // ServiceContainer.RegisterNamedService is not generic, cast to Node
            sc.RegisterNamedService(typeof(TInterface).Name, (Node)(object)service);
            DebugLogger.LogServices(() => $"DIContainer: Registered interface {typeof(TInterface).Name}");
        }
        catch (Exception ex)
        {
            DebugLogger.LogServices(() => $"DIContainer.RegisterInterface: Fehler bei '{typeof(TInterface).Name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Validiert, dass alle kritischen Manager erfolgreich initialisiert wurden.
    /// Fail-fast Ansatz: Wenn ein Manager fehlt, stoppen wir das Spiel sofort.
    /// </summary>
    private void ValidateComposition(
        LandManager? landManager,
        EconomyManager? economyManager,
        BuildingManager? buildingManager,
        TransportManager? transportManager,
        RoadManager? roadManager,
        InputManager? inputManager,
        ResourceManager? resourceManager,
        ProductionManager? productionManager,
        GameClockManager? gameClockManager,
        CityGrowthManager? cityGrowthManager,
        Simulation? simulation,
        UIService? uiService)
    {
        var errors = new System.Collections.Generic.List<string>();

        // Kritische Manager (MÜSSEN vorhanden sein)
        if (landManager == null)
        {
            errors.Add("LandManager");
        }

        if (economyManager == null)
        {
            errors.Add("EconomyManager");
        }

        if (buildingManager == null)
        {
            errors.Add("BuildingManager");
        }

        if (resourceManager == null)
        {
            errors.Add("ResourceManager");
        }

        if (productionManager == null)
        {
            errors.Add("ProductionManager");
        }

        if (simulation == null)
        {
            errors.Add("Simulation");
        }

        // Optionale Manager (Warning statt Error)
        if (transportManager == null)
        {
            GD.PushWarning("DIContainer: TransportManager nicht gefunden (optional)");
        }

        if (roadManager == null)
        {
            GD.PushWarning("DIContainer: RoadManager nicht gefunden (optional)");
        }

        if (inputManager == null)
        {
            GD.PushWarning("DIContainer: InputManager nicht gefunden (optional)");
        }

        if (gameClockManager == null)
        {
            GD.PushWarning("DIContainer: GameClockManager nicht gefunden (optional)");
        }

        if (cityGrowthManager == null)
        {
            GD.PushWarning("DIContainer: CityGrowthManager nicht gefunden (optional)");
        }

        if (uiService == null)
        {
            GD.PushWarning("DIContainer: UIService nicht gefunden (optional)");
        }

        // Fail-fast wenn kritische Manager fehlen
        if (errors.Count > 0)
        {
            var msg = $"DIContainer: KRITISCHER FEHLER - Folgende Manager fehlen: {string.Join(", ", errors)}";
            DebugLogger.Error("debug_services", "DICompositionError", msg);
            DebugLogger.Error("debug_services", "DICompositionFatal", "DI-Composition fehlgeschlagen! Spiel kann nicht korrekt funktionieren.");

            // Optional: Exception werfen oder Spiel stoppen
            throw new System.InvalidOperationException(msg);
        }

        DebugLogger.LogServices("DIContainer.ValidateComposition: Alle kritischen Manager erfolgreich initialisiert ✓");
    }

    // --- Deferred retry to wait for Database in export builds ---
    private bool retryScheduled = false;

    private void WaitForDatabaseIfMissing(ServiceContainer sc)
    {
        try
        {
            var db = sc.GetNamedService<Database>(ServiceNames.Database);
            if (db == null && !this.retryScheduled)
            {
                this.retryScheduled = true;
                GD.Print("DIContainer: Database not ready yet. Deferring InitializeAll by 1 frame.");
                this.CallDeferred(nameof(this.RetryInitializeAll));
            }
        }
        catch
        {
        }
    }

    private void RetryInitializeAll()
    {
        this.retryScheduled = false;
        try
        {
            this.InitializeAll();
        }
        catch
        {
        }
    }
}

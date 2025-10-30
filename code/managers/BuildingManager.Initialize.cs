// SPDX-License-Identifier: MIT
using System;
using Godot;

public partial class BuildingManager
{
    /// <summary>
    /// Explicit DI initialization for core manager to avoid Service-Locator usage.
    /// Deutsche Logs/Kommentare gem. Richtlinie.
    /// </summary>
    public void Initialize(LandManager landManager, IEconomyManager economyManager, ISceneGraph sceneGraph, Database? database = null, EventHub? eventHub = null, IProductionManager? productionManager = null, Simulation? simulation = null, GameTimeManager? gameTimeManager = null, IRoadManager? roadManager = null, Node? dataIndex = null)
    {
        if (landManager == null)
        {
            throw new ArgumentNullException(nameof(landManager));
        }

        if (economyManager == null)
        {
            throw new ArgumentNullException(nameof(economyManager));
        }

        if (sceneGraph == null)
        {
            throw new ArgumentNullException(nameof(sceneGraph));
        }

        if (database == null)
        {
            throw new ArgumentNullException(nameof(database), "BuildingManager requires Database for BuildingFactory and PlacementService");
        }

        this.landManager = landManager;
        this.economyManager = (EconomyManager)economyManager; // Cast for storage (will be replaced with interface field later)
        this.sceneGraph = sceneGraph;
        this.database = database;
        this.eventHub = eventHub;
        this.simulation = simulation;
        this.gameTimeManager = gameTimeManager;
        this.dataIndex = dataIndex;

        // Ports für testbare Kernlogik verwenden
        var economyPort = new EconomyPort((EconomyManager)economyManager); // Port needs concrete type (will be refactored later)
        var roadPort = roadManager != null ? new RoadReadModelPort((RoadManager)roadManager) : null; // Port needs concrete type
        var defProvider = new DatabaseBuildingDefinitionProvider(database);
        this.placementService = new PlacementService(this.landManager, economyPort, defProvider, roadPort);
        this.buildingFactory = new BuildingFactory(database, (ProductionManager?)productionManager, (EconomyManager)economyManager, this.eventHub, this.simulation, this.gameTimeManager, dataIndex);

        this.initialized = true;
        DebugLogger.LogServices("BuildingManager.Initialize(): Abhaengigkeiten gesetzt (typed DI)");
    }

    /// <summary>
    /// Sets ProductionManager reference after initialization (breaks circular dependency).
    /// Called by DIContainer after ProductionManager is initialized.
    /// Also registers all existing production buildings with ProductionManager.
    /// </summary>
    public void SetProductionManager(IProductionManager? productionManager)
    {
        var pm = (ProductionManager?)productionManager;

        // Recreate BuildingFactory with ProductionManager (using stored simulation & gameTimeManager)
        this.buildingFactory = new BuildingFactory(this.database, pm, this.economyManager, this.eventHub, this.simulation, this.gameTimeManager, this.dataIndex);
        DebugLogger.LogServices("BuildingManager.SetProductionManager(): BuildingFactory neu erstellt mit ProductionManager, Simulation und GameTimeManager");

        // Register all existing production buildings with ProductionManager
        if (pm != null)
        {
            int registeredCount = 0;
            foreach (var building in this.Buildings)
            {
                if (building is IProducer producer)
                {
                    pm.RegisterProducer(producer);
                    registeredCount++;
                }
            }
            DebugLogger.LogServices($"BuildingManager.SetProductionManager(): {registeredCount} existing production buildings registered");
        }

        // Re-initialize all existing Cities with GameTimeManager
        if (this.gameTimeManager != null)
        {
            int citiesUpdated = 0;
            foreach (var city in this.Cities)
            {
                try
                {
                    city.Initialize(this.eventHub, this.gameTimeManager, this.simulation);
                    citiesUpdated++;
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("debug_building", "CityReinitFailed", $"Failed to re-initialize city: {ex.Message}");
                }
            }
            DebugLogger.LogServices($"BuildingManager.SetProductionManager(): {citiesUpdated} existing cities re-initialized with GameTimeManager");
        }
    }
}


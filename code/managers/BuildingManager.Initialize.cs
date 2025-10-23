// SPDX-License-Identifier: MIT
using System;
using Godot;

public partial class BuildingManager
{
    /// <summary>
    /// Explicit DI initialization for core manager to avoid Service-Locator usage.
    /// Deutsche Logs/Kommentare gem. Richtlinie.
    /// </summary>
    public void Initialize(LandManager landManager, EconomyManager economyManager, Database? database = null, EventHub? eventHub = null, ProductionManager? productionManager = null, Simulation? simulation = null, GameTimeManager? gameTimeManager = null, RoadManager? roadManager = null)
    {
        if (landManager == null)
        {
            throw new ArgumentNullException(nameof(landManager));
        }

        if (economyManager == null)
        {
            throw new ArgumentNullException(nameof(economyManager));
        }

        this.landManager = landManager;
        this.economyManager = economyManager;
        this.database = database;
        this.eventHub = eventHub;

        this.placementService = new PlacementService(this.landManager, this.economyManager, this.database, roadManager);
        this.buildingFactory = new BuildingFactory(this.database, productionManager, this.economyManager, this.eventHub, simulation, gameTimeManager);

        this.initialized = true;
        DebugLogger.LogServices("BuildingManager.Initialize(): Abhaengigkeiten gesetzt (typed DI)");
    }
}


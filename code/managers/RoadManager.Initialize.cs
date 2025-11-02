// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// RoadManager: Explizite Dependency Injection (neue DI-Architektur).
/// </summary>
public partial class RoadManager
{
    private bool initialized;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(LandManager landManager, IBuildingManager buildingManager, IEconomyManager economyManager, ISceneGraph sceneGraph, EventHub? eventHub, CameraController? camera, Node? dataIndex)
    {
        if (this.initialized)
        {
            DebugLogger.LogRoad(() => "RoadManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.landManager = landManager;
        this.buildingManager = (BuildingManager)buildingManager; // Cast for storage (will be replaced with interface field later)
        this.economyManager = (EconomyManager)economyManager; // Cast for storage (will be replaced with interface field later)
        this.sceneGraph = sceneGraph;
        this.eventHub = eventHub;

        // Grid und Sub-Systeme initialisieren
        this.grid = new RoadGrid(landManager.GridW, landManager.GridH);
        this.pathfinder = new RoadPathfinder(this.grid, this.buildingManager.TileSize, this.MaxNearestRoadRadius, this.EnablePathDebug, this.UseQuadtreeNearest);
        this.renderer = new RoadRenderer();
        this.sceneGraph.AddChild(this.renderer);
        this.renderer.Init(this.grid, this.buildingManager, dataIndex);

        if (camera != null)
        {
            try
            {
                this.renderer.SetCamera(camera);
            }
            catch
            {
            }
        }

        this.initialized = true;
        DebugLogger.LogRoad(() => $"RoadManager.Initialize(): Initialisiert OK (Land={landManager != null}, Building={buildingManager != null}, Economy={economyManager != null})");
    }
}

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
    public void Initialize(LandManager landManager, BuildingManager buildingManager, EconomyManager economyManager, ISceneGraph sceneGraph, EventHub? eventHub, CameraController? camera)
    {
        if (this.initialized)
        {
            DebugLogger.LogRoad(() => "RoadManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.landManager = landManager;
        this.buildingManager = buildingManager;
        this.economyManager = economyManager;
        this.sceneGraph = sceneGraph;
        this.eventHub = eventHub;

        // Grid und Sub-Systeme initialisieren
        this.grid = new RoadGrid(landManager.GridW, landManager.GridH);
        this.pathfinder = new RoadPathfinder(this.grid, buildingManager.TileSize, this.MaxNearestRoadRadius, this.EnablePathDebug, this.UseQuadtreeNearest);
        this.renderer = new RoadRenderer();
        this.sceneGraph.AddChild(this.renderer);
        this.renderer.Init(this.grid, buildingManager);

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

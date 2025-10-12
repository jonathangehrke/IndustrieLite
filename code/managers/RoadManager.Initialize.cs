// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// RoadManager: Explizite Dependency Injection (neue DI-Architektur)
/// </summary>
public partial class RoadManager
{
    private bool _initialized;

    /// <summary>
    /// Explizite Dependency Injection (neue Architektur).
    /// Wird von DIContainer.InitializeAll() aufgerufen.
    /// </summary>
    public void Initialize(LandManager landManager, BuildingManager buildingManager, EconomyManager economyManager, EventHub? eventHub, CameraController? camera)
    {
        if (_initialized)
        {
            DebugLogger.LogRoad(() => "RoadManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.landManager = landManager;
        this.buildingManager = buildingManager;
        this.economyManager = economyManager;
        this.eventHub = eventHub;

        // Grid und Sub-Systeme initialisieren
        grid = new RoadGrid(landManager.GridW, landManager.GridH);
        pathfinder = new RoadPathfinder(grid, buildingManager.TileSize, MaxNearestRoadRadius, EnablePathDebug, UseQuadtreeNearest);
        renderer = new RoadRenderer();
        AddChild(renderer);
        renderer.Init(grid, buildingManager);

        if (camera != null)
        {
            try { renderer.SetCamera(camera); } catch { }
        }

        _initialized = true;
        DebugLogger.LogRoad(() => $"RoadManager.Initialize(): Initialisiert OK (Land={landManager != null}, Building={buildingManager != null}, Economy={economyManager != null})");
    }
}

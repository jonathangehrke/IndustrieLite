// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Map - Explicit DI Initialization
/// Dependencies werden via Initialize() injiziert statt via ServiceContainer lookup
/// </summary>
public partial class Map : Node2D
{
    /// <summary>
    /// Explizite Initialisierung mit allen Dependencies.
    /// Ersetzt ServiceContainer lookups in EnsureCameraConnected().
    /// </summary>
    public void Initialize(GameManager? gameManager, CameraController? cameraController)
    {
        this.game = gameManager;
        this.camera = cameraController;

        if (gameManager == null)
            DebugLogger.LogServices("Map: WARNING - GameManager not found");
        if (cameraController == null)
            DebugLogger.LogServices("Map: WARNING - CameraController not found");

        // Hook up camera if available
        if (camera != null && !cameraHooked)
        {
            _abos.VerbindeSignal(camera, CameraController.SignalName.CameraViewChanged, this, nameof(OnCameraViewChanged));
            cameraHooked = true;
        }
    }
}

// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Werkzeug zum Abriss (Strassen/Gebaeude).
/// </summary>
public class DemolishTool : IInputTool
{
    private readonly RoadManager roadManager;
    private readonly BuildingManager buildingManager;
    private readonly UIService? uiService;

    public DemolishTool(RoadManager roadManager, BuildingManager buildingManager, UIService? uiService = null)
    {
        this.roadManager = roadManager;
        this.buildingManager = buildingManager;
        this.uiService = uiService;
    }

    /// <inheritdoc/>
    public void Enter()
    {
        DebugLogger.LogInput("DemolishTool aktiviert");
    }

    /// <inheritdoc/>
    public void Exit()
    {
        DebugLogger.LogInput("DemolishTool deaktiviert");
    }

    /// <inheritdoc/>
    public void OnClick(Vector2I zelle)
    {
        // Erst Strassen
        if (this.roadManager != null && this.roadManager.IsRoad(zelle))
        {
            var res = this.roadManager.TryRemoveRoad(zelle);
            if (!res.Ok)
            {
                if (res.ErrorInfo != null)
                {
                    this.uiService?.ShowErrorToast(res.ErrorInfo);
                }

                return;
            }
            DebugLogger.LogInput(() => $"Strasse entfernt bei {zelle}");
            {
                this.uiService?.ShowSuccessToast($"Strasse entfernt bei {zelle}");
            }
            return;
        }
        // Dann Gebaeude
        var gebaeude = this.buildingManager.GetBuildingAt(zelle);
        if (gebaeude != null)
        {
            var res = this.buildingManager.TryRemoveBuilding(gebaeude);
            if (!res.Ok)
            {
                if (res.ErrorInfo != null)
                {
                    this.uiService?.ShowErrorToast(res.ErrorInfo);
                }

                return;
            }
            DebugLogger.LogInput(() => $"Gebaeude entfernt bei {zelle}");
            {
                this.uiService?.ShowSuccessToast($"Gebaeude entfernt bei {zelle}");
            }
            return;
        }
        DebugLogger.LogInput(() => $"Nichts zum Abriss bei {zelle}");
    }
}

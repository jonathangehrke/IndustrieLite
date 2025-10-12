// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Werkzeug zum Abriss (Strassen/Gebaeude).
/// </summary>
public class DemolishTool : IInputTool
{
    private readonly RoadManager roadManager;
    private readonly BuildingManager buildingManager;

    public DemolishTool(RoadManager roadManager, BuildingManager buildingManager)
    {
        this.roadManager = roadManager;
        this.buildingManager = buildingManager;
    }

    public void Enter()
    {
        DebugLogger.LogInput("DemolishTool aktiviert");
    }

    public void Exit()
    {
        DebugLogger.LogInput("DemolishTool deaktiviert");
    }

    public void OnClick(Vector2I zelle)
    {
        // Erst Strassen
        if (roadManager != null && roadManager.IsRoad(zelle))
        {
            var res = roadManager.TryRemoveRoad(zelle);
            if (!res.Ok)
            {
                var ui = ServiceContainer.Instance?.GetNamedService<UIService>(ServiceNames.UIService);
                if (res.ErrorInfo != null) ui?.ShowErrorToast(res.ErrorInfo);
                return;
            }
            DebugLogger.LogInput(() => $"Strasse entfernt bei {zelle}");
            {
                var ui = ServiceContainer.Instance?.GetNamedService<UIService>(ServiceNames.UIService);
                ui?.ShowSuccessToast($"Strasse entfernt bei {zelle}");
            }
            return;
        }
        // Dann Gebaeude
        var gebaeude = buildingManager.GetBuildingAt(zelle);
        if (gebaeude != null)
        {
            var res = buildingManager.TryRemoveBuilding(gebaeude);
            if (!res.Ok)
            {
                var ui = ServiceContainer.Instance?.GetNamedService<UIService>(ServiceNames.UIService);
                if (res.ErrorInfo != null) ui?.ShowErrorToast(res.ErrorInfo);
                return;
            }
            DebugLogger.LogInput(() => $"Gebaeude entfernt bei {zelle}");
            {
                var ui = ServiceContainer.Instance?.GetNamedService<UIService>(ServiceNames.UIService);
                ui?.ShowSuccessToast($"Gebaeude entfernt bei {zelle}");
            }
            return;
        }
        DebugLogger.LogInput(() => $"Nichts zum Abriss bei {zelle}");
    }
}

// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Werkzeug fuer den Landverkauf.
/// </summary>
public class SellLandTool : IInputTool
{
    private readonly LandManager landManager;
    private readonly EconomyManager economyManager;
    private readonly Map karte;
    private readonly BuildingManager buildingManager;
    private readonly RoadManager? roadManager;

    public SellLandTool(LandManager landManager, EconomyManager economyManager, Map karte, BuildingManager buildingManager, RoadManager? roadManager)
    {
        this.landManager = landManager;
        this.economyManager = economyManager;
        this.karte = karte;
        this.buildingManager = buildingManager;
        this.roadManager = roadManager;
    }

    public void Enter()
    {
        DebugLogger.LogInput("SellLandTool aktiviert");
    }

    public void Exit()
    {
        DebugLogger.LogInput("SellLandTool deaktiviert");
    }

    public void OnClick(Vector2I zelle)
    {
        DebugLogger.LogInput(() => $"SellLandTool: versuche Land bei {zelle} zu verkaufen");
        var res = landManager.TrySellLand(zelle, economyManager, buildingManager, roadManager);
        if (!res.Ok)
        {
            var ui = ServiceContainer.Instance?.GetNamedService<UIService>(ServiceNames.UIService);
            if (res.ErrorInfo != null) ui?.ShowErrorToast(res.ErrorInfo);
            return;
        }
        // Optionales Feedback: gleiches visuelles Feedback wie beim Kauf nutzen
        karte?.TriggerPurchaseFeedback(zelle);
        {
            var ui = ServiceContainer.Instance?.GetNamedService<UIService>(ServiceNames.UIService);
            ui?.ShowSuccessToast($"Land verkauft bei {zelle}");
        }
    }
}

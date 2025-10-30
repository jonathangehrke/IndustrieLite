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
    private readonly UIService? uiService;

    public SellLandTool(LandManager landManager, EconomyManager economyManager, Map karte, BuildingManager buildingManager, RoadManager? roadManager, UIService? uiService = null)
    {
        this.landManager = landManager;
        this.economyManager = economyManager;
        this.karte = karte;
        this.buildingManager = buildingManager;
        this.roadManager = roadManager;
        this.uiService = uiService;
    }

    /// <inheritdoc/>
    public void Enter()
    {
        DebugLogger.LogInput("SellLandTool aktiviert");
    }

    /// <inheritdoc/>
    public void Exit()
    {
        DebugLogger.LogInput("SellLandTool deaktiviert");
    }

    /// <inheritdoc/>
    public void OnClick(Vector2I zelle)
    {
        DebugLogger.LogInput(() => $"SellLandTool: versuche Land bei {zelle} zu verkaufen");
        var res = this.landManager.TrySellLand(zelle, this.economyManager, this.buildingManager, this.roadManager);
        if (!res.Ok)
        {
            if (res.ErrorInfo != null)
            {
                this.uiService?.ShowErrorToast(res.ErrorInfo);
            }

            return;
        }
        // Optionales Feedback: gleiches visuelles Feedback wie beim Kauf nutzen
        this.karte?.TriggerPurchaseFeedback(zelle);
        {
            this.uiService?.ShowSuccessToast($"Land verkauft bei {zelle}");
        }
    }
}

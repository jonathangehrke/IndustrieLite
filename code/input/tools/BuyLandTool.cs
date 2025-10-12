// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Werkzeug fuer den Landkauf.
/// </summary>
public class BuyLandTool : IInputTool
{
    private readonly LandManager landManager;
    private readonly EconomyManager economyManager;
    private readonly Map karte;

    public BuyLandTool(LandManager landManager, EconomyManager economyManager, Map karte)
    {
        this.landManager = landManager;
        this.economyManager = economyManager;
        this.karte = karte;
    }

    public void Enter()
    {
        DebugLogger.LogInput("BuyLandTool aktiviert");
    }

    public void Exit()
    {
        DebugLogger.LogInput("BuyLandTool deaktiviert");
    }

    public void OnClick(Vector2I zelle)
    {
        DebugLogger.LogInput(() => $"BuyLandTool: versuche Land bei {zelle} zu kaufen");
        if (landManager.BuyLand(zelle, economyManager))
        {
            karte?.TriggerPurchaseFeedback(zelle);
        }
    }
}

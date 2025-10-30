// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Werkzeug fuer das Platzieren von Gebaeuden.
/// </summary>
public class BuildTool : IInputTool
{
    // Abhaengigkeiten (injiziert vom InputManager)
    private readonly LandManager landManager;
    private readonly BuildingManager buildingManager;
    private readonly EconomyManager economyManager;
    private readonly RoadManager roadManager;
    private readonly UIService? uiService;

    // Aktueller Bautyp (z. B. "House", "Road", ...)
    public string AktuellerBautyp { get; set; } = string.Empty;

    public BuildTool(LandManager landManager, BuildingManager buildingManager, EconomyManager economyManager, RoadManager roadManager, UIService? uiService = null)
    {
        this.landManager = landManager;
        this.buildingManager = buildingManager;
        this.economyManager = economyManager;
        this.roadManager = roadManager;
        this.uiService = uiService;
    }

    /// <inheritdoc/>
    public void Enter()
    {
        DebugLogger.LogInput($"BuildTool aktiviert (Typ: {this.AktuellerBautyp})");
    }

    /// <inheritdoc/>
    public void Exit()
    {
        DebugLogger.LogInput("BuildTool deaktiviert");
    }

    /// <inheritdoc/>
    public void OnClick(Vector2I zelle)
    {
        if (string.IsNullOrEmpty(this.AktuellerBautyp))
        {
            DebugLogger.LogInput("Kein Bautyp gesetzt");
            return;
        }

        DebugLogger.LogInput($"BuildTool: versuche '{this.AktuellerBautyp}' bei Zelle {zelle} zu platzieren");

        // Spezialfall: Strasse (kanonisch)
        var kanon = IdMigration.ToCanonical(this.AktuellerBautyp);
        if (string.Equals(kanon, "road", System.StringComparison.Ordinal))
        {
            if (this.roadManager == null)
            {
                DebugLogger.LogInput("RoadManager nicht gefunden");
                return;
            }
            var res = this.roadManager.TryPlaceRoad(zelle);
            if (!res.Ok)
            {
                if (res.ErrorInfo != null)
                {
                    this.uiService?.ShowErrorToast(res.ErrorInfo);
                }

                return;
            }
            {
                DebugLogger.LogInput("Strasse platziert");
                this.uiService?.ShowSuccessToast($"Strasse platziert bei {zelle}");
            }
            return;
        }

        // Fruehvalidierung gegen Database entfällt in DI-Pfad; BuildingManager.CanPlace validiert konsistent
        var canEx = this.buildingManager.CanPlaceEx(this.AktuellerBautyp, zelle);
        if (!canEx.Ok)
        {
            DebugLogger.LogInput($"Kann '{this.AktuellerBautyp}' nicht bei {zelle} platzieren");
            this.uiService?.ShowErrorToast(canEx.ErrorInfo ?? new ErrorInfo(ErrorIds.BuildingInvalidPlacementName, "Platzierung nicht moeglich"));
            return;
        }

        // Kosten ermitteln (fuer Debit) – einfache Abfrage ueber CanPlace, da es Size/Cost liefert
        if (!this.buildingManager.CanPlace(this.AktuellerBautyp, zelle, out var groesse, out var kosten))
        {
            // Sollte nicht vorkommen, da CanPlaceEx bereits true meldete, aber sicherheitshalber
            this.uiService?.ShowErrorToast(new ErrorInfo(ErrorIds.BuildingInvalidPlacementName, "Platzierung nicht moeglich"));
            return;
        }

        DebugLogger.LogInput($"Platzierung moeglich, Kosten: {kosten}");
        var debit = this.economyManager.TryDebit(kosten);
        if (!debit.Ok)
        {
            this.uiService?.ShowErrorToast(debit.ErrorInfo ?? new ErrorInfo(ErrorIds.EconomyInsufficientFundsName, "Nicht genug Geld"));
            return;
        }

        var place = this.buildingManager.TryPlaceBuilding(this.AktuellerBautyp, zelle);
        if (!place.Ok)
        {
            // Rückzahlung, falls Platzierung unerwartet fehlschlägt
            this.economyManager.TryCredit(kosten);
            this.uiService?.ShowErrorToast(place.ErrorInfo ?? new ErrorInfo(ErrorIds.SystemUnexpectedExceptionName, "Platzierung fehlgeschlagen"));
            return;
        }

        DebugLogger.LogInput("Gebaeude platziert!");
        {
            this.uiService?.ShowSuccessToast($"Gebaeude platziert bei {zelle}");
        }
    }
}


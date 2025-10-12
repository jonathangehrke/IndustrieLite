// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Koordiniert die Lebenszyklen aller Werkzeuge und verwaltet den Input-Modus.
/// </summary>
public partial class ToolManager : Node
{
    private LandManager landManager = default!;
    private BuildingManager buildingManager = default!;
    private EconomyManager economyManager = default!;
    private TransportManager transportManager = default!;
    private RoadManager? roadManager;
    private Map map = default!;
    private EventHub? eventHub;

    private BuildTool? buildTool;
    private BuyLandTool? buyLandTool;
    private TransportTool? transportTool;
    private DemolishTool? demolishTool;
    private SellLandTool? sellLandTool;
    private IInputTool? aktuellesWerkzeug;

    private bool signaleAktiv = true;
    private bool initialisiert;

    public InputManager.InputMode CurrentMode { get; private set; } = InputManager.InputMode.None;
    public string CurrentBuildType { get; private set; } = string.Empty;

    /// <summary>
    /// Injiziert alle benoetigten Manager-Referenzen und erstellt die Werkzeuge.
    /// </summary>
    public void InjiziereDependencies(
        LandManager landManager,
        BuildingManager buildingManager,
        EconomyManager economyManager,
        TransportManager transportManager,
        RoadManager? roadManager,
        Map map,
        EventHub? eventHub)
    {
        this.landManager = landManager;
        this.buildingManager = buildingManager;
        this.economyManager = economyManager;
        this.transportManager = transportManager;
        this.roadManager = roadManager;
        this.map = map;
        this.eventHub = eventHub;

        ErzeugeWerkzeuge();
        initialisiert = true;
        DebugLogger.LogInput("ToolManager: Abhaengigkeiten injiziert");
    }

    /// <summary>
    /// Aktiviert oder deaktiviert das Senden von EventHub-Signalen.
    /// </summary>
    public void SetzeSignaleAktiv(bool aktiv)
    {
        signaleAktiv = aktiv;
    }

    /// <summary>
    /// Liefert das aktuell aktive Werkzeug (falls vorhanden).
    /// </summary>
    public IInputTool? HoleAktuellesWerkzeug()
    {
        return aktuellesWerkzeug;
    }

    public void SetMode(InputManager.InputMode mode, string buildType = "")
    {
        if (!initialisiert)
        {
            DebugLogger.LogInput("ToolManager: SetMode aufgerufen bevor Abhaengigkeiten gesetzt wurden");
            return;
        }

        aktuellesWerkzeug?.Exit();

        CurrentMode = mode;
        CurrentBuildType = mode == InputManager.InputMode.Build ? buildType ?? string.Empty : string.Empty;

        switch (mode)
        {
            case InputManager.InputMode.Build:
                if (buildTool != null)
                {
                    buildTool.AktuellerBautyp = CurrentBuildType;
                    aktuellesWerkzeug = buildTool;
                }
                else
                {
                    DebugLogger.LogInput("ToolManager: BuildTool nicht verfuegbar");
                    CurrentMode = InputManager.InputMode.None;
                    CurrentBuildType = string.Empty;
                    aktuellesWerkzeug = null;
                }
                break;
            case InputManager.InputMode.BuyLand:
                aktuellesWerkzeug = buyLandTool;
                break;
            case InputManager.InputMode.SellLand:
                aktuellesWerkzeug = sellLandTool;
                break;
            case InputManager.InputMode.Transport:
                aktuellesWerkzeug = transportTool;
                break;
            case InputManager.InputMode.Demolish:
                aktuellesWerkzeug = demolishTool;
                break;
            default:
                aktuellesWerkzeug = null;
                break;
        }

        aktuellesWerkzeug?.Enter();
        SendeModeSignal();
        map?.RequestRedraw();
        DebugLogger.LogInput(() => $"Input mode gewechselt zu: {CurrentMode} {(string.IsNullOrEmpty(CurrentBuildType) ? string.Empty : "(" + CurrentBuildType + ")")}");
    }

    public void SetBuildMode(string buildId)
    {
        SetMode(InputManager.InputMode.Build, buildId);
    }

    public void ToggleDemolishModus()
    {
        if (CurrentMode == InputManager.InputMode.Demolish)
        {
            SetMode(InputManager.InputMode.None);
            DebugLogger.LogInput("Demolish mode OFF");
        }
        else
        {
            SetMode(InputManager.InputMode.Demolish);
            DebugLogger.LogInput("Demolish mode ON");
        }
    }

    private void ErzeugeWerkzeuge()
    {
        if (roadManager == null)
        {
            DebugLogger.LogInput("ToolManager: RoadManager fehlt - Strassenfunktionen eingeschraenkt");
        }

        if (roadManager != null)
        {
            buildTool = new BuildTool(landManager, buildingManager, economyManager, roadManager);
            demolishTool = new DemolishTool(roadManager, buildingManager);
        }

        buyLandTool = new BuyLandTool(landManager, economyManager, map);
        transportTool = new TransportTool(transportManager);
        sellLandTool = new SellLandTool(landManager, economyManager, map, buildingManager, roadManager);

        aktuellesWerkzeug = null;
    }

    private void SendeModeSignal()
    {
        if (!signaleAktiv || eventHub == null)
        {
            return;
        }

        try
        {
            eventHub.EmitSignal(EventHub.SignalName.InputModeChanged, CurrentMode.ToString(), CurrentBuildType);
        }
        catch
        {
            DebugLogger.LogInput("ToolManager: Konnte InputModeChanged Signal nicht senden");
        }
    }
}

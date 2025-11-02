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
    private UIService? uiService;

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
        EventHub? eventHub,
        UIService? uiService = null)
    {
        this.landManager = landManager;
        this.buildingManager = buildingManager;
        this.economyManager = economyManager;
        this.transportManager = transportManager;
        this.roadManager = roadManager;
        this.map = map;
        this.eventHub = eventHub;
        this.uiService = uiService;

        this.ErzeugeWerkzeuge();
        this.initialisiert = true;
        DebugLogger.LogInput("ToolManager: Abhaengigkeiten injiziert");
    }

    /// <summary>
    /// Aktiviert oder deaktiviert das Senden von EventHub-Signalen.
    /// </summary>
    public void SetzeSignaleAktiv(bool aktiv)
    {
        this.signaleAktiv = aktiv;
    }

    /// <summary>
    /// Liefert das aktuell aktive Werkzeug (falls vorhanden).
    /// </summary>
    /// <returns></returns>
    public IInputTool? HoleAktuellesWerkzeug()
    {
        return this.aktuellesWerkzeug;
    }

    public void SetMode(InputManager.InputMode mode, string buildType = "")
    {
        if (!this.initialisiert)
        {
            DebugLogger.LogInput("ToolManager: SetMode aufgerufen bevor Abhaengigkeiten gesetzt wurden");
            return;
        }

        this.aktuellesWerkzeug?.Exit();

        this.CurrentMode = mode;
        this.CurrentBuildType = mode == InputManager.InputMode.Build ? buildType ?? string.Empty : string.Empty;

        switch (mode)
        {
            case InputManager.InputMode.Build:
                if (this.buildTool != null)
                {
                    this.buildTool.AktuellerBautyp = this.CurrentBuildType;
                    this.aktuellesWerkzeug = this.buildTool;
                }
                else
                {
                    DebugLogger.LogInput("ToolManager: BuildTool nicht verfuegbar");
                    this.CurrentMode = InputManager.InputMode.None;
                    this.CurrentBuildType = string.Empty;
                    this.aktuellesWerkzeug = null;
                }
                break;
            case InputManager.InputMode.BuyLand:
                this.aktuellesWerkzeug = this.buyLandTool;
                break;
            case InputManager.InputMode.SellLand:
                this.aktuellesWerkzeug = this.sellLandTool;
                break;
            case InputManager.InputMode.Transport:
                this.aktuellesWerkzeug = this.transportTool;
                break;
            case InputManager.InputMode.Demolish:
                this.aktuellesWerkzeug = this.demolishTool;
                break;
            default:
                this.aktuellesWerkzeug = null;
                break;
        }

        this.aktuellesWerkzeug?.Enter();
        this.SendeModeSignal();
        this.map?.RequestRedraw();
        DebugLogger.LogInput(() => $"Input mode gewechselt zu: {this.CurrentMode} {(string.IsNullOrEmpty(this.CurrentBuildType) ? string.Empty : "(" + this.CurrentBuildType + ")")}");
    }

    public void SetBuildMode(string buildId)
    {
        this.SetMode(InputManager.InputMode.Build, buildId);
    }

    public void ToggleDemolishModus()
    {
        if (this.CurrentMode == InputManager.InputMode.Demolish)
        {
            this.SetMode(InputManager.InputMode.None);
            DebugLogger.LogInput("Demolish mode OFF");
        }
        else
        {
            this.SetMode(InputManager.InputMode.Demolish);
            DebugLogger.LogInput("Demolish mode ON");
        }
    }

    private void ErzeugeWerkzeuge()
    {
        if (this.roadManager == null)
        {
            DebugLogger.LogInput("ToolManager: RoadManager fehlt - Strassenfunktionen eingeschraenkt");
        }

        if (this.roadManager != null)
        {
            this.buildTool = new BuildTool(this.landManager, this.buildingManager, this.economyManager, this.roadManager, this.uiService);
            this.demolishTool = new DemolishTool(this.roadManager, this.buildingManager, this.uiService);
        }

        this.buyLandTool = new BuyLandTool(this.landManager, this.economyManager, this.map);
        this.transportTool = new TransportTool(this.transportManager);
        this.sellLandTool = new SellLandTool(this.landManager, this.economyManager, this.map, this.buildingManager, this.roadManager, this.uiService);

        this.aktuellesWerkzeug = null;
    }

    private void SendeModeSignal()
    {
        if (!this.signaleAktiv || this.eventHub == null)
        {
            return;
        }

        try
        {
            this.eventHub.EmitSignal(EventHub.SignalName.InputModeChanged, this.CurrentMode.ToString(), this.CurrentBuildType);
        }
        catch
        {
            DebugLogger.LogInput("ToolManager: Konnte InputModeChanged Signal nicht senden");
        }
    }
}

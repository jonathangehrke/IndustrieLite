// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

/// <summary>
/// Verarbeitet die vom InputHandler erzeugten Befehle und integriert sie in die Simulation.
/// </summary>
public partial class InputEventRouter : Node, ITickable
{
    public enum EingabeBefehlTyp
    {
        ModusAbbrechenEsc,
        ModusAbbrechenRechtsklick,
        ToggleDemolish,
        ZoomSchritt,
        MausKlick,
        CameraBewegen,
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct EingabeBefehl
    {
        public EingabeBefehl(EingabeBefehlTyp typ)
        {
            this.Typ = typ;
            this.Wert = 0;
            this.Zelle = default;
            this.Richtung = Vector2.Zero;
        }

        public EingabeBefehl(EingabeBefehlTyp typ, int wert)
        {
            this.Typ = typ;
            this.Wert = wert;
            this.Zelle = default;
            this.Richtung = Vector2.Zero;
        }

        public EingabeBefehl(EingabeBefehlTyp typ, Vector2I zelle)
        {
            this.Typ = typ;
            this.Wert = 0;
            this.Zelle = zelle;
            this.Richtung = Vector2.Zero;
        }

        public EingabeBefehl(EingabeBefehlTyp typ, Vector2 richtung)
        {
            this.Typ = typ;
            this.Wert = 0;
            this.Zelle = default;
            this.Richtung = richtung;
        }

        public EingabeBefehlTyp Typ { get; }

        public int Wert { get; }

        public Vector2I Zelle { get; }

        public Vector2 Richtung { get; }
    }

    private readonly Queue<EingabeBefehl> befehle = new();
    private readonly List<int> zoomBefehle = new();

    private ToolManager? toolManager;
    private Map? map;
    private GameManager? gameManager;
    private BuildingManager? buildingManager;
    private CameraController? kameraController;
    private EventHub? eventHub;
    private Simulation? simulation;
    private bool signaleAktiv = true;
    private bool demolishTasteGedrueckt;
    private Vector2 aktuelleKameraRichtung = Vector2.Zero;

    /// <inheritdoc/>
    string ITickable.Name => "InputEventRouter";

    public void InjiziereDependencies(
        Map map,
        GameManager gameManager,
        ToolManager toolManager,
        BuildingManager buildingManager,
        CameraController? kameraController,
        EventHub? eventHub)
    {
        this.map = map;
        this.gameManager = gameManager;
        this.toolManager = toolManager;
        this.buildingManager = buildingManager;
        this.kameraController = kameraController;
        this.eventHub = eventHub;

        this.RegistrierungBeiSimulationSicherstellen();
        DebugLogger.LogInput("InputEventRouter: Abhaengigkeiten injiziert");
    }

    public void SetzeSignaleAktiv(bool aktiv)
    {
        this.signaleAktiv = aktiv;
    }

    public void FordereModusAbbrechenDurchEscAn()
    {
        this.FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.ModusAbbrechenEsc));
    }

    public void FordereModusAbbrechenDurchRechtsklickAn()
    {
        this.FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.ModusAbbrechenRechtsklick));
    }

    public void FuegeZoomSchrittHinzu(int delta)
    {
        this.FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.ZoomSchritt, delta));
    }

    public void FuegeMausKlickHinzu(Vector2I zelle)
    {
        this.FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.MausKlick, zelle));
    }

    public void MeldeKameraBewegung(Vector2 richtung)
    {
        this.FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.CameraBewegen, richtung));
    }

    public void VerarbeiteDemolishAktion(bool gedrueckt)
    {
        if (gedrueckt)
        {
            if (!this.demolishTasteGedrueckt)
            {
                this.demolishTasteGedrueckt = true;
                this.FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.ToggleDemolish));
            }
        }
        else
        {
            this.demolishTasteGedrueckt = false;
        }
    }

    public void HandleClick(Vector2I zelle)
    {
        this.RouteMausKlick(zelle);
    }

    /// <inheritdoc/>
    public void Tick(double dt)
    {
        this.zoomBefehle.Clear();

        while (this.befehle.Count > 0)
        {
            var befehl = this.befehle.Dequeue();
            switch (befehl.Typ)
            {
                case EingabeBefehlTyp.ModusAbbrechenEsc:
                case EingabeBefehlTyp.ModusAbbrechenRechtsklick:
                    this.FuehreAbbrechenAus(befehl.Typ);
                    break;
                case EingabeBefehlTyp.ToggleDemolish:
                    this.ToggleDemolishModus();
                    break;
                case EingabeBefehlTyp.ZoomSchritt:
                    this.zoomBefehle.Add(befehl.Wert);
                    break;
                case EingabeBefehlTyp.MausKlick:
                    this.RouteMausKlick(befehl.Zelle);
                    break;
                case EingabeBefehlTyp.CameraBewegen:
                    this.aktuelleKameraRichtung = befehl.Richtung;
                    break;
            }
        }

        if (this.kameraController != null)
        {
            this.kameraController.VerarbeiteSimTick(dt, this.aktuelleKameraRichtung, this.zoomBefehle);
        }

        this.zoomBefehle.Clear();
    }

    private void FuegeBefehlHinzu(EingabeBefehl befehl)
    {
        this.befehle.Enqueue(befehl);
    }

    private void RouteMausKlick(Vector2I zelle)
    {
        DebugLogger.LogInput(() => $"HandleClick Zelle: {zelle}, Modus: {this.toolManager?.CurrentMode}");

        var koordinator = this.gameManager?.ManagerCoordinator;
        bool warPotentiellerKauf = koordinator != null && this.map != null && koordinator.IsBuyLandModeActive() && !koordinator.IsOwned(zelle) && koordinator.CanBuyLand(zelle);

        // Keine Selektion starten, wenn wir im Build- oder Demolish-Modus sind
        if (this.toolManager != null &&
            this.toolManager.CurrentMode != InputManager.InputMode.Build &&
            this.toolManager.CurrentMode != InputManager.InputMode.Demolish)
        {
            this.HandleBuildingSelection(zelle);
        }

        var werkzeug = this.toolManager?.HoleAktuellesWerkzeug();
        if (werkzeug != null)
        {
            werkzeug.OnClick(zelle);
        }
        else
        {
            DebugLogger.LogInput("InputEventRouter: Kein aktives Werkzeug");
        }

        if (koordinator != null && warPotentiellerKauf && koordinator.IsOwned(zelle))
        {
            this.map?.TriggerPurchaseFeedback(zelle);
        }
    }

    private void HandleBuildingSelection(Vector2I cell)
    {
        if (this.buildingManager == null)
        {
            DebugLogger.LogInput("InputEventRouter: BuildingManager fehlt fuer Selektion");
            return;
        }

        var building = this.buildingManager.GetBuildingAt(cell);
        if (building != null)
        {
            DebugLogger.LogInput(() => $"Gebaeude ausgewaehlt: {building.Name} ({building.GetType().Name})");
            if (this.signaleAktiv && this.eventHub != null)
            {
                this.eventHub.EmitSignal(EventHub.SignalName.SelectedBuildingChanged, building);
                DebugLogger.LogInput("SelectedBuildingChanged Signal gesendet");
            }
        }
        else
        {
            DebugLogger.LogInput("Kein Gebaeude an Position");
            if (this.signaleAktiv && this.eventHub != null)
            {
                this.eventHub.EmitSignal(EventHub.SignalName.SelectedBuildingChanged, (Node)null!);
            }
        }
    }

    private void FuehreAbbrechenAus(EingabeBefehlTyp typ)
    {
        if (this.toolManager != null && this.toolManager.CurrentMode != InputManager.InputMode.None)
        {
            this.toolManager.SetMode(InputManager.InputMode.None);
            if (typ == EingabeBefehlTyp.ModusAbbrechenEsc)
            {
                DebugLogger.LogInput("ESC: Modus auf None zurueckgesetzt");
            }
            else
            {
                DebugLogger.LogInput("RightClick: Modus auf None zurueckgesetzt");
            }
        }
        else if (typ == EingabeBefehlTyp.ModusAbbrechenEsc)
        {
            // Wenn ein HUD-Panel offen ist (z.B. Markt, Land, Produktion), zuerst dieses schließen
            if (this.SchliesseOffeneHudPanels())
            {
                return;
            }

            var root = this.GetTree().Root.GetNodeOrNull<Node>("Root");
            if (root != null)
            {
                if (root.HasMethod("toggle_menu"))
                {
                    root.Call("toggle_menu");
                }
                else if (root.HasMethod("show_menu"))
                {
                    root.Call("show_menu");
                }
            }
        }
    }

    private bool SchliesseOffeneHudPanels()
    {
        var hud = this.GetTree().Root.FindChild("HUD", true, false) as Control;
        if (hud == null)
        {
            return false;
        }

        bool geschlossen = false;

        if (hud.FindChild("MarketPanel", true, false) is Control market && market.Visible)
        {
            market.Hide();
            geschlossen = true;
        }

        if (hud.FindChild("LandPanel", true, false) is Control land && land.Visible)
        {
            land.Visible = false;
            geschlossen = true;

            // Landkauf/-verkauf-Modi sauber deaktivieren
            var sc = this.GetTree().Root.GetNodeOrNull<Node>("/root/ServiceContainer");
            Node? uiService = null;
            if (sc != null)
            {
                Variant v = sc.Call("GetNamedService", "UIService");
                if (v.VariantType != Variant.Type.Nil)
                {
                    uiService = v.AsGodotObject() as Node;
                }
            }
            uiService?.Call("ToggleBuyLandMode", false);
            uiService?.Call("ToggleSellLandMode", false);
        }

        if (hud.FindChild("ProductionPanelHost", true, false) is Control prod && prod.Visible)
        {
            prod.Visible = false;
            geschlossen = true;
        }

        // Build-Menü (BuildBar + Hintergrund) schließen
        if (hud.FindChild("BuildBar", true, false) is Control buildBar && buildBar.Visible)
        {
            var buttonMgr = hud.FindChild("ButtonVerwalter", true, false);
            if (buttonMgr != null)
            {
                buttonMgr.Call("setze_bau_leiste_sichtbar", false);
            }
            else
            {
                buildBar.Visible = false;
                if (hud.FindChild("BauLeisteHintergrund", true, false) is Control bg)
                {
                    bg.Visible = false;
                }
            }
            // Verlasse Build-Modus (InputManager auf None)
            var sc = this.GetTree().Root.GetNodeOrNull<Node>("/root/ServiceContainer");
            Node? im = null;
            if (sc != null)
            {
                Variant v2 = sc.Call("GetNamedService", "InputManager");
                if (v2.VariantType != Variant.Type.Nil)
                {
                    im = v2.AsGodotObject() as Node;
                }
            }
            im?.Call("SetMode", 0, "");
            geschlossen = true;
        }

        return geschlossen;
    }

    private void ToggleDemolishModus()
    {
        this.toolManager?.ToggleDemolishModus();
    }

    private void RegistrierungBeiSimulationSicherstellen()
    {
        if (this.simulation == null)
        {
            this.simulation = Simulation.Instance;
        }
        if (this.simulation != null && !this.simulation.IsRegistered(this))
        {
            this.simulation.Register(this);
            DebugLogger.LogInput("InputEventRouter bei Simulation registriert");
        }
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        if (this.simulation != null)
        {
            this.simulation.Unregister(this);
            this.simulation = null;
        }
        base._ExitTree();
    }
}

// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

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
        CameraBewegen
    }

    public readonly struct EingabeBefehl
    {
        public EingabeBefehl(EingabeBefehlTyp typ)
        {
            Typ = typ;
            Wert = 0;
            Zelle = default;
            Richtung = Vector2.Zero;
        }

        public EingabeBefehl(EingabeBefehlTyp typ, int wert)
        {
            Typ = typ;
            Wert = wert;
            Zelle = default;
            Richtung = Vector2.Zero;
        }

        public EingabeBefehl(EingabeBefehlTyp typ, Vector2I zelle)
        {
            Typ = typ;
            Wert = 0;
            Zelle = zelle;
            Richtung = Vector2.Zero;
        }

        public EingabeBefehl(EingabeBefehlTyp typ, Vector2 richtung)
        {
            Typ = typ;
            Wert = 0;
            Zelle = default;
            Richtung = richtung;
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

        RegistrierungBeiSimulationSicherstellen();
        DebugLogger.LogInput("InputEventRouter: Abhaengigkeiten injiziert");
    }

    public void SetzeSignaleAktiv(bool aktiv)
    {
        signaleAktiv = aktiv;
    }

    public void FordereModusAbbrechenDurchEscAn()
    {
        FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.ModusAbbrechenEsc));
    }

    public void FordereModusAbbrechenDurchRechtsklickAn()
    {
        FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.ModusAbbrechenRechtsklick));
    }

    public void FuegeZoomSchrittHinzu(int delta)
    {
        FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.ZoomSchritt, delta));
    }

    public void FuegeMausKlickHinzu(Vector2I zelle)
    {
        FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.MausKlick, zelle));
    }

    public void MeldeKameraBewegung(Vector2 richtung)
    {
        FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.CameraBewegen, richtung));
    }

    public void VerarbeiteDemolishAktion(bool gedrueckt)
    {
        if (gedrueckt)
        {
            if (!demolishTasteGedrueckt)
            {
                demolishTasteGedrueckt = true;
                FuegeBefehlHinzu(new EingabeBefehl(EingabeBefehlTyp.ToggleDemolish));
            }
        }
        else
        {
            demolishTasteGedrueckt = false;
        }
    }

    public void HandleClick(Vector2I zelle)
    {
        RouteMausKlick(zelle);
    }

    public void Tick(double dt)
    {
        zoomBefehle.Clear();

        while (befehle.Count > 0)
        {
            var befehl = befehle.Dequeue();
            switch (befehl.Typ)
            {
                case EingabeBefehlTyp.ModusAbbrechenEsc:
                case EingabeBefehlTyp.ModusAbbrechenRechtsklick:
                    FuehreAbbrechenAus(befehl.Typ);
                    break;
                case EingabeBefehlTyp.ToggleDemolish:
                    ToggleDemolishModus();
                    break;
                case EingabeBefehlTyp.ZoomSchritt:
                    zoomBefehle.Add(befehl.Wert);
                    break;
                case EingabeBefehlTyp.MausKlick:
                    RouteMausKlick(befehl.Zelle);
                    break;
                case EingabeBefehlTyp.CameraBewegen:
                    aktuelleKameraRichtung = befehl.Richtung;
                    break;
            }
        }

        if (kameraController != null)
        {
            kameraController.VerarbeiteSimTick(dt, aktuelleKameraRichtung, zoomBefehle);
        }

        zoomBefehle.Clear();
    }

    private void FuegeBefehlHinzu(EingabeBefehl befehl)
    {
        befehle.Enqueue(befehl);
    }

    private void RouteMausKlick(Vector2I zelle)
    {
        DebugLogger.LogInput(() => $"HandleClick Zelle: {zelle}, Modus: {toolManager?.CurrentMode}");

        var koordinator = gameManager?.ManagerCoordinator;
        bool warPotentiellerKauf = koordinator != null && map != null && koordinator.IsBuyLandModeActive() && !koordinator.IsOwned(zelle) && koordinator.CanBuyLand(zelle);

        // Keine Selektion starten, wenn wir im Build- oder Demolish-Modus sind
        if (toolManager != null &&
            toolManager.CurrentMode != InputManager.InputMode.Build &&
            toolManager.CurrentMode != InputManager.InputMode.Demolish)
        {
            HandleBuildingSelection(zelle);
        }

        var werkzeug = toolManager?.HoleAktuellesWerkzeug();
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
            map?.TriggerPurchaseFeedback(zelle);
        }
    }

    private void HandleBuildingSelection(Vector2I cell)
    {
        if (buildingManager == null)
        {
            DebugLogger.LogInput("InputEventRouter: BuildingManager fehlt fuer Selektion");
            return;
        }

        var building = buildingManager.GetBuildingAt(cell);
        if (building != null)
        {
            DebugLogger.LogInput(() => $"Gebaeude ausgewaehlt: {building.Name} ({building.GetType().Name})");
            if (signaleAktiv && eventHub != null)
            {
                eventHub.EmitSignal(EventHub.SignalName.SelectedBuildingChanged, building);
                DebugLogger.LogInput("SelectedBuildingChanged Signal gesendet");
            }
        }
        else
        {
            DebugLogger.LogInput("Kein Gebaeude an Position");
            if (signaleAktiv && eventHub != null)
            {
                eventHub.EmitSignal(EventHub.SignalName.SelectedBuildingChanged, (Node)null!);
            }
        }
    }

    private void FuehreAbbrechenAus(EingabeBefehlTyp typ)
    {
        if (toolManager != null && toolManager.CurrentMode != InputManager.InputMode.None)
        {
            toolManager.SetMode(InputManager.InputMode.None);
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
            if (SchliesseOffeneHudPanels())
            {
                return;
            }

            var root = GetTree().Root.GetNodeOrNull<Node>("Root");
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
        var hud = GetTree().Root.FindChild("HUD", true, false) as Control;
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
            var sc = GetTree().Root.GetNodeOrNull<Node>("/root/ServiceContainer");
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
            var sc = GetTree().Root.GetNodeOrNull<Node>("/root/ServiceContainer");
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
        toolManager?.ToggleDemolishModus();
    }

    private void RegistrierungBeiSimulationSicherstellen()
    {
        if (simulation == null)
        {
            simulation = Simulation.Instance;
        }
        if (simulation != null && !simulation.IsRegistered(this))
        {
            simulation.Register(this);
            DebugLogger.LogInput("InputEventRouter bei Simulation registriert");
        }
    }

    public override void _ExitTree()
    {
        if (simulation != null)
        {
            simulation.Unregister(this);
            simulation = null;
        }
        base._ExitTree();
    }
}

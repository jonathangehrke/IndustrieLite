// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Verantwortlich fuer das Erfassen von Roh-Input und das Ableiten von Befehlen.
/// </summary>
public partial class InputHandler : Node
{
    private Map? map;
    private InputEventRouter? inputEventRouter;
    private ToolManager? toolManager;
    private Vector2 letzteKameraRichtung = Vector2.Zero;

    public override void _Ready()
    {
        SetProcess(true);
        SetProcessInput(true);
        SetProcessUnhandledInput(true);
    }

    /// <summary>
    /// Injektion aller benoetigten Referenzen nach dem Laden durch den InputManager.
    /// </summary>
    /// <param name="map">Karten-Referenz fuer Zellberechnungen.</param>
    /// <param name="router">Router fuer die Weiterleitung der Eingabebefehle.</param>
    /// <param name="toolManager">ToolManager fuer Statusabfragen.</param>
    public void InjiziereDependencies(Map map, InputEventRouter router, ToolManager toolManager)
    {
        this.map = map;
        inputEventRouter = router;
        this.toolManager = toolManager;
    }

    public override void _Input(InputEvent @event)
    {
        if (inputEventRouter == null)
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            if (@event is InputEventKey key && key.Echo)
            {
                return;
            }

            var fokus = GetViewport()?.GuiGetFocusOwner();
            if (fokus is LineEdit || fokus is TextEdit)
            {
                return;
            }

            inputEventRouter.FordereModusAbbrechenDurchEscAn();
            GetViewport()?.SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (inputEventRouter == null)
        {
            return;
        }

        if (@event is InputEventAction action)
        {
            switch (action.Action)
            {
                case "toggle_demolish":
                    inputEventRouter.VerarbeiteDemolishAktion(action.Pressed);
                    return;
                case "zoom_in":
                    if (action.Pressed)
                    {
                        inputEventRouter.FuegeZoomSchrittHinzu(-1);
                    }
                    return;
                case "zoom_out":
                    if (action.Pressed)
                    {
                        inputEventRouter.FuegeZoomSchrittHinzu(1);
                    }
                    return;
                default:
                    return;
            }
        }

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (map != null)
                {
                    var weltPos = map.GetGlobalMousePosition();
                    var zelle = map.BerechneCellVonPosition(weltPos);
                    inputEventRouter.FuegeMausKlickHinzu(zelle);
                }
                return;
            }

            if (mb.ButtonIndex == MouseButton.Right)
            {
                inputEventRouter.FordereModusAbbrechenDurchRechtsklickAn();
                return;
            }

            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                inputEventRouter.FuegeZoomSchrittHinzu(-1);
                return;
            }

            if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                inputEventRouter.FuegeZoomSchrittHinzu(1);
                return;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (inputEventRouter == null)
        {
            return;
        }

        var richtung = BerechneKameraBewegung();
        if (richtung != letzteKameraRichtung)
        {
            inputEventRouter.MeldeKameraBewegung(richtung);
            letzteKameraRichtung = richtung;
        }
    }

    private Vector2 BerechneKameraBewegung()
    {
        Vector2 richtung = Vector2.Zero;
        if (Input.IsActionPressed("move_camera_left"))
        {
            richtung.X -= 1f;
        }
        if (Input.IsActionPressed("move_camera_right"))
        {
            richtung.X += 1f;
        }
        if (Input.IsActionPressed("move_camera_up"))
        {
            richtung.Y -= 1f;
        }
        if (Input.IsActionPressed("move_camera_down"))
        {
            richtung.Y += 1f;
        }

        if (richtung != Vector2.Zero)
        {
            richtung = richtung.Normalized();
        }

        return richtung;
    }
}

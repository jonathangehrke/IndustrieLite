// SPDX-License-Identifier: MIT
using Godot;

public partial class MapVisualFeedbackSystem : Node
{
    [Export] public double AnzeigeDauer { get; set; } = 1.0; // Dauer der Visualisierung in Sekunden
    [Export] public bool DebugAusgabe { get; set; } = false; // Debug-Ausgaben an/aus

    private Vector2I? aktiveZelle;
    private double timer = 0.0;
    private Map? map;

    public Vector2I? AktuelleZelle => aktiveZelle;

    public override void _Ready()
    {
        map = GetParent() as Map;
        SetProcess(false);
    }

    public void AktiviereFeedback(Vector2I zelle)
    {
        aktiveZelle = zelle;
        timer = 0.0;
        SetProcess(true);
        map?.RequestRedraw();
        DebugLogger.LogInput(() => $"MapVisualFeedbackSystem: Feedback gestartet bei {zelle}", DebugAusgabe);
    }

    public override void _Process(double delta)
    {
        if (!aktiveZelle.HasValue)
        {
            SetProcess(false);
            return;
        }

        timer += delta;
        if (timer >= AnzeigeDauer)
        {
            var beendeteZelle = aktiveZelle.Value;
            aktiveZelle = null;
            timer = 0.0;
            SetProcess(false);
            map?.RequestRedraw();
            DebugLogger.LogInput(() => $"MapVisualFeedbackSystem: Feedback beendet (Zelle {beendeteZelle})", DebugAusgabe);
        }
    }
}

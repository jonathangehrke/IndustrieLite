// SPDX-License-Identifier: MIT
using Godot;

public partial class MapVisualFeedbackSystem : Node
{
    [Export]
    public double AnzeigeDauer { get; set; } = 1.0; // Dauer der Visualisierung in Sekunden

    [Export]
    public bool DebugAusgabe { get; set; } = false; // Debug-Ausgaben an/aus

    private Vector2I? aktiveZelle;
    private double timer = 0.0;
    private Map? map;

    public Vector2I? AktuelleZelle => this.aktiveZelle;

    /// <inheritdoc/>
    public override void _Ready()
    {
        this.map = this.GetParent() as Map;
        this.SetProcess(false);
    }

    public void AktiviereFeedback(Vector2I zelle)
    {
        this.aktiveZelle = zelle;
        this.timer = 0.0;
        this.SetProcess(true);
        this.map?.RequestRedraw();
        DebugLogger.LogInput(() => $"MapVisualFeedbackSystem: Feedback gestartet bei {zelle}", this.DebugAusgabe);
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        if (!this.aktiveZelle.HasValue)
        {
            this.SetProcess(false);
            return;
        }

        this.timer += delta;
        if (this.timer >= this.AnzeigeDauer)
        {
            var beendeteZelle = this.aktiveZelle.Value;
            this.aktiveZelle = null;
            this.timer = 0.0;
            this.SetProcess(false);
            this.map?.RequestRedraw();
            DebugLogger.LogInput(() => $"MapVisualFeedbackSystem: Feedback beendet (Zelle {beendeteZelle})", this.DebugAusgabe);
        }
    }
}

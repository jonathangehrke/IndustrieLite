// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Definition eines Produktionsrezepts (datengetrieben)
/// - I/O werden als Mengen pro Minute angegeben (siehe Amount)
/// - Zeitsteuerung über CycleSeconds (Sekunden pro Zyklus)
/// - Optionale Anforderungen/Kosten für spätere Ökonomie-Integration.
/// </summary>
[GlobalClass]
public partial class RecipeDef : Resource
{
    // Identität & Anzeige
    [Export]
    public string Id { get; set; } = "";                    // z. B. "chicken_production"

    [Export]
    public string DisplayName { get; set; } = "";            // z. B. "Hühnerproduktion"

    // I/O Definitionen
    [Export]
    public Godot.Collections.Array<Amount> Inputs { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Amount> Outputs { get; set; } = new();

    // Timing
    [Export]
    public float CycleSeconds { get; set; } = 60.0f;          // Dauer eines Produktionszyklus in Sekunden

    [Export]
    public float StartupSeconds { get; set; } = 0.0f;          // Anlaufzeit (optional)

    // Ressourcen-Anforderungen (laufend während Produktion)
    [Export]
    public float PowerRequirement { get; set; } = 0.0f;        // kW-Äquivalent

    [Export]
    public float WaterRequirement { get; set; } = 0.0f;        // l/h-Äquivalent (optional)

    // Ökonomie (optional)
    [Export]
    public float ProductionCost { get; set; } = 0.0f;          // Kosten pro Zyklus

    [Export]
    public float MaintenanceCost { get; set; } = 0.0f;         // Kosten pro Stunde

    public RecipeDef()
    {
        // Standard-Konstruktor für Godot
    }

    public RecipeDef(string id, float cycleSeconds = 60.0f)
    {
        this.Id = id;
        this.CycleSeconds = cycleSeconds;
    }
}

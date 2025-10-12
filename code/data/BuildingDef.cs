// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

/// <summary>
/// Definition eines Gebäudes
/// </summary>
[GlobalClass]
public partial class BuildingDef : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public PackedScene? Prefab { get; set; }
    [Export] public int Width { get; set; } = 1;
    [Export] public int Height { get; set; } = 1;
    [Export] public double Cost { get; set; } = 0.0;
    [Export] public string Category { get; set; } = "basic";
    [Export] public Texture2D? Icon { get; set; }
    [Export] public Godot.Collections.Array<string> Alternatives { get; set; } = new();
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
    [Export] public Godot.Collections.Array<string> LegacyIds { get; set; } = new();

    // Level-System: Mindest-Level für Freischaltung
    [Export] public int RequiredLevel { get; set; } = 1;

    // Rezept-Verknüpfung (Phase 1)
    [Export] public string DefaultRecipeId { get; set; } = "";
    [Export] public Godot.Collections.Array<string> AvailableRecipes { get; set; } = new();
    [Export] public bool AutoStartProduction { get; set; } = true;

    // Optionale Basisressourcen-Bedarfe (z. B. Arbeiter) pro Produktions-Tick
    // Wird von Gebaeuden wie der Huehnerfarm ausgelesen und in GetResourceNeeds() beruecksichtigt
    [Export] public int WorkersRequired { get; set; } = 0;

    public BuildingDef()
    {
        // Standard-Konstruktor für Godot
    }

    public BuildingDef(string id, string displayName, int width = 1, int height = 1, double cost = 0.0)
    {
        Id = id;
        DisplayName = displayName;
        Width = width;
        Height = height;
        Cost = cost;
    }
}

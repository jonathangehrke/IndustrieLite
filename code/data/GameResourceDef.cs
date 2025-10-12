// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Definition einer Spielressource (z.B. Strom, Wasser, Hühner)
/// </summary>
[GlobalClass]
public partial class GameResourceDef : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D? Icon { get; set; }
    [Export] public string Category { get; set; } = "basic";

    // Level-System: Mindest-Level für Freischaltung
    [Export] public int RequiredLevel { get; set; } = 1;
    
    public GameResourceDef()
    {
        // Standard-Konstruktor für Godot
    }
    
    public GameResourceDef(string id, string displayName, string category = "basic")
    {
        Id = id;
        DisplayName = displayName;
        Category = category;
    }
}

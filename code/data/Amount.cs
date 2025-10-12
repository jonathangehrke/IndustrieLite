// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Ressourcen-Menge mit Produktionsrate (pro Minute)
/// </summary>
[GlobalClass]
public partial class Amount : Resource
{
    [Export] public string ResourceId { get; set; } = "";
    [Export] public float PerMinute { get; set; } = 0.0f;
    
    public Amount()
    {
        // Standard-Konstruktor für Godot
    }
    
    public Amount(string resourceId, float perMinute)
    {
        ResourceId = resourceId;
        PerMinute = perMinute;
    }
}

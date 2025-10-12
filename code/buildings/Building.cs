// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// Base class for all buildings in the game
/// Handles common functionality like grid-based positioning, rendering, and inspector data
/// </summary>
public partial class Building : Node2D
{
    /// <summary>Grid position of the building in tile coordinates</summary>
    public Vector2I GridPos;

    /// <summary>Size of the building in grid tiles</summary>
    public Vector2I Size;

    /// <summary>Size of each tile in pixels</summary>
    public int TileSize = 32;

    /// <summary>Default size for buildings (2x2 tiles)</summary>
    public Vector2I DefaultSize = new Vector2I(2, 2);

    /// <summary>Visual color for building rendering</summary>
    public Color Color = new Color(0.6f, 0.6f, 0.6f, 1f);

    // Kanonische ID der Gebaeudedefinition (aus Database)
    public string DefinitionId { get; set; } = "";
    protected Database? _database;

    [Export] public string BuildingId { get; set; } = "";

    // Logistik-Upgrade-Einstellungen pro Gebaeude
    // Kapazitaet pro Truck (fuer von diesem Gebaeude erzeugte Lieferungen)
    [Export] public int LogisticsTruckCapacity { get; set; } = 5;
    // Geschwindigkeit der Trucks (Pixel/Sek) fuer Lieferungen dieses Gebaeudes
    [Export] public float LogisticsTruckSpeed { get; set; } = 32.0f;

    // Interne Grafiksteuerung: Wird das Gebaeude per Icon gerendert?
    private bool _hatIconGrafik = false;

    /// <summary>
    /// Initialize building with proper Z-index for rendering
    /// </summary>
    public override void _Ready()
    {
        if (string.IsNullOrEmpty(BuildingId))
            BuildingId = Guid.NewGuid().ToString();

        ZIndex = 1; // Buildings render above ground
        // Versuche, eine Sprite-Grafik aus der BuildingDef.Icon zu erzeugen
        ErzeugeGrafikAusIconWennVorhanden();
    }

    /// <summary>
    /// Render the building as a colored rectangle
    /// </summary>
    public override void _Draw()
    {
        // Wenn keine Icon-Grafik gesetzt ist, fallback auf farbiges Rechteck
        if (!_hatIconGrafik)
        {
            var rect = new Rect2(Vector2.Zero, new Vector2(Size.X * TileSize, Size.Y * TileSize));
            DrawRect(rect, Color);
        }
    }

    /// <summary>
    /// Trigger redraw when building enters scene tree
    /// </summary>
    public override void _EnterTree()
    {
        QueueRedraw();
    }

    /// <summary>
    /// Liefert die BuildingDef aus der zentralen Database anhand der gespeicherten DefinitionId
    /// </summary>
    public BuildingDef? GetBuildingDef()
    {
        if (_database == null)
            return null;
        if (string.IsNullOrEmpty(DefinitionId)) return null;
        return _database.GetBuilding(DefinitionId);
    }

    /// <summary>
    /// Erzeugt eine Sprite2D-Grafik aus BuildingDef.Icon (falls gesetzt) und skaliert sie auf die Baugroesse.
    /// </summary>
    private void ErzeugeGrafikAusIconWennVorhanden()
    {
        var def = GetBuildingDef();
        if (def == null || def.Icon == null)
        {
            _hatIconGrafik = false;
            return;
        }

        var tex = def.Icon;
        var size = tex.GetSize();
        if (size.X <= 0 || size.Y <= 0)
        {
            _hatIconGrafik = false;
            return;
        }

        var zielBreite = Size.X * TileSize;
        var zielHoehe = Size.Y * TileSize;
        var scaleX = (float)zielBreite / (float)size.X;
        var scaleY = (float)zielHoehe / (float)size.Y;

        var sprite = new Sprite2D();
        sprite.Texture = tex;
        sprite.Centered = false; // Oben-Links als Ursprung
        sprite.ZIndex = ZIndex;  // Gleiche Ebene wie Gebaeude
        sprite.Scale = new Vector2(scaleX, scaleY);
        AddChild(sprite);

        _hatIconGrafik = true;
    }

    // Leichtgewichtiger Presenter fuer den Inspector (Godot-kompatible Collections)
    public virtual Godot.Collections.Dictionary GetInspectorData()
    {
        var pairs = new Godot.Collections.Array<Godot.Collections.Array>
        {
            new() { "Typ", GetType().Name },
            new() { "Position", GridPos.ToString() }
        };
        return new Godot.Collections.Dictionary
        {
            { "title", Name },
            { "pairs", pairs }
        };
    }

    public virtual void SetDatabase(Database? database)
    {
        _database = database;
    }

    /// <summary>
    /// Einheitliche Initialize-Methode fuer Gebaeude (aktuell: Database setzen).
    /// </summary>
    public virtual void Initialize(Database? database)
    {
        _database = database;
    }

    /// <summary>
    /// Explizite DI fuer Gebaeude: Abhaengigkeiten injizieren (optional je nach Typ).
    /// Basisklasse macht nichts; abgeleitete Klassen ueberschreiben bei Bedarf.
    /// </summary>
    public virtual void InitializeDependencies(ProductionManager? productionManager, EconomyManager? economyManager, EventHub? eventHub)
    {
        // no-op in base
    }

    /// <summary>
    /// Hook fuer abgeleitete Klassen: Wird nach dem Wiederherstellen des Recipe-States aufgerufen.
    /// Ermoeglicht es Buildings, interne Zustaende (z.B. RezeptIdOverride) zu synchronisieren.
    /// </summary>
    public virtual void OnRecipeStateRestored(string recipeId)
    {
        // no-op in base
    }

}








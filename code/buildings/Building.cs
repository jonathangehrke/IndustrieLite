// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// Base class for all buildings in the game
/// Handles common functionality like grid-based positioning, rendering, and inspector data.
/// </summary>
public partial class Building : Node2D
{
    /// <summary>Grid position of the building in tile coordinates.</summary>
    public Vector2I GridPos;

    /// <summary>Size of the building in grid tiles.</summary>
    public Vector2I Size;

    /// <summary>Size of each tile in pixels.</summary>
    public int TileSize = 32;

    /// <summary>Default size for buildings (2x2 tiles).</summary>
    public Vector2I DefaultSize = new Vector2I(2, 2);

    /// <summary>Visual color for building rendering.</summary>
    public Color Color = new Color(0.6f, 0.6f, 0.6f, 1f);

    // Kanonische ID der Gebaeudedefinition (aus Database)
    public string DefinitionId { get; set; } = "";

    protected Database? database;
    protected Node? dataIndex;

    [Export]
    public string BuildingId { get; set; } = "";

    // Logistik-Upgrade-Einstellungen pro Gebaeude
    // Kapazitaet pro Truck (fuer von diesem Gebaeude erzeugte Lieferungen)
    [Export]
    public int LogisticsTruckCapacity { get; set; } = 5;

    // Geschwindigkeit der Trucks (Pixel/Sek) fuer Lieferungen dieses Gebaeudes
    [Export]
    public float LogisticsTruckSpeed { get; set; } = 32.0f;

    // Interne Grafiksteuerung: Wird das Gebaeude per Icon gerendert?
    private bool hatIconGrafik = false;

    /// <summary>
    /// Initialize building with proper Z-index for rendering.
    /// </summary>
    public override void _Ready()
    {
        if (string.IsNullOrEmpty(this.BuildingId))
        {
            this.BuildingId = Guid.NewGuid().ToString();
        }

        this.ZIndex = 1; // Buildings render above ground
        // Versuche, eine Sprite-Grafik aus der BuildingDef.Icon zu erzeugen
        this.ErzeugeGrafikAusIconWennVorhanden();
    }

    /// <summary>
    /// Render the building as a colored rectangle.
    /// </summary>
    public override void _Draw()
    {
        // Wenn keine Icon-Grafik gesetzt ist, fallback auf farbiges Rechteck
        if (!this.hatIconGrafik)
        {
            var rect = new Rect2(Vector2.Zero, new Vector2(this.Size.X * this.TileSize, this.Size.Y * this.TileSize));
            this.DrawRect(rect, this.Color);
        }
    }

    /// <summary>
    /// Trigger redraw when building enters scene tree.
    /// </summary>
    public override void _EnterTree()
    {
        this.QueueRedraw();
    }

    /// <summary>
    /// Liefert die BuildingDef aus der zentralen Database anhand der gespeicherten DefinitionId.
    /// </summary>
    /// <returns></returns>
    public BuildingDef? GetBuildingDef()
    {
        if (string.IsNullOrEmpty(this.DefinitionId))
        {
            return null;
        }

        // 1) Primär aus Database (falls initialisiert)
        if (this.database != null)
        {
            var def = this.database.GetBuilding(this.DefinitionId);
            if (def != null)
            {
                return def;
            }
        }

        // 2) Fallback: DataIndex (preloaded, export-sicher)
        try
        {
            var di = this.dataIndex ?? this.GetNodeOrNull("/root/DataIndex");
            if (di != null && di.HasMethod("get_buildings"))
            {
                var arrVar = di.Call("get_buildings");
                if (arrVar.VariantType != Variant.Type.Nil)
                {
                    foreach (var v in (Godot.Collections.Array)arrVar)
                    {
                        var res = v.AsGodotObject();
                        if (res is BuildingDef bd && !string.IsNullOrEmpty(bd.Id) && string.Equals(bd.Id, this.DefinitionId, System.StringComparison.Ordinal))
                        {
                            return bd;
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>
    /// Erzeugt eine Sprite2D-Grafik aus BuildingDef.Icon (falls gesetzt) und skaliert sie auf die Baugroesse.
    /// </summary>
    private void ErzeugeGrafikAusIconWennVorhanden()
    {
        var def = this.GetBuildingDef();
        // Export-Fallback: Falls Icon in Def fehlt, versuche DataIndex anhand der Id
        if (def != null && def.Icon == null)
        {
            try
            {
                var di = this.dataIndex ?? this.GetNodeOrNull("/root/DataIndex");
                if (di != null && !string.IsNullOrEmpty(def.Id))
                {
                    var v = di.Call("get_building_icon", def.Id);
                    if (v.VariantType != Variant.Type.Nil)
                    {
                        def.Icon = v.As<Texture2D>();
                    }
                }
            }
            catch
            {
                // Ignorieren – wir fallen spaeter ggf. auf Rechteck-Zeichnung zurueck
            }
        }
        if (def == null || def.Icon == null)
        {
            this.hatIconGrafik = false;
            return;
        }

        var tex = def.Icon;
        var size = tex.GetSize();
        if (size.X <= 0 || size.Y <= 0)
        {
            this.hatIconGrafik = false;
            return;
        }

        var zielBreite = this.Size.X * this.TileSize;
        var zielHoehe = this.Size.Y * this.TileSize;
        var scaleX = (float)zielBreite / (float)size.X;
        var scaleY = (float)zielHoehe / (float)size.Y;

        var sprite = new Sprite2D();
        sprite.Texture = tex;
        sprite.Centered = false; // Oben-Links als Ursprung
        sprite.ZIndex = this.ZIndex;  // Gleiche Ebene wie Gebaeude
        sprite.Scale = new Vector2(scaleX, scaleY);
        this.AddChild(sprite);

        this.hatIconGrafik = true;
    }

    // Leichtgewichtiger Presenter fuer den Inspector (Godot-kompatible Collections)
    public virtual Godot.Collections.Dictionary GetInspectorData()
    {
        var pairs = new Godot.Collections.Array<Godot.Collections.Array>
        {
            new() { "Typ", this.GetType().Name },
            new() { "Position", this.GridPos.ToString() },
        };
        return new Godot.Collections.Dictionary
        {
            { "title", this.Name },
            { "pairs", pairs },
        };
    }

    public virtual void SetDatabase(Database? database)
    {
        this.database = database;
    }

    /// <summary>
    /// Einheitliche Initialize-Methode fuer Gebaeude (aktuell: Database setzen).
    /// </summary>
    public virtual void Initialize(Database? database, Node? dataIndex = null)
    {
        this.database = database;
        this.dataIndex = dataIndex;
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








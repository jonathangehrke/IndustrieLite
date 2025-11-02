// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// RoadRenderer: zeichnet die Strassen. Hoert auf RoadGrid-Events.
/// </summary>
public partial class RoadRenderer : Node2D
{
    private RoadGrid? grid;
    private BuildingManager? buildingManager;
    private Camera2D? camera;
    private bool cameraHooked = false;
    private bool viewportHooked = false;
    private Texture2D? roadTexture; // Kachel-Grafik fuer Strassen
    // Gespeicherte Delegate-Referenzen, damit wir sauber unsubscriben koennen
    private System.Action<Vector2I>? onAdded;
    private System.Action<Vector2I>? onRemoved;
    private readonly AboVerwalter abos = new();

    public void Init(RoadGrid grid, BuildingManager buildingManager, Node? dataIndex)
    {
        this.grid = grid;
        this.buildingManager = buildingManager;
        this.ZIndex = 10;
        // Strassen-Textur laden: Primär aus DataIndex (preloaded), Fallback: Runtime-Loading
        if (dataIndex != null && dataIndex.HasMethod("get_building_icon"))
        {
            var icon = dataIndex.Call("get_building_icon", "road");
            if (icon.VariantType != Variant.Type.Nil)
            {
                this.roadTexture = icon.As<Texture2D>();
            }
        }

        // Fallback falls DataIndex nicht verfügbar oder Icon nicht gefunden
        if (this.roadTexture == null)
        {
            try
            {
                this.roadTexture = ResourceLoader.Load<Texture2D>("res://assets/tiles/strasse.png");
            }
            catch
            {
                this.roadTexture = null;
            }
        }
        if (grid != null)
        {
            // Delegates speichern und Abos via AboVerwalter verwalten
            this.onAdded = _ => this.QueueRedraw();
            this.onRemoved = _ => this.QueueRedraw();
            this.abos.Abonniere(
                () => grid.RoadAdded += this.onAdded,
                () =>
                {
                    try
                    {
                        grid.RoadAdded -= this.onAdded;
                    }
                    catch
                    {
                    }
                });
            this.abos.Abonniere(
                () => grid.RoadRemoved += this.onRemoved,
                () =>
                {
                    try
                    {
                        grid.RoadRemoved -= this.onRemoved;
                    }
                    catch
                    {
                    }
                });
        }
        this.HookViewport();
    }

    // NodePath-basierte Kamera-Setzung entfernt; bitte SetCamera(Camera2D) verwenden

    // Neue Variante: Kamera direkt setzen (ServiceContainer-Variante)
    public void SetCamera(Camera2D cam)
    {
        this.camera = cam;
        this.HookCamera();
    }

    private void HookCamera()
    {
        if (this.cameraHooked)
        {
            return;
        }

        if (this.camera is CameraController ctrl)
        {
            this.abos.VerbindeSignal(ctrl, CameraController.SignalName.CameraViewChanged, this, nameof(this.OnCameraViewChanged));
            this.cameraHooked = true;
        }
    }

    private void OnCameraViewChanged(Vector2 pos, Vector2 zoom)
    {
        this.QueueRedraw();
    }

    private void HookViewport()
    {
        if (this.viewportHooked)
        {
            return;
        }

        var vp = this.GetViewport();
        if (vp != null)
        {
            this.abos.VerbindeSignal(vp, Viewport.SignalName.SizeChanged, this, nameof(this.OnViewportSizeChanged));
            this.viewportHooked = true;
        }
    }

    private void OnViewportSizeChanged()
    {
        this.QueueRedraw();
    }

    /// <inheritdoc/>
    public override void _Draw()
    {
        if (this.grid == null || this.buildingManager == null)
        {
            return;
        }

        int gridW = this.grid.Width;
        int gridH = this.grid.Height;
        int tileSize = this.buildingManager.TileSize;

        // Braeunlicher Ton fuer deutliche Abgrenzung zum Kartengrund
        var roadColor = new Color(0.78f, 0.56f, 0.34f, 1.0f);

        // Sichtbaren Bereich bestimmen
        int minX = 0, minY = 0, maxX = gridW - 1, maxY = gridH - 1;
        var vp = this.GetViewport();
        if (this.camera != null && vp != null)
        {
            var vr = vp.GetVisibleRect();
            Vector2 viewSize = new Vector2(vr.Size.X, vr.Size.Y);
            Vector2 zoom = this.camera.Zoom;
            // Godot 4: Hoeherer Zoom => staerkeres Hineinzoomen => kleinerer sichtbarer Weltbereich.
            // Sichtbarer Weltbereich = Viewport‑Pixelgroesse geteilt durch Zoom.
            Vector2 halfWorld = new Vector2(viewSize.X * 0.5f / zoom.X, viewSize.Y * 0.5f / zoom.Y);

            // Dyn. Overdraw-Rand (in Bildschirm-Pixeln), um Randartefakte bei starkem Zoom-Out zu vermeiden
            const float overdrawPx = 64f;
            Vector2 overWorld = new Vector2(overdrawPx / zoom.X, overdrawPx / zoom.Y);
            halfWorld += overWorld;
            Vector2 center = this.camera.GlobalPosition;
            var vis = new Rect2(center - halfWorld, halfWorld * 2f);

            minX = Mathf.Clamp(Mathf.FloorToInt(vis.Position.X / tileSize) - 1, 0, gridW - 1);
            minY = Mathf.Clamp(Mathf.FloorToInt(vis.Position.Y / tileSize) - 1, 0, gridH - 1);
            maxX = Mathf.Clamp(Mathf.CeilToInt((vis.Position.X + vis.Size.X) / tileSize) + 1, 0, gridW - 1);
            maxY = Mathf.Clamp(Mathf.CeilToInt((vis.Position.Y + vis.Size.Y) / tileSize) + 1, 0, gridH - 1);
        }

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (!this.grid.GetCell(x, y))
                {
                    continue;
                }

                if (this.roadTexture != null)
                {
                    var r = new Rect2(x * tileSize, y * tileSize, tileSize, tileSize);
                    // Modulation anheben, damit Strassen-Textur klarer sichtbar ist
                    this.DrawTextureRect(this.roadTexture, r, false, roadColor, false);
                }
                else
                {
                    var rect = new Rect2((x * tileSize) + 2, (y * tileSize) + 2, tileSize - 4, tileSize - 4);
                    this.DrawRect(rect, roadColor);
                }
            }
        }
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        // Abos immer lösen, um Gedächtnislecks/doppelte Redraws zu vermeiden
        if (this.grid != null)
        {
            if (this.onAdded != null)
            {
                this.grid.RoadAdded -= this.onAdded;
            }

            if (this.onRemoved != null)
            {
                this.grid.RoadRemoved -= this.onRemoved;
            }
        }
        DebugLogger.LogRoad(() => "RoadRenderer: Event-Abos geloest");
        this.abos.DisposeAll();
        base._ExitTree();
    }
}

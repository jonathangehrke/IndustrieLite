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
    private System.Action<Vector2I>? _onAdded;
    private System.Action<Vector2I>? _onRemoved;
    private readonly AboVerwalter _abos = new();

    public void Init(RoadGrid grid, BuildingManager buildingManager)
    {
        this.grid = grid;
        this.buildingManager = buildingManager;
        ZIndex = 10;
        // Strassen-Textur laden (falls vorhanden)
        try { roadTexture = ResourceLoader.Load<Texture2D>("res://assets/tiles/strasse.png"); } catch { roadTexture = null; }
        if (grid != null)
        {
            // Delegates speichern und Abos via AboVerwalter verwalten
            _onAdded = _ => QueueRedraw();
            _onRemoved = _ => QueueRedraw();
            _abos.Abonniere(
                () => grid.RoadAdded += _onAdded,
                () => { try { grid.RoadAdded -= _onAdded; } catch { } }
            );
            _abos.Abonniere(
                () => grid.RoadRemoved += _onRemoved,
                () => { try { grid.RoadRemoved -= _onRemoved; } catch { } }
            );
        }
        HookViewport();
    }

    // NodePath-basierte Kamera-Setzung entfernt; bitte SetCamera(Camera2D) verwenden

    // Neue Variante: Kamera direkt setzen (ServiceContainer-Variante)
    public void SetCamera(Camera2D cam)
    {
        camera = cam;
        HookCamera();
    }

    private void HookCamera()
    {
        if (cameraHooked) return;
        if (camera is CameraController ctrl)
        {
            _abos.VerbindeSignal(ctrl, CameraController.SignalName.CameraViewChanged, this, nameof(OnCameraViewChanged));
            cameraHooked = true;
        }
    }

    private void OnCameraViewChanged(Vector2 pos, Vector2 zoom)
    {
        QueueRedraw();
    }

    private void HookViewport()
    {
        if (viewportHooked) return;
        var vp = GetViewport();
        if (vp != null)
        {
            _abos.VerbindeSignal(vp, Viewport.SignalName.SizeChanged, this, nameof(OnViewportSizeChanged));
            viewportHooked = true;
        }
    }

    private void OnViewportSizeChanged()
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (grid == null || buildingManager == null) return;
        int gridW = grid.Width;
        int gridH = grid.Height;
        int tileSize = buildingManager.TileSize;

        // Braeunlicher Ton fuer deutliche Abgrenzung zum Kartengrund
        var roadColor = new Color(0.78f, 0.56f, 0.34f, 1.0f);

        // Sichtbaren Bereich bestimmen
        int minX = 0, minY = 0, maxX = gridW - 1, maxY = gridH - 1;
        var vp = GetViewport();
        if (camera != null && vp != null)
        {
            var vr = vp.GetVisibleRect();
            Vector2 viewSize = new Vector2(vr.Size.X, vr.Size.Y);
            Vector2 zoom = camera.Zoom;
            Vector2 halfWorld = new Vector2(viewSize.X * 0.5f * zoom.X, viewSize.Y * 0.5f * zoom.Y);
            Vector2 center = camera.GlobalPosition;
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
                if (!grid.GetCell(x, y)) continue;
                if (roadTexture != null)
                {
                    var r = new Rect2(x * tileSize, y * tileSize, tileSize, tileSize);
                    // Modulation anheben, damit Strassen-Textur klarer sichtbar ist
                    DrawTextureRect(roadTexture, r, false, roadColor, false);
                }
                else
                {
                    var rect = new Rect2(x * tileSize + 2, y * tileSize + 2, tileSize - 4, tileSize - 4);
                    DrawRect(rect, roadColor);
                }
            }
        }
    }

    public override void _ExitTree()
    {
        // Abos immer lösen, um Gedächtnislecks/doppelte Redraws zu vermeiden
        if (grid != null)
        {
            if (_onAdded != null) grid.RoadAdded -= _onAdded;
            if (_onRemoved != null) grid.RoadRemoved -= _onRemoved;
        }
        DebugLogger.LogRoad(() => "RoadRenderer: Event-Abos geloest");
        _abos.DisposeAll();
        base._ExitTree();
    }
}

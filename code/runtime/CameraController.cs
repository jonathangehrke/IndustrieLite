// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

public partial class CameraController : Camera2D
{
    // Signal bei Kamera-Aktualisierung (Position/Zoom geaendert)
    [Signal]
    public delegate void CameraViewChangedEventHandler(Vector2 position, Vector2 zoom);

    [Export]
    public float PanSpeed = 600f; // Pixel pro Sekunde
    [Export]
    public float ZoomStep = 0.1f; // relative Aenderung pro Schritt (10%)
    [Export]
    public float MinZoom = 0.5f;  // minimaler Zoom (kleinster Wert)
    [Export]
    public float MaxZoom = 3.0f;  // maximaler Zoom (groesster Wert)

    // Keine NodePath-DI mehr; Game-Daten über ServiceContainer
    private LandManager? landManager;
    private BuildingManager? buildingManager;
    private Vector2 lastEmittedPos = Vector2.Inf; // init for immediate first emit
    private Vector2 lastEmittedZoom = Vector2.Inf;

    private Vector2 simPosition;
    private Vector2 letzteSimPosition;
    private float simZoom;
    private float letzterSimZoom;
    private GameClockManager? gameClock;
    private float interpAccum = 1f;
    private float interpInterval = 1f;

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Manager-Referenzen fuer Weltgrenzen ermitteln (ServiceContainer)
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            sc.TryGetNamedService<LandManager>(nameof(LandManager), out this.landManager);
            sc.TryGetNamedService<BuildingManager>(nameof(BuildingManager), out this.buildingManager);
        }

        // Kamera initial auf Kartenmitte setzen
        if (this.landManager != null && this.buildingManager != null)
        {
            int worldW = this.landManager.GridW * this.buildingManager.TileSize;
            int worldH = this.landManager.GridH * this.buildingManager.TileSize;
            this.Position = new Vector2(worldW / 2f, worldH / 2f);
        }

        this.simPosition = this.Position;
        this.letzteSimPosition = this.Position;
        this.simZoom = this.Zoom.X;
        this.letzterSimZoom = this.Zoom.X;

        this.interpAccum = 1f;
        this.interpInterval = 1f;

        this.SucheGameClock();

        // Selbstregistrierung im ServiceContainer
        try
        {
            // Typed-Registration entfernt (nur Named)
            sc?.RegisterNamedService(nameof(CameraController), this);
        }
        catch
        {
        }
    }

    private void SucheGameClock()
    {
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            sc.TryGetNamedService<GameClockManager>("GameClockManager", out this.gameClock);
        }

        if (this.gameClock == null)
        {
            this.CallDeferred(nameof(this.SucheGameClock));
        }
    }

    public void VerarbeiteSimTick(double dt, Vector2 bewegungsRichtung, IReadOnlyList<int> zoomSchritte)
    {
        this.letzteSimPosition = this.simPosition;
        this.letzterSimZoom = this.simZoom;

        if (bewegungsRichtung != Vector2.Zero)
        {
            var norm = bewegungsRichtung;
            if (norm.LengthSquared() > 1.0001f)
            {
                norm = norm.Normalized();
            }
            float distanz = this.PanSpeed * (float)dt;
            this.simPosition += norm * distanz;
        }

        if (this.landManager != null && this.buildingManager != null)
        {
            float worldW = this.landManager.GridW * this.buildingManager.TileSize;
            float worldH = this.landManager.GridH * this.buildingManager.TileSize;
            // Grobe Klammer, falls Viewport/Zoom noch nicht verfuegbar sind
            this.simPosition = new Vector2(
                Mathf.Clamp(this.simPosition.X, 0f, worldW),
                Mathf.Clamp(this.simPosition.Y, 0f, worldH));
        }

        if (zoomSchritte != null)
        {
            for (int i = 0; i < zoomSchritte.Count; i++)
            {
                int schritt = zoomSchritte[i];
                if (schritt > 0)
                {
                    for (int j = 0; j < schritt; j++)
                    {
                        this.simZoom *= 1.0f + this.ZoomStep;
                    }
                }
                else if (schritt < 0)
                {
                    for (int j = 0; j < -schritt; j++)
                    {
                        this.simZoom *= 1.0f - this.ZoomStep;
                    }
                }
            }
        }

        this.simZoom = Mathf.Clamp(this.simZoom, this.MinZoom, this.MaxZoom);

        // Exakte Klammer basierend auf Viewport-Groesse und Zoom, damit die Kamera
        // nicht ueber die Kartenraender hinaus geschoben werden kann.
        if (this.landManager != null && this.buildingManager != null)
        {
            var vp = this.GetViewport();
            if (vp != null)
            {
                float worldW = this.landManager.GridW * this.buildingManager.TileSize;
                float worldH = this.landManager.GridH * this.buildingManager.TileSize;

                var vr = vp.GetVisibleRect();
                Vector2 viewSize = new Vector2(vr.Size.X, vr.Size.Y);
                float zx = this.simZoom;
                float zy = this.simZoom;
                Vector2 halfWorld = new Vector2(viewSize.X * 0.5f / zx, viewSize.Y * 0.5f / zy);

                float minXBound = halfWorld.X;
                float minYBound = halfWorld.Y;
                float maxXBound = worldW - halfWorld.X;
                float maxYBound = worldH - halfWorld.Y;

                float clampedX = this.simPosition.X;
                float clampedY = this.simPosition.Y;

                if (maxXBound < minXBound)
                {
                    clampedX = worldW * 0.5f;
                }
                else
                {
                    clampedX = Mathf.Clamp(clampedX, minXBound, maxXBound);
                }

                if (maxYBound < minYBound)
                {
                    clampedY = worldH * 0.5f;
                }
                else
                {
                    clampedY = Mathf.Clamp(clampedY, minYBound, maxYBound);
                }

                this.simPosition = new Vector2(clampedX, clampedY);
            }
        }

        this.interpAccum = 0f;
        this.interpInterval = (float)dt;
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        this.interpAccum += (float)delta;
        float alpha = this.interpInterval > 0f ? Mathf.Clamp(this.interpAccum / this.interpInterval, 0f, 1f) : 1f;

        var interpoliertePosition = this.letzteSimPosition.Lerp(this.simPosition, alpha);
        this.Position = interpoliertePosition;

        float interpolierterZoom = Mathf.Lerp(this.letzterSimZoom, this.simZoom, alpha);
        this.Zoom = new Vector2(interpolierterZoom, interpolierterZoom);

        // Aenderungen an Position/Zoom erkennen und einmaliges Event senden
        var pos = this.Position;
        var zoom = this.Zoom;
        bool moved = (pos - this.lastEmittedPos).LengthSquared() > 0.0001f;
        bool zoomed = (zoom - this.lastEmittedZoom).LengthSquared() > 0.000001f;
        if (moved || zoomed)
        {
            this.EmitSignal(SignalName.CameraViewChanged, pos, zoom);
            this.lastEmittedPos = pos;
            this.lastEmittedZoom = zoom;
        }
    }

    public void JumpToImmediate(Vector2 worldPos)
    {
        this.simPosition = worldPos;
        this.letzteSimPosition = worldPos;
        this.Position = worldPos;
        this.lastEmittedPos = worldPos;
        this.interpAccum = 1f;
        this.interpInterval = 1f;
        this.EmitSignal(SignalName.CameraViewChanged, this.Position, this.Zoom);
    }

    public void SetZoomImmediate(float targetZoom)
    {
        float clamped = Mathf.Clamp(targetZoom, this.MinZoom, this.MaxZoom);
        this.simZoom = clamped;
        this.letzterSimZoom = clamped;
        this.Zoom = new Vector2(clamped, clamped);
        this.lastEmittedZoom = this.Zoom;
        this.interpAccum = 1f;
        this.interpInterval = 1f;
        this.EmitSignal(SignalName.CameraViewChanged, this.Position, this.Zoom);
    }
}


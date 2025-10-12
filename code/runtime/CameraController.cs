// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

public partial class CameraController : Camera2D
{
    // Signal bei Kamera-Aktualisierung (Position/Zoom geaendert)
    [Signal] public delegate void CameraViewChangedEventHandler(Vector2 position, Vector2 zoom);

    [Export] public float PanSpeed = 600f; // Pixel pro Sekunde
    [Export] public float ZoomStep = 0.1f; // relative Aenderung pro Schritt (10%)
    [Export] public float MinZoom = 0.4f;  // minimaler Zoom (kleinster Wert)
    [Export] public float MaxZoom = 3.0f;  // maximaler Zoom (groesster Wert)

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
    private float _interpAccum = 1f;
    private float _interpInterval = 1f;


    public override void _Ready()
    {
        // Manager-Referenzen fuer Weltgrenzen ermitteln (ServiceContainer)
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            sc.TryGetNamedService<LandManager>(nameof(LandManager), out landManager);
            sc.TryGetNamedService<BuildingManager>(nameof(BuildingManager), out buildingManager);
        }

        // Kamera initial auf Kartenmitte setzen
        if (landManager != null && buildingManager != null)
        {
            int worldW = landManager.GridW * buildingManager.TileSize;
            int worldH = landManager.GridH * buildingManager.TileSize;
            Position = new Vector2(worldW / 2f, worldH / 2f);
        }

        simPosition = Position;
        letzteSimPosition = Position;
        simZoom = Zoom.X;
        letzterSimZoom = Zoom.X;

        _interpAccum = 1f;
        _interpInterval = 1f;

        SucheGameClock();

        // Selbstregistrierung im ServiceContainer
        try
        {
            // Typed-Registration entfernt (nur Named)
            sc?.RegisterNamedService(nameof(CameraController), this);
        }
        catch { }
    }

    private void SucheGameClock()
    {
        var sc = ServiceContainer.Instance;
        if (sc != null)
            sc.TryGetNamedService<GameClockManager>("GameClockManager", out gameClock);
        if (gameClock == null)
        {
            CallDeferred(nameof(SucheGameClock));
        }
    }

    public void VerarbeiteSimTick(double dt, Vector2 bewegungsRichtung, IReadOnlyList<int> zoomSchritte)
    {
        letzteSimPosition = simPosition;
        letzterSimZoom = simZoom;

        if (bewegungsRichtung != Vector2.Zero)
        {
            var norm = bewegungsRichtung;
            if (norm.LengthSquared() > 1.0001f)
            {
                norm = norm.Normalized();
            }
            float distanz = PanSpeed * (float)dt;
            simPosition += norm * distanz;
        }

        if (landManager != null && buildingManager != null)
        {
            float worldW = landManager.GridW * buildingManager.TileSize;
            float worldH = landManager.GridH * buildingManager.TileSize;
            simPosition = new Vector2(
                Mathf.Clamp(simPosition.X, 0f, worldW),
                Mathf.Clamp(simPosition.Y, 0f, worldH)
            );
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
                        simZoom *= 1.0f + ZoomStep;
                    }
                }
                else if (schritt < 0)
                {
                    for (int j = 0; j < -schritt; j++)
                    {
                        simZoom *= 1.0f - ZoomStep;
                    }
                }
            }
        }

        simZoom = Mathf.Clamp(simZoom, MinZoom, MaxZoom);

        _interpAccum = 0f;
        _interpInterval = (float)dt;
    }

    public override void _Process(double delta)
    {
        _interpAccum += (float)delta;
        float alpha = _interpInterval > 0f ? Mathf.Clamp(_interpAccum / _interpInterval, 0f, 1f) : 1f;

        var interpoliertePosition = letzteSimPosition.Lerp(simPosition, alpha);
        Position = interpoliertePosition;

        float interpolierterZoom = Mathf.Lerp(letzterSimZoom, simZoom, alpha);
        Zoom = new Vector2(interpolierterZoom, interpolierterZoom);

        // Aenderungen an Position/Zoom erkennen und einmaliges Event senden
        var pos = Position;
        var zoom = Zoom;
        bool moved = (pos - lastEmittedPos).LengthSquared() > 0.0001f;
        bool zoomed = (zoom - lastEmittedZoom).LengthSquared() > 0.000001f;
        if (moved || zoomed)
        {
            EmitSignal(SignalName.CameraViewChanged, pos, zoom);
            lastEmittedPos = pos;
            lastEmittedZoom = zoom;
        }
    }

    public void JumpToImmediate(Vector2 worldPos)
    {
        simPosition = worldPos;
        letzteSimPosition = worldPos;
        Position = worldPos;
        lastEmittedPos = worldPos;
        _interpAccum = 1f;
        _interpInterval = 1f;
        EmitSignal(SignalName.CameraViewChanged, Position, Zoom);
    }

    public void SetZoomImmediate(float targetZoom)
    {
        float clamped = Mathf.Clamp(targetZoom, MinZoom, MaxZoom);
        simZoom = clamped;
        letzterSimZoom = clamped;
        Zoom = new Vector2(clamped, clamped);
        lastEmittedZoom = Zoom;
        _interpAccum = 1f;
        _interpInterval = 1f;
        EmitSignal(SignalName.CameraViewChanged, Position, Zoom);
    }
}


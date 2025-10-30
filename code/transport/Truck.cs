// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

public partial class Truck : Node2D
{
    public Guid TruckId { get; set; } = Guid.NewGuid();

    // Ziel und Fracht
    public Vector2 Target;
    public int Amount;
    public double PricePerUnit = 1.0;
    public double TransportCost = 0.0;
    public GameManager? Game;
    // Zuordnung zur Order/Resource
    public int OrderId = 0;

    public Guid JobId { get; set; } = Guid.Empty;

    public StringName ResourceId = new StringName("");

    // Quelle/Ziel-Knoten fuer robustes Aufraeumen
    public Node2D? SourceNode { get; set; }

    public Node2D? TargetNode { get; set; }

    // Bewegungsparameter
    private float speed = GameConstants.Transport.DefaultTruckSpeed; // Pixel pro Sekunde

    public void SetSpeed(float value)
    {
        if (value > 0f)
        {
            this.speed = value;
        }
    }

    public float GetSpeed() => this.speed;

    public List<Vector2>? Path;
    private int pathIndex = 0;

    public int GetPathIndex() => this.pathIndex;

    public void SetPathIndex(int index) => this.pathIndex = index;

    // SC-only: DevFlags via ServiceContainer
    private Node? devFlags;

    // Sprite fuer visuelles Truck-Bild
    private Sprite2D? sprite;

    /// <summary>
    /// Sets DevFlags reference (optional, for debug features).
    /// </summary>
    public void SetDevFlags(Node? devFlags)
    {
        this.devFlags = devFlags;
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Render-Reihenfolge: Trucks ueber Strassen (RoadRenderer ZIndex=10)
        try
        {
            this.ZAsRelative = false;
            this.ZIndex = 11; // knapp ueber RoadRenderer (10)
        }
        catch
        {
        }

        // Truck-Sprite laden
        try
        {
            var texture = ResourceLoader.Load<Texture2D>("res://assets/vehicles/Truck.png");
            if (texture != null)
            {
                this.sprite = new Sprite2D();
                this.sprite.Texture = texture;
                this.sprite.Centered = true;  // Zentriert auf Truck-Position
                this.sprite.ZIndex = 0;       // Relativ zum Truck-Node
                this.sprite.Scale = new Vector2(0.5f, 0.5f);  // Skalierung anpassbar
                this.AddChild(this.sprite);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogTransport(() => $"Truck: Sprite konnte nicht geladen werden: {ex.Message}");
            this.sprite = null;  // Fallback auf DrawRect
        }
    }

    // Fixed-Step Bewegung (immer aktiv)
    private Vector2 simPos;       // aktuelle Sim-Position
    private Vector2 prevSimPos;   // vorherige Sim-Position (fuer Interpolation)
    private Vector2 nextSimPos;   // naechste Sim-Position (fuer Interpolation)
    private bool simInitialized = false;
    private float interpAccum = 1f;
    private float interpInterval = 1f;

    /// <inheritdoc/>
    public override void _Draw()
    {
        // Truck-Koerper zeichnen (nur als Fallback, wenn Sprite fehlt)
        if (this.sprite == null)
        {
            this.DrawRect(new Rect2(new Vector2(-4, -3), new Vector2(8, 6)), new Color(1, 1, 1));
        }

        // Debug: Pfad zeichnen
        bool debug = false;
        if (this.devFlags != null)
        {
            try
            {
                debug = (bool)this.devFlags.Get("debug_draw_paths");
            }
            catch
            {
                debug = false;
            }
        }
        if (debug && this.Path != null && this.pathIndex < this.Path.Count)
        {
            var count = this.Path.Count - this.pathIndex + 1;
            var pts = new Vector2[count];
            pts[0] = Vector2.Zero; // von aktueller Position
            for (int i = 0; i < this.Path.Count - this.pathIndex; i++)
            {
                var wp = this.Path[this.pathIndex + i];
                pts[i + 1] = this.ToLocal(wp);
            }
            this.DrawPolyline(pts, new Color(0, 1, 0, 0.9f), 1.5f);
            for (int i = 1; i < pts.Length; i++)
            {
                this.DrawCircle(pts[i], 2f, new Color(0, 0.8f, 0.2f, 0.9f));
            }
        }
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        if (!this.simInitialized)
        {
            this.simPos = this.GlobalPosition;
            this.prevSimPos = this.simPos;
            this.nextSimPos = this.simPos;
            this.simInitialized = true;
            this.interpAccum = 1f;
            this.interpInterval = 1f;
        }

        this.interpAccum += (float)delta;
        float alpha = this.interpInterval > 0f ? Mathf.Clamp(this.interpAccum / this.interpInterval, 0f, 1f) : 1f;
        this.GlobalPosition = this.prevSimPos.Lerp(this.nextSimPos, alpha);
        this.QueueRedraw();
    }

    /// <summary>
    /// Fixed-Step Simulationsupdate, aufgerufen vom TransportManager.
    /// </summary>
    public void FixedStepTick(double dt)
    {
        if (!this.simInitialized)
        {
            this.simPos = this.GlobalPosition;
            this.prevSimPos = this.simPos;
            this.nextSimPos = this.simPos;
            this.simInitialized = true;
            this.interpAccum = 1f;
            this.interpInterval = 1f;
        }

        this.prevSimPos = this.simPos;

        float verbleibend = this.speed * (float)dt;
        const float arriveEps = 2.5f;

        // Wegpunkte abarbeiten
        while (verbleibend > 0f && this.Path != null && this.pathIndex < (this.Path?.Count ?? 0))
        {
            var waypoint = this.Path![this.pathIndex];
            var toWp = waypoint - this.simPos;
            var distWp = toWp.Length();

            if (distWp <= arriveEps)
            {
                this.simPos = waypoint;
                this.pathIndex++;
                continue;
            }

            if (verbleibend >= distWp)
            {
                this.simPos = waypoint;
                this.pathIndex++;
                verbleibend -= distWp;
            }
            else
            {
                this.simPos += toWp / distWp * verbleibend;
                verbleibend = 0f;
            }
        }

        // Kein (weiterer) Wegpunkt: Direktbewegung zum Ziel
        if (verbleibend > 0f)
        {
            var dir = this.Target - this.simPos;
            float dist = dir.Length();
            if (dist < 4f)
            {
                if (this.Game?.TransportManager != null)
                {
                    this.Game.TransportManager.TruckArrived(this);
                }
                this.QueueFree();
                return;
            }
            if (verbleibend >= dist)
            {
                this.simPos = this.Target;
            }
            else if (dist > 0.0001f)
            {
                this.simPos += dir / dist * verbleibend;
            }
        }

        this.nextSimPos = this.simPos;
        this.interpAccum = 0f;
        this.interpInterval = (float)(dt > 0.0 ? dt : 0.0001);

        // Sprite in Bewegungsrichtung drehen
        if (this.sprite != null)
        {
            var direction = this.nextSimPos - this.prevSimPos;
            if (direction.LengthSquared() > 0.01f) // Nur drehen bei ausreichender Bewegung
            {
                var angle = Mathf.Atan2(direction.Y, direction.X);
                this.sprite.Rotation = angle - (Mathf.Pi / 2); // -90° Offset (Truck zeigt nach oben statt rechts)
            }
        }

        // Zeichnen erfolgt in _Process()
    }
}

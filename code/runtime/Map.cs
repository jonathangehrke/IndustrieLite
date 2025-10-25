// SPDX-License-Identifier: MIT
using System;
using Godot;

public partial class Map : Node2D, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    // Keine NodePath-DI mehr: GameManager/Kamera über ServiceContainer
    [Export]
    public bool DebugLogs { get; set; } = false; // Debug-Ausgaben ein/aus

    [Export]
    public string GrasTexturPfad { get; set; } = "res://assets/tiles/gras.png"; // Pfad zur Gras-Textur

    [Export]
    public double KaufFeedbackDauer { get; set; } = 1.0; // Dauer des Kauf-Feedbacks in Sekunden

    [Export]
    public double MaxRedrawHz { get; set; } = 60.0; // Begrenzung der Redraw-Rate

    private GameManager? game;
    private CameraController? camera; // Injected dependency
    private MapVisualFeedbackSystem? visualFeedback;
    private Color landColor = new Color(0.2f, 0.5f, 0.2f, 0.6f);
    private Color gridColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
    private Color buyableColor = new Color(0.8f, 0.6f, 0.2f, 0.4f); // Orange fuer kaufbare Tiles
    private Color purchaseFeedbackColor = new Color(0.0f, 1.0f, 0.0f, 0.8f); // Gruen fuer Kauf-Bestaetigung
    private bool forceRedraw = false;
    private double sinceLastRedraw = 0.0; // Zeit seit letztem Redraw (Sekunden)
    private int redrawCounter = 0; // Anzahl Redraws (laufende Sitzung)
    private double debugCounterTimer = 0.0; // Ausgabeintervall fuer Debug
    private bool cameraHooked = false;
    private bool viewportHooked = false;
    private readonly AboVerwalter abos = new();
    private Texture2D? grasTex; // Geladene Gras-Textur fuer Landflaechen
    private Color sellableColor = new Color(0.8f, 0.2f, 0.2f, 0.4f); // Rot fuer verkaufbares Land

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Dependencies werden via Initialize() injiziert (explizite DI)
        this.EnsureViewportConnected();

        try
        {
            this.grasTex = ResourceLoader.Load<Texture2D>(this.GrasTexturPfad);
        }
        catch
        {
            this.grasTex = null;
        }

        this.CallDeferred(nameof(this.EnsureViewportConnected));

        this.visualFeedback = new MapVisualFeedbackSystem();
        this.visualFeedback.Name = "VisualFeedback";
        this.visualFeedback.AnzeigeDauer = this.KaufFeedbackDauer;
        this.visualFeedback.DebugAusgabe = this.DebugLogs;
        this.AddChild(this.visualFeedback);

        // Self-registration in ServiceContainer (Named only)
        try
        {
            ServiceContainer.Instance?.RegisterNamedService(nameof(Map), this);
        }
        catch
        {
        }
    }

    public void TriggerPurchaseFeedback(Vector2I cell)
    {
        if (this.visualFeedback != null)
        {
            this.visualFeedback.AktiviereFeedback(cell);
        }
        else
        {
            this.RequestRedraw();
        }
        DebugLogger.LogInput("Purchase feedback triggered for cell: " + cell, this.DebugLogs);
    }

    public Vector2I BerechneCellVonPosition(Vector2 weltPosition)
    {
        if (this.game == null)
        {
            return Vector2I.Zero;
        }

        int tileGroesse = this.game.BuildingManager.TileSize;
        int x = Mathf.FloorToInt(weltPosition.X / tileGroesse);
        int y = Mathf.FloorToInt(weltPosition.Y / tileGroesse);
        return new Vector2I(x, y);
    }

    // Achtung: Nur Visual-Redraw-Steuerung, keine Spielzustandslogik

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        this.sinceLastRedraw += delta;
        this.debugCounterTimer += delta;

        if (this.forceRedraw)
        {
            double minInterval = this.MaxRedrawHz > 0 ? (1.0 / this.MaxRedrawHz) : 0.0;
            if (this.sinceLastRedraw >= minInterval)
            {
                this.QueueRedraw();
                this.redrawCounter++;
                this.sinceLastRedraw = 0.0;
                this.forceRedraw = false;
            }
        }
    }

    // EnsureCameraConnected is now handled in Initialize() method
    // Camera is injected via explicit DI, not via ServiceContainer lookup
    private void OnCameraViewChanged(Vector2 pos, Vector2 zoom)
    {
        this.forceRedraw = true;
    }

    public void RequestRedraw()
    {
        this.forceRedraw = true;
    }

    private void EnsureViewportConnected()
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
        this.forceRedraw = true;
    }

    /// <inheritdoc/>
    public override void _Draw()
    {
        if (this.game == null)
        {
            return;
        }

        var koordinator = this.game.ManagerCoordinator;
        if (koordinator == null)
        {
            return;
        }

        if (this.debugCounterTimer >= 1.0)
        {
            DebugLogger.LogPerf(() => $"Map: Redraws/s ~ {this.redrawCounter}");
            this.redrawCounter = 0;
            this.debugCounterTimer = 0.0;
        }

        int w = this.game.LandManager.GridW;
        int h = this.game.LandManager.GridH;
        int ts = this.game.BuildingManager.TileSize;

        int minX = 0, minY = 0, maxX = w - 1, maxY = h - 1;
        // Use injected camera instead of ServiceContainer lookup
        var vp = this.GetViewport();
        if (this.camera != null && vp != null)
        {
            var vr = vp.GetVisibleRect();
            Vector2 viewSize = new Vector2(vr.Size.X, vr.Size.Y);
            Vector2 zoom = this.camera.Zoom;
            Vector2 halfWorld = new Vector2(viewSize.X * 0.5f * zoom.X, viewSize.Y * 0.5f * zoom.Y);
            Vector2 center = this.camera.GlobalPosition;
            var vis = new Rect2(center - halfWorld, halfWorld * 2f);

            minX = Mathf.Clamp(Mathf.FloorToInt(vis.Position.X / ts) - 1, 0, w - 1);
            minY = Mathf.Clamp(Mathf.FloorToInt(vis.Position.Y / ts) - 1, 0, h - 1);
            maxX = Mathf.Clamp(Mathf.CeilToInt((vis.Position.X + vis.Size.X) / ts) + 1, 0, w - 1);
            maxY = Mathf.Clamp(Mathf.CeilToInt((vis.Position.Y + vis.Size.Y) / ts) + 1, 0, h - 1);
        }

        Vector2I? feedbackCell = this.visualFeedback?.AktuelleZelle;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var rect = new Rect2(x * ts, y * ts, ts, ts);
                var cell = new Vector2I(x, y);

                if (koordinator.IsOwned(cell))
                {
                    if (feedbackCell.HasValue && feedbackCell.Value == cell)
                    {
                        this.DrawRect(rect, this.purchaseFeedbackColor);
                    }
                    else
                    {
                        if (this.grasTex != null)
                        {
                            this.DrawTextureRect(this.grasTex, rect, false);
                        }
                        else
                        {
                            this.DrawRect(rect, this.landColor);
                        }
                    }

                    if (koordinator.IsSellLandModeActive() && koordinator.CanSellLand(cell))
                    {
                        this.DrawRect(rect, this.sellableColor);
                    }
                }
                else if (koordinator.IsBuyLandModeActive() && koordinator.CanBuyLand(cell))
                {
                    this.DrawRect(rect, this.buyableColor);
                }

                // Linien fuer Tile-Umrandungen vorerst deaktiviert (kann spaeter reaktiviert werden):
                // DrawRect(rect, gridColor, false, 1.0f);
            }
        }
    }

    /// <summary>
    /// Clears map data - for lifecycle management.
    /// </summary>
    public void ClearMap()
    {
        // Queue redraw to clear any visual state
        this.QueueRedraw();
        DebugLogger.Log("debug_map", DebugLogger.LogLevel.Info,
            () => "Map: Cleared map data");
    }

    /// <summary>
    /// Resets map to initial state - for lifecycle management.
    /// </summary>
    public void ResetToInitialState()
    {
        // Reset any map-specific state if needed
        this.QueueRedraw();
        DebugLogger.Log("debug_map", DebugLogger.LogLevel.Info,
            () => "Map: Reset to initial state");
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        // Alle Verbindungen loesen und Referenzen abbauen
        this.abos.DisposeAll();
        this.visualFeedback = null;
        this.game = null;
        this.cameraHooked = false;
        this.viewportHooked = false;
        base._ExitTree();
    }
}

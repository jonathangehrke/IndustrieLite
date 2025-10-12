// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;

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
	private float Speed = GameConstants.Transport.DefaultTruckSpeed; // Pixel pro Sekunde

	public void SetSpeed(float value)
	{
		if (value > 0f)
			Speed = value;
	}

	public float GetSpeed() => Speed;
	public List<Vector2>? Path;
	private int _pathIndex = 0;
	public int GetPathIndex() => _pathIndex;
	public void SetPathIndex(int index) => _pathIndex = index;

	// SC-only: DevFlags via ServiceContainer
	private Node? _devFlags;

	// Sprite fuer visuelles Truck-Bild
	private Sprite2D? _sprite;

	public override void _Ready()
	{
		_devFlags = ServiceContainer.Instance?.GetNamedService<Node>(ServiceNames.DevFlags);
		// Render-Reihenfolge: Trucks ueber Strassen (RoadRenderer ZIndex=10)
		try
		{
			ZAsRelative = false;
			ZIndex = 11; // knapp ueber RoadRenderer (10)
		}
		catch { }

		// Truck-Sprite laden
		try
		{
			var texture = ResourceLoader.Load<Texture2D>("res://assets/vehicles/Truck.png");
			if (texture != null)
			{
				_sprite = new Sprite2D();
				_sprite.Texture = texture;
				_sprite.Centered = true;  // Zentriert auf Truck-Position
				_sprite.ZIndex = 0;       // Relativ zum Truck-Node
				_sprite.Scale = new Vector2(0.5f, 0.5f);  // Skalierung anpassbar
				AddChild(_sprite);
			}
		}
		catch (Exception ex)
		{
			DebugLogger.LogTransport(() => $"Truck: Sprite konnte nicht geladen werden: {ex.Message}");
			_sprite = null;  // Fallback auf DrawRect
		}
	}

	// Fixed-Step Bewegung (immer aktiv)
	private Vector2 _simPos;       // aktuelle Sim-Position
	private Vector2 _prevSimPos;   // vorherige Sim-Position (fuer Interpolation)
	private Vector2 _nextSimPos;   // naechste Sim-Position (fuer Interpolation)
	private bool _simInitialized = false;
	private float _interpAccum = 1f;
	private float _interpInterval = 1f;

	public override void _Draw()
	{
		// Truck-Koerper zeichnen (nur als Fallback, wenn Sprite fehlt)
		if (_sprite == null)
		{
			DrawRect(new Rect2(new Vector2(-4, -3), new Vector2(8, 6)), new Color(1, 1, 1));
		}

		// Debug: Pfad zeichnen
		bool debug = false;
		if (_devFlags != null)
		{
			try { debug = (bool)_devFlags.Get("debug_draw_paths"); } catch { debug = false; }
		}
		if (debug && Path != null && _pathIndex < Path.Count)
		{
			var count = Path.Count - _pathIndex + 1;
			var pts = new Vector2[count];
			pts[0] = Vector2.Zero; // von aktueller Position
			for (int i = 0; i < Path.Count - _pathIndex; i++)
			{
				var wp = Path[_pathIndex + i];
				pts[i + 1] = ToLocal(wp);
			}
			DrawPolyline(pts, new Color(0, 1, 0, 0.9f), 1.5f);
			for (int i = 1; i < pts.Length; i++)
				DrawCircle(pts[i], 2f, new Color(0, 0.8f, 0.2f, 0.9f));
		}
	}

	public override void _Process(double delta)
	{
		if (!_simInitialized)
		{
			_simPos = GlobalPosition;
			_prevSimPos = _simPos;
			_nextSimPos = _simPos;
			_simInitialized = true;
			_interpAccum = 1f;
			_interpInterval = 1f;
		}

		_interpAccum += (float)delta;
		float alpha = _interpInterval > 0f ? Mathf.Clamp(_interpAccum / _interpInterval, 0f, 1f) : 1f;
		GlobalPosition = _prevSimPos.Lerp(_nextSimPos, alpha);
		QueueRedraw();
	}

	/// <summary>
	/// Fixed-Step Simulationsupdate, aufgerufen vom TransportManager
	/// </summary>
	public void FixedStepTick(double dt)
	{
		if (!_simInitialized)
		{
			_simPos = GlobalPosition;
			_prevSimPos = _simPos;
			_nextSimPos = _simPos;
			_simInitialized = true;
			_interpAccum = 1f;
			_interpInterval = 1f;
		}

		_prevSimPos = _simPos;

		float verbleibend = Speed * (float)dt;
		const float arriveEps = 2.5f;

		// Wegpunkte abarbeiten
		while (verbleibend > 0f && Path != null && _pathIndex < (Path?.Count ?? 0))
		{
			var waypoint = Path![_pathIndex];
			var toWp = waypoint - _simPos;
			var distWp = toWp.Length();

			if (distWp <= arriveEps)
			{
				_simPos = waypoint;
				_pathIndex++;
				continue;
			}

			if (verbleibend >= distWp)
			{
				_simPos = waypoint;
				_pathIndex++;
				verbleibend -= distWp;
			}
			else
			{
				_simPos += toWp / distWp * verbleibend;
				verbleibend = 0f;
			}
		}

		// Kein (weiterer) Wegpunkt: Direktbewegung zum Ziel
		if (verbleibend > 0f)
		{
			var dir = (Target - _simPos);
			float dist = dir.Length();
			if (dist < 4f)
			{
				if (Game?.TransportManager != null)
				{
					Game.TransportManager.TruckArrived(this);
				}
				QueueFree();
				return;
			}
			if (verbleibend >= dist)
			{
				_simPos = Target;
			}
			else if (dist > 0.0001f)
			{
				_simPos += dir / dist * verbleibend;
			}
		}

		_nextSimPos = _simPos;
		_interpAccum = 0f;
		_interpInterval = (float)(dt > 0.0 ? dt : 0.0001);

		// Sprite in Bewegungsrichtung drehen
		if (_sprite != null)
		{
			var direction = _nextSimPos - _prevSimPos;
			if (direction.LengthSquared() > 0.01f) // Nur drehen bei ausreichender Bewegung
			{
				var angle = Mathf.Atan2(direction.Y, direction.X);
				_sprite.Rotation = angle - Mathf.Pi / 2; // -90° Offset (Truck zeigt nach oben statt rechts)
			}
		}

		// Zeichnen erfolgt in _Process()
	}
}

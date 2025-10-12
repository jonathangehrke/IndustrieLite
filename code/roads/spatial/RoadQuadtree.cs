// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// RoadQuadtree: Raeumlicher Index fuer Strassenzellen (Grid-Koordinaten).
/// Bietet Nearest-Neighbor-Suche mit Manhattan-Distanz.
/// </summary>
public class RoadQuadtree
{
    private class Node
    {
        public Rect2I Bounds;
        public List<Vector2I> Punkte = new List<Vector2I>();
        public Node[]? Kinder = null; // Reihenfolge: NW, NE, SW, SE
        public bool IstBlatt => Kinder == null;
        public int Tiefe;

        public Node(Rect2I bounds, int tiefe)
        {
            Bounds = bounds;
            Tiefe = tiefe;
        }
    }

    private readonly int kapazitaet;
    private readonly int maxTiefe;
    private Node wurzel;

    public RoadQuadtree(Rect2I bounds, int kapazitaet = 8, int maxTiefe = 10)
    {
        this.kapazitaet = Math.Max(1, kapazitaet);
        this.maxTiefe = Math.Max(1, maxTiefe);
        wurzel = new Node(bounds, 0);
    }

    public void Clear()
    {
        wurzel = new Node(wurzel.Bounds, 0);
    }

    public bool Insert(Vector2I p)
    {
        if (!wurzel.Bounds.HasPoint(p)) return false;
        return Insert(wurzel, p);
    }

    public bool Remove(Vector2I p)
    {
        if (!wurzel.Bounds.HasPoint(p)) return false;
        return Remove(wurzel, p);
    }

    public Vector2I? Nearest(Vector2I von, int maxRadius)
    {
        int best = maxRadius >= 0 ? maxRadius : int.MaxValue;
        Vector2I? bestPunkt = null;
        Nearest(wurzel, von, ref best, ref bestPunkt);
        return bestPunkt;
    }

    private bool Insert(Node n, Vector2I p)
    {
        if (!n.Bounds.HasPoint(p)) return false;
        if (n.IstBlatt)
        {
            if (n.Punkte.Count < kapazitaet || n.Tiefe >= maxTiefe)
            {
                n.Punkte.Add(p);
                return true;
            }
            Subdivide(n);
        }
        if (n.Kinder != null)
        {
            foreach (var k in n.Kinder)
            {
                if (Insert(k, p)) return true;
            }
        }
        // Fallback: in diesem Knoten behalten
        n.Punkte.Add(p);
        return true;
    }

    private bool Remove(Node n, Vector2I p)
    {
        if (!n.Bounds.HasPoint(p)) return false;
        if (n.IstBlatt)
        {
            for (int i = 0; i < n.Punkte.Count; i++)
            {
                if (n.Punkte[i] == p)
                {
                    n.Punkte.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
        if (n.Kinder != null)
        {
            foreach (var k in n.Kinder)
            {
                if (Remove(k, p)) return true;
            }
        }
        // Falls nicht in Kindern, ggf. im Rest-Lager
        for (int i = 0; i < n.Punkte.Count; i++)
        {
            if (n.Punkte[i] == p)
            {
                n.Punkte.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    private void Nearest(Node n, Vector2I von, ref int best, ref Vector2I? bestPunkt)
    {
        // Pruning: Wenn die minimale Distanz des Knoten-Rechtecks groesser als bisheriges best ist, ueberspringen
        int minRectDist = MinManhattanDistanceRect(n.Bounds, von);
        if (minRectDist > best) return;

        if (n.IstBlatt)
        {
            for (int i = 0; i < n.Punkte.Count; i++)
            {
                int d = Manhattan(n.Punkte[i], von);
                if (d < best)
                {
                    best = d;
                    bestPunkt = n.Punkte[i];
                    if (best == 0) return;
                }
            }
            return;
        }

        // Reihenfolge der Kinder ist egal; wir pruefen nur, wenn ihr Rect innerhalb best liegen koennte
        if (n.Kinder != null)
        {
            foreach (var k in n.Kinder)
            {
                Nearest(k, von, ref best, ref bestPunkt);
            }
        }
    }

    private void Subdivide(Node n)
    {
        if (!n.IstBlatt) return;
        var pos = n.Bounds.Position;
        var size = n.Bounds.Size;
        int hw = size.X / 2;
        int hh = size.Y / 2;
        if (hw <= 0 || hh <= 0)
        {
            // zu klein zum Subdividen
            return;
        }
        n.Kinder = new Node[4];
        // NW, NE, SW, SE
        n.Kinder[0] = new Node(new Rect2I(pos.X, pos.Y, hw, hh), n.Tiefe + 1);
        n.Kinder[1] = new Node(new Rect2I(pos.X + hw, pos.Y, size.X - hw, hh), n.Tiefe + 1);
        n.Kinder[2] = new Node(new Rect2I(pos.X, pos.Y + hh, hw, size.Y - hh), n.Tiefe + 1);
        n.Kinder[3] = new Node(new Rect2I(pos.X + hw, pos.Y + hh, size.X - hw, size.Y - hh), n.Tiefe + 1);

        // Bestehende Punkte in Kinder verteilen
        var alt = n.Punkte;
        n.Punkte = new List<Vector2I>();
        foreach (var p in alt)
        {
            bool einsortiert = false;
            foreach (var k in n.Kinder)
            {
                if (k.Bounds.HasPoint(p))
                {
                    k.Punkte.Add(p);
                    einsortiert = true;
                    break;
                }
            }
            if (!einsortiert)
            {
                n.Punkte.Add(p);
            }
        }
    }

    private static int Manhattan(Vector2I a, Vector2I b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private static int MinManhattanDistanceRect(Rect2I r, Vector2I p)
    {
        int xMin = r.Position.X;
        int xMax = r.Position.X + r.Size.X - 1;
        int yMin = r.Position.Y;
        int yMax = r.Position.Y + r.Size.Y - 1;

        int dx = 0;
        if (p.X < xMin) dx = xMin - p.X; else if (p.X > xMax) dx = p.X - xMax;
        int dy = 0;
        if (p.Y < yMin) dy = yMin - p.Y; else if (p.Y > yMax) dy = p.Y - yMax;
        return dx + dy;
    }
}

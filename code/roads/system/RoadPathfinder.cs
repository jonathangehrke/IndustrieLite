// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// RoadPathfinder: kapselt AStarGrid2D fuer Strassenwege.
/// </summary>
public class RoadPathfinder : System.IDisposable
{
    private readonly RoadGrid grid;
    private readonly int tileSize;
    private readonly AStarGrid2D astar;
    private readonly RoadQuadtree? quadtree;
    private readonly AboVerwalter abos = new();

    // Parameter für Phase 1: BFS-Suche
    private readonly int maxNearestRoadRadius;
    private readonly bool enablePathDebug;
    private readonly bool useQuadtreeNearest;

    public RoadPathfinder(RoadGrid grid, int tileSize, int maxNearestRoadRadius = 50, bool enablePathDebug = false, bool useQuadtreeNearest = false)
    {
        this.grid = grid;
        this.tileSize = tileSize;
        this.maxNearestRoadRadius = Mathf.Max(1, maxNearestRoadRadius);
        this.enablePathDebug = enablePathDebug;
        this.useQuadtreeNearest = useQuadtreeNearest;
        this.astar = new AStarGrid2D();
        this.astar.Region = new Rect2I(0, 0, grid.Width, grid.Height);
        this.astar.CellSize = new Vector2(tileSize, tileSize);
        this.astar.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        this.astar.Update();

        // Start: alles blockiert, RoadCells entsperren
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                this.astar.SetPointSolid(new Vector2I(x, y), true);
            }
        }
        // Sync bestehende Strassen
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                if (grid.GetCell(x, y))
                {
                    this.astar.SetPointSolid(new Vector2I(x, y), false);
                }
            }
        }

        // Events via AboVerwalter (sauberes Aufräumen)
        this.abos.Abonniere(
            () => grid.RoadAdded += this.OnRoadAdded,
            () =>
            {
                try
                {
                    grid.RoadAdded -= this.OnRoadAdded;
                }
                catch
                {
                }
            });
        this.abos.Abonniere(
            () => grid.RoadRemoved += this.OnRoadRemoved,
            () =>
            {
                try
                {
                    grid.RoadRemoved -= this.OnRoadRemoved;
                }
                catch
                {
                }
            });

        // Optional: Quadtree fuer schnellere Nearest-Suche aufbauen
        if (useQuadtreeNearest)
        {
            this.quadtree = new RoadQuadtree(new Rect2I(0, 0, grid.Width, grid.Height));
            // Initiale Befuellung durch Scan (einmalig)
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid.GetCell(x, y))
                    {
                        this.quadtree.Insert(new Vector2I(x, y));
                    }
                }
            }
        }
    }

    private void OnRoadAdded(Vector2I cell)
    {
        this.astar.SetPointSolid(cell, false);
        if (this.quadtree != null)
        {
            this.quadtree.Insert(cell);
        }
    }

    private void OnRoadRemoved(Vector2I cell)
    {
        this.astar.SetPointSolid(cell, true);
        if (this.quadtree != null)
        {
            this.quadtree.Remove(cell);
        }
    }

    public List<Vector2> GetPathWorld(Vector2 fromWorld, Vector2 toWorld)
    {
        var fromCell = new Vector2I(Mathf.FloorToInt(fromWorld.X / this.tileSize), Mathf.FloorToInt(fromWorld.Y / this.tileSize));
        var toCell = new Vector2I(Mathf.FloorToInt(toWorld.X / this.tileSize), Mathf.FloorToInt(toWorld.Y / this.tileSize));

        if (!this.grid.AnyRoadExists())
        {
            return new List<Vector2>();
        }

        // Phase 1: BFS, optional Phase 2: Quadtree bevorzugen
        var start = this.FindNearestRoadCellFast(fromCell);
        var end = this.FindNearestRoadCellFast(toCell);
        if (start == null || end == null)
        {
            return new List<Vector2>();
        }

        var idPath = this.astar.GetIdPath(start.Value, end.Value);
        if (idPath == null || idPath.Count == 0)
        {
            return new List<Vector2>();
        }

        var simplified = this.SimplifyGridPath(idPath);
        var world = new List<Vector2>(simplified.Count);
        foreach (var c in simplified)
        {
            world.Add(this.CellCenterToWorld(c));
        }

        return world;
    }

    // BFS-Wellen von der Ausgangszelle; bricht beim ersten Straßentreffer ab
    private Vector2I? FindNearestRoadCellBFS(Vector2I from, int maxRadius)
    {
        // Falls Start außerhalb liegt, auf Kartenbereich clampen
        var start = new Vector2I(
            Mathf.Clamp(from.X, 0, this.grid.Width - 1),
            Mathf.Clamp(from.Y, 0, this.grid.Height - 1));

        if (this.grid.IsRoad(start))
        {
            return start;
        }

        var besucht = new HashSet<Vector2I>();
        var queue = new Queue<(Vector2I zelle, int dist)>();
        queue.Enqueue((start, 0));
        besucht.Add(start);

        int besuche = 0;
        while (queue.Count > 0)
        {
            var (zelle, dist) = queue.Dequeue();
            besuche++;

            // Nachbarn nur bis zum Maxradius erweitern
            if (dist >= maxRadius)
            {
                continue;
            }

            foreach (var n in this.GetNachbarn4(zelle))
            {
                if (besucht.Contains(n))
                {
                    continue;
                }

                besucht.Add(n);

                if (this.grid.IsRoad(n))
                {
                    if (this.enablePathDebug)
                    {
                        DebugLogger.LogRoad(() => $"RoadPathfinder: Nearest road in r={dist + 1}, visits={besuche} at cell={n}");
                    }

                    return n;
                }
                queue.Enqueue((n, dist + 1));
            }
        }

        if (this.enablePathDebug)
        {
            DebugLogger.LogRoad(() => $"RoadPathfinder: Nearest road not found within r={maxRadius} from={from}");
        }

        return null;
    }

    private IEnumerable<Vector2I> GetNachbarn4(Vector2I z)
    {
        // Oben, Unten, Links, Rechts
        var kandidaten = new Vector2I[]
        {
            new Vector2I(z.X + 1, z.Y),
            new Vector2I(z.X - 1, z.Y),
            new Vector2I(z.X, z.Y + 1),
            new Vector2I(z.X, z.Y - 1),
        };
        foreach (var k in kandidaten)
        {
            if (this.grid.InBounds(k))
            {
                yield return k;
            }
        }
    }

    private Vector2 CellCenterToWorld(Vector2I cell)
    {
        return new Vector2((cell.X * this.tileSize) + (this.tileSize / 2f), (cell.Y * this.tileSize) + (this.tileSize / 2f));
    }

    private List<Vector2I> SimplifyGridPath(Godot.Collections.Array<Vector2I> gridPath)
    {
        var result = new List<Vector2I>();
        if (gridPath.Count == 0)
        {
            return result;
        }

        result.Add(gridPath[0]);

        Vector2I? prev = null;
        Vector2I dir = Vector2I.Zero;
        for (int i = 1; i < gridPath.Count; i++)
        {
            var curr = gridPath[i];
            var last = gridPath[i - 1];
            var step = new Vector2I(System.Math.Sign(curr.X - last.X), System.Math.Sign(curr.Y - last.Y));
            if (prev == null)
            {
                dir = step;
                prev = curr;
                continue;
            }
            if (step != dir)
            {
                result.Add(last);
                dir = step;
            }
            prev = curr;
        }
        var finalCell = gridPath[gridPath.Count - 1];
        if (result.Count == 0 || result[result.Count - 1] != finalCell)
        {
            result.Add(finalCell);
        }

        return result;
    }

    private Vector2I? FindNearestRoadCellFast(Vector2I from)
    {
        if (this.quadtree != null)
        {
            var q = this.quadtree.Nearest(from, this.maxNearestRoadRadius);
            if (q != null)
            {
                if (this.enablePathDebug)
                {
                    DebugLogger.LogRoad(() => $"RoadPathfinder: Quadtree nearest at {q.Value} (r<= {this.maxNearestRoadRadius})");
                }

                return q;
            }
        }
        // Fallback auf BFS
        return this.FindNearestRoadCellBFS(from, this.maxNearestRoadRadius);
    }

    private bool disposed;

    /// <summary>
    /// Event-Abos sauber lösen.
    /// </summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        try
        {
            this.abos.DisposeAll();
            DebugLogger.LogRoad(() => "RoadPathfinder: Event-Abos (AboVerwalter) geloest");
        }
        catch
        { /* defensiv: keine Exceptions beim Dispose propagieren */
        }
        this.disposed = true;
        System.GC.SuppressFinalize(this);
    }

    ~RoadPathfinder()
    {
        // Fallback, falls Dispose nicht aufgerufen wurde
        try
        {
            this.abos.DisposeAll();
        }
        catch
        {
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PreviewPathfinder2D : MonoBehaviour
{
    [Header("World bounds (χάρτης)")]
    public BoxCollider2D worldBounds;

    [Header("Grid")]
    public float cellSize = 0.5f;

    [Tooltip("Ποιες layers θα ελεγχθούν για εμπόδια. Θα αγνοηθούν triggers.")]
    public LayerMask obstacleLayers = ~0;

    [Header("Ignore rules for obstacles")]
    [Tooltip("Tags που ΔΕΝ μετράνε ως εμπόδιο (π.χ. Shelter, Waypoint, Goal).")]
    public string[] ignoreObstacleTags = new[] { "Shelter" };

    [Tooltip("Συγκεκριμένα colliders που θέλουμε να αγνοούνται πάντα.")]
    public Collider2D[] ignoreObstacleColliders;

    [Header("Goal relax")]
    [Tooltip("Αν το goal είναι σε blocked κελί, ψάξε το κοντινότερο walkable.")]
    public bool relaxBlockedGoal = true;

    [Tooltip("Μέγιστη ακτίνα (σε cells) για αναζήτηση walkable goal όταν μπλοκάρεται.")]
    public int relaxMaxRadius = 6;

    [Header("Debug")]
    public bool debugVerbose = false;

    class Node
    {
        public bool walkable;
        public Vector2 wpos;
        public int x, y;
        public int g, h;
        public Node parent;
        public int f => g + h;
    }

    Node[,] grid;
    int gx, gy;
    Vector2 origin;

    const string LOG = "[PP2D] ";


    public void BuildGrid()
    {
        if (!worldBounds) { Debug.LogError($"{LOG}worldBounds is null"); return; }

        var b = worldBounds.bounds;
        origin = b.min;
        gx = Mathf.Max(1, Mathf.CeilToInt(b.size.x / cellSize));
        gy = Mathf.Max(1, Mathf.CeilToInt(b.size.y / cellSize));
        grid = new Node[gx, gy];

        if (debugVerbose)
            Debug.Log($"{LOG}BuildGrid: bounds={b} origin={origin} size=({gx},{gy}) cell={cellSize} obstacleMask={MaskToString(obstacleLayers)}");

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(obstacleLayers);
        var hits = new List<Collider2D>();

        int blockedCount = 0;

        for (int x = 0; x < gx; x++)
            for (int y = 0; y < gy; y++)
            {
                Vector2 p = origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);

                hits.Clear();
                Physics2D.OverlapBox(p, Vector2.one * (cellSize * 0.9f), 0f, filter, hits);

                bool blocked = false;
                foreach (var h in hits)
                {
                    if (!h || h.isTrigger) continue;

                    // Αγνόησε τον ίδιο τον worldBounds
                    if (ReferenceEquals(h, worldBounds)) continue;
                    if (h.transform == worldBounds.transform) continue;

                    // Αγνόησε explicit colliders
                    if (ignoreObstacleColliders != null)
                    {
                        bool isIgnored = false;
                        for (int i = 0; i < ignoreObstacleColliders.Length; i++)
                        {
                            if (ignoreObstacleColliders[i] && ReferenceEquals(h, ignoreObstacleColliders[i]))
                            {
                                isIgnored = true; break;
                            }
                        }
                        if (isIgnored) continue;
                    }

                    // Αγνόησε per-Tag
                    if (ignoreObstacleTags != null && ignoreObstacleTags.Length > 0)
                    {
                        var tag = h.tag;
                        for (int i = 0; i < ignoreObstacleTags.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(ignoreObstacleTags[i]) && tag == ignoreObstacleTags[i])
                            {
                                // ignore
                                goto continueHits;
                            }
                        }
                    }

                    // Αν φτάσαμε εδώ: αυτό το hit μετράει ως εμπόδιο
                    blocked = true;
                    break;

                continueHits: { }
                }

                if (blocked) blockedCount++;
                grid[x, y] = new Node { walkable = !blocked, wpos = p, x = x, y = y };
            }

        if (debugVerbose)
            Debug.Log($"{LOG}BuildGrid done. blockedCells={blockedCount}/{gx * gy}");
    }

    Node NodeFromWorld(Vector2 p)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / cellSize), 0, gx - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((p.y - origin.y) / cellSize), 0, gy - 1);
        var n = grid[x, y];
        if (debugVerbose) Debug.Log($"{LOG}NodeFromWorld({p}) -> ({x},{y}) walkable={n.walkable}");
        return n;
    }

    IEnumerable<Node> Neigh(Node n)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            int nx = n.x + dx, ny = n.y + dy;
            if (nx < 0 || ny < 0 || nx >= gx || ny >= gy) continue;
            var m = grid[nx, ny];
            if (!m.walkable) continue;

            // κόψε διαγώνιες που “ξύνονται” σε εμπόδια
            if (dx != 0 && dy != 0)
            {
                if (!grid[n.x + dx, n.y].walkable) continue;
                if (!grid[n.x, n.y + dy].walkable) continue;
            }
            yield return m;
        }
    }

    static int H(Node a, Node b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        int dmin = Mathf.Min(dx, dy), dmax = Mathf.Max(dx, dy);
        return 14 * dmin + 10 * (dmax - dmin);
    }

    List<Vector3> Reconstruct(Node end)
    {
        var path = new List<Vector3>();
        for (var c = end; c != null; c = c.parent) path.Add(c.wpos);
        path.Reverse();
        return path;
    }

    // Βρες κοντινότερο walkable node αν ο στόχος είναι blocked
    Node FindNearestWalkable(Node target)
    {
        if (target.walkable) return target;

        for (int r = 1; r <= relaxMaxRadius; r++)
        {
            int minX = Mathf.Max(0, target.x - r);
            int maxX = Mathf.Min(gx - 1, target.x + r);
            int minY = Mathf.Max(0, target.y - r);
            int maxY = Mathf.Min(gy - 1, target.y + r);

            for (int x = minX; x <= maxX; x++)
            {
                int y1 = minY; int y2 = maxY;
                if (grid[x, y1].walkable) return grid[x, y1];
                if (grid[x, y2].walkable) return grid[x, y2];
            }
            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                int x1 = minX; int x2 = maxX;
                if (grid[x1, y].walkable) return grid[x1, y];
                if (grid[x2, y].walkable) return grid[x2, y];
            }
        }
        return null;
    }

    public List<Vector3> FindPath(Vector2 start, Vector2 goal)
    {
        if (grid == null) BuildGrid();

        var s = NodeFromWorld(start);
        var t = NodeFromWorld(goal);

        if (!s.walkable)
        {
            Debug.LogWarning($"{LOG}FindPath: start.walkable=False → returning null");
            return null;
        }

        if (!t.walkable && relaxBlockedGoal)
        {
            var oldT = t;
            t = FindNearestWalkable(oldT);
            if (debugVerbose)
            {
                Debug.Log($"{LOG}Goal was blocked. Relaxed goal to {(t != null ? $"({t.x},{t.y}) {t.wpos}" : "<null>")}");
            }
            if (t == null) return null;
        }
        else if (!t.walkable)
        {
            Debug.LogWarning($"{LOG}FindPath: goal.walkable=False (relax disabled) → returning null");
            return null;
        }

        var open = new List<Node> { s };
        var closed = new HashSet<Node>();
        s.g = 0; s.h = H(s, t); s.parent = null;

        int iter = 0;

        while (open.Count > 0)
        {
            iter++;
            var cur = open[0];
            for (int i = 1; i < open.Count; i++)
            {
                var n = open[i];
                if (n.f < cur.f || (n.f == cur.f && n.h < cur.h)) cur = n;
            }
            if (cur == t)
            {
                var path = Reconstruct(cur);
                if (debugVerbose) Debug.Log($"{LOG}FindPath OK in {iter} iters. pathLen={path.Count}");
                return path;
            }

            open.Remove(cur);
            closed.Add(cur);

            foreach (var nb in Neigh(cur))
            {
                if (closed.Contains(nb)) continue;
                int step = (nb.x != cur.x && nb.y != cur.y) ? 14 : 10;
                int gNew = cur.g + step;

                if (!open.Contains(nb) || gNew < nb.g)
                {
                    nb.g = gNew;
                    nb.h = H(nb, t);
                    nb.parent = cur;
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }

            if (iter > gx * gy * 2)
            {
                Debug.LogWarning($"{LOG}FindPath bail-out: too many iterations ({iter}). Check obstacle map / bounds.");
                break;
            }
        }

        Debug.LogWarning($"{LOG}FindPath: no path found start={start} goal={goal}");
        return null;
    }

    string MaskToString(LayerMask m)
    {
        var names = new List<string>();
        for (int i = 0; i < 32; i++)
            if ((m.value & (1 << i)) != 0) names.Add(LayerMask.LayerToName(i));
        return string.Join(",", names);
    }
}

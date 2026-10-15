using System;
using System.Collections.Generic;

namespace BlindAttack.Core
{
    /// <summary>8 yönlü A* (köşe kesmeden). Deterministik: aynı girdi her makinede aynı yolu verir.</summary>
    public class Pathfinder
    {
        readonly GridMap map;
        readonly float[,] g;
        readonly Cell[,] parent;
        readonly int[,] visitStamp, closedStamp;
        int stamp;

        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        const float Diag = 1.41421356f;

        public Pathfinder(GridMap map)
        {
            this.map = map;
            g = new float[map.Width, map.Height];
            parent = new Cell[map.Width, map.Height];
            visitStamp = new int[map.Width, map.Height];
            closedStamp = new int[map.Width, map.Height];
        }

        struct Node { public float F; public int Order; public Cell C; }

        /// <summary>Hücre yolunu döner (başlangıç hariç, hedef dahil). Yol yoksa null.</summary>
        public List<Cell> FindPath(Cell from, Cell to)
        {
            if (!map.IsWalkable(to) || !map.IsWalkable(from)) return null;
            if (from == to) return new List<Cell>();
            stamp++;
            int order = 0;
            var open = new List<Node>();
            g[from.X, from.Y] = 0; visitStamp[from.X, from.Y] = stamp;
            Push(open, new Node { F = H(from, to), Order = order++, C = from });

            while (open.Count > 0)
            {
                var cur = Pop(open);
                var c = cur.C;
                if (closedStamp[c.X, c.Y] == stamp) continue;
                closedStamp[c.X, c.Y] = stamp;
                if (c == to) return Build(from, to);

                for (int i = 0; i < 8; i++)
                {
                    int nx = c.X + Dx[i], ny = c.Y + Dy[i];
                    if (!map.IsWalkable(nx, ny)) continue;
                    if (i >= 4 && (!map.IsWalkable(c.X + Dx[i], c.Y) || !map.IsWalkable(c.X, c.Y + Dy[i]))) continue;
                    if (closedStamp[nx, ny] == stamp) continue;
                    float ng = g[c.X, c.Y] + (i >= 4 ? Diag : 1f);
                    if (visitStamp[nx, ny] == stamp && ng >= g[nx, ny]) continue;
                    visitStamp[nx, ny] = stamp;
                    g[nx, ny] = ng;
                    parent[nx, ny] = c;
                    var n = new Cell(nx, ny);
                    Push(open, new Node { F = ng + H(n, to), Order = order++, C = n });
                }
            }
            return null;
        }

        /// <summary>Yol uzunluğu (birim cinsinden), yol yoksa sonsuz.</summary>
        public float PathLength(Cell from, Cell to)
        {
            var p = FindPath(from, to);
            if (p == null) return float.PositiveInfinity;
            float len = 0; var prev = from;
            foreach (var c in p)
            {
                len += (c.X != prev.X && c.Y != prev.Y) ? Diag : 1f;
                prev = c;
            }
            return len;
        }

        static float H(Cell a, Cell b)
        {
            int dx = Math.Abs(a.X - b.X), dy = Math.Abs(a.Y - b.Y);
            return Math.Max(dx, dy) + (Diag - 1f) * Math.Min(dx, dy);
        }

        List<Cell> Build(Cell from, Cell to)
        {
            var list = new List<Cell>();
            var c = to;
            while (c != from) { list.Add(c); c = parent[c.X, c.Y]; }
            list.Reverse();
            return list;
        }

        static bool Less(Node a, Node b) => a.F < b.F || (a.F == b.F && a.Order < b.Order);

        static void Push(List<Node> h, Node n)
        {
            h.Add(n);
            int i = h.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (!Less(h[i], h[p])) break;
                (h[i], h[p]) = (h[p], h[i]);
                i = p;
            }
        }

        static Node Pop(List<Node> h)
        {
            var top = h[0];
            int last = h.Count - 1;
            h[0] = h[last];
            h.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < h.Count && Less(h[l], h[m])) m = l;
                if (r < h.Count && Less(h[r], h[m])) m = r;
                if (m == i) break;
                (h[i], h[m]) = (h[m], h[i]);
                i = m;
            }
            return top;
        }
    }
}

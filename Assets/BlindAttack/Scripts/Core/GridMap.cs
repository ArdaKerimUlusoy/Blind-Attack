using System;
using System.Collections.Generic;

namespace BlindAttack.Core
{
    public enum Tile : byte
    {
        Grass = 0,   // yürünebilir
        Path = 1,    // yürünebilir (toprak yol, sadece görsel fark)
        Wall = 2,    // taş harabe (engel, görüşü keser)
        Tree = 3,    // ağaç (engel, görüşü keser)
        Water = 4,   // su (engel, ama üstünden görülür/ok atılır)
        Start = 5,   // önerilen kahraman başlangıcı (yürünebilir)
        Exit = 6,    // çıkış kapısı
        Hedge = 7,   // çalı çit (engel, görüşü keser)
        Rock = 8,    // kaya (engel, üstünden görülür)
        House = 9,   // ev (engel, görüşü keser)
        Bridge = 10, // köprü (yürünebilir)
    }

    public struct Cell : IEquatable<Cell>
    {
        public int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public bool Equals(Cell o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => X * 73856093 ^ Y * 19349663;
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public Vec2 Center => new Vec2(X + 0.5f, Y + 0.5f);
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>Unity'den bağımsız, deterministik 2B vektör.</summary>
    public struct Vec2
    {
        public float X, Y;
        public Vec2(float x, float y) { X = x; Y = y; }
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public static float Dist(Vec2 a, Vec2 b) => (a - b).Length;
        public Cell ToCell() => new Cell((int)Math.Floor(X), (int)Math.Floor(Y));
        public override string ToString() => $"({X:0.00},{Y:0.00})";
    }

    /// <summary>Izgara tabanlı harita. (0,0) sol-alt köşe.</summary>
    public class GridMap
    {
        public readonly string Name;
        public readonly int Width, Height;
        readonly Tile[,] tiles;
        public Cell Start { get; private set; }
        public Cell Exit { get; private set; }

        int[,] distToExit;

        public GridMap(string name, string[] rows)
        {
            Name = name;
            Height = rows.Length;
            Width = rows[0].Length;
            tiles = new Tile[Width, Height];
            for (int r = 0; r < Height; r++)
            {
                if (rows[r].Length != Width) throw new ArgumentException($"{name}: satır {r} uzunluğu farklı");
                int y = Height - 1 - r; // ilk satır haritanın üstü
                for (int x = 0; x < Width; x++)
                {
                    char c = rows[r][x];
                    Tile t;
                    switch (c)
                    {
                        case '#': t = Tile.Wall; break;
                        case 'T': t = Tile.Tree; break;
                        case '~': t = Tile.Water; break;
                        case ',': t = Tile.Path; break;
                        case 'h': t = Tile.Hedge; break;
                        case 'R': t = Tile.Rock; break;
                        case 'H': t = Tile.House; break;
                        case '=': t = Tile.Bridge; break;
                        case 'S': t = Tile.Start; Start = new Cell(x, y); break;
                        case 'E': t = Tile.Exit; Exit = new Cell(x, y); break;
                        default: t = Tile.Grass; break;
                    }
                    tiles[x, y] = t;
                }
            }
            distToExit = Bfs(Exit);
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    MaxStepsToExit = Math.Max(MaxStepsToExit, distToExit[x, y]);
            HeroMinSteps = (int)(MaxStepsToExit * 0.55f);
        }

        /// <summary>Haritadaki en uzak yürünebilir hücrenin çıkışa uzaklığı.</summary>
        public int MaxStepsToExit { get; private set; }
        /// <summary>Kahraman çıkışa en az bu kadar adım uzağa konmalı (başlangıç bölgesi).</summary>
        public int HeroMinSteps { get; private set; }

        /// <summary>Kahraman bu hücreye konabilir mi (başlangıç bölgesi).</summary>
        public bool CanSpawnHero(Cell c) => IsWalkable(c) && StepsToExit(c) >= HeroMinSteps;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Cell c) => InBounds(c.X, c.Y);
        public Tile Get(int x, int y) => InBounds(x, y) ? tiles[x, y] : Tile.Wall;
        public Tile Get(Cell c) => Get(c.X, c.Y);

        public bool IsWalkable(int x, int y)
        {
            var t = Get(x, y);
            return t == Tile.Grass || t == Tile.Path || t == Tile.Start || t == Tile.Exit || t == Tile.Bridge;
        }
        public bool IsWalkable(Cell c) => IsWalkable(c.X, c.Y);

        /// <summary>Çıkışa adım sayısı (ulaşılamazsa -1).</summary>
        public int StepsToExit(Cell c) => InBounds(c) ? distToExit[c.X, c.Y] : -1;
        public int StartToExitSteps => distToExit[Start.X, Start.Y];

        int[,] Bfs(Cell from)
        {
            var d = new int[Width, Height];
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++) d[x, y] = -1;
            var q = new Queue<Cell>();
            d[from.X, from.Y] = 0; q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = c.X + Dx4[i], ny = c.Y + Dy4[i];
                    if (!IsWalkable(nx, ny) || d[nx, ny] >= 0) continue;
                    d[nx, ny] = d[c.X, c.Y] + 1;
                    q.Enqueue(new Cell(nx, ny));
                }
            }
            return d;
        }

        /// <summary>İki nokta arasında görüş var mı (engeller görüşü keser).</summary>
        public bool HasLineOfSight(Vec2 a, Vec2 b)
        {
            var delta = b - a;
            float len = delta.Length;
            int steps = Math.Max(1, (int)(len / 0.2f));
            for (int i = 1; i < steps; i++)
            {
                var p = a + delta * (i / (float)steps);
                var c = p.ToCell();
                if (BlocksSight(Get(c))) return false;
            }
            return true;
        }

        /// <summary>İki nokta arasında düz yürünebilir bir hat var mı (su/kaya da engeller).</summary>
        public bool HasWalkLine(Vec2 a, Vec2 b)
        {
            var delta = b - a;
            float len = delta.Length;
            int steps = Math.Max(1, (int)(len / 0.2f));
            for (int i = 1; i < steps; i++)
            {
                var p = a + delta * (i / (float)steps);
                if (!IsWalkable(p.ToCell())) return false;
            }
            return true;
        }

        public static bool BlocksSight(Tile t) => t == Tile.Wall || t == Tile.Tree || t == Tile.Hedge || t == Tile.House;

        public static readonly int[] Dx4 = { 1, -1, 0, 0 };
        public static readonly int[] Dy4 = { 0, 0, 1, -1 };
    }
}

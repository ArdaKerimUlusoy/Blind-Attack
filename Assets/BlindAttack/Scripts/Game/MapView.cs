using System.Collections.Generic;
using BlindAttack.Core;
using UnityEngine;

namespace BlindAttack.Game
{
    public enum MapId { NehirVadisi = 0, KaleBahcesi = 1, TasOcagi = 2 }
    public enum OverlayMode { None, Defense, Supply }

    /// <summary>
    /// Bir GridMap'i 3B sahneye çevirir. Hücre (x,y) -> dünya (x+0.5, 0, y+0.5).
    /// Edit modunda da çalışır: Inspector'dan seçilen harita Scene/Hierarchy'de görünür
    /// (oluşturulan parçalar sahneye kaydedilmez, her açılışta yeniden üretilir).
    /// </summary>
    [ExecuteAlways]
    public class MapView : MonoBehaviour
    {
        [Tooltip("Edit modunda ve ana menüde gösterilecek harita")]
        public MapId previewMap = MapId.NehirVadisi;

        public GridMap Map { get; private set; }
        public Transform UnitsRoot { get; private set; }
        public Transform ExitPortal { get; private set; }

        Transform generated, overlayRoot;
        GameObject hover, gridOverlay;
        Material hoverMat;
        int builtIndex = -1;
        OverlayMode overlayMode = OverlayMode.None;

        public static Vector3 CellToWorld(Cell c) => new Vector3(c.X + 0.5f, 0f, c.Y + 0.5f);
        public static Vector3 ToWorld(Vec2 p) => new Vector3(p.X, 0f, p.Y);

        public Bounds WorldBounds => Map == null ? new Bounds(new Vector3(15, 0.5f, 10), new Vector3(30, 1, 20))
            : new Bounds(new Vector3(Map.Width / 2f, 0.6f, Map.Height / 2f), new Vector3(Map.Width, 1.2f, Map.Height));

        // ---------------- Edit modu önizleme ----------------
        void OnEnable()
        {
            if (!Application.isPlaying) Build((int)previewMap);
        }

        void OnDisable()
        {
            if (!Application.isPlaying) ClearGenerated();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled && !Application.isPlaying && builtIndex != (int)previewMap) Build((int)previewMap);
            };
        }
#endif

        void ClearGenerated()
        {
            if (generated) Gen.Kill(generated.gameObject);
            generated = null; overlayRoot = null; UnitsRoot = null; hover = null; gridOverlay = null;
            builtIndex = -1;
            overlayMode = OverlayMode.None;
            Map = null;
        }

        bool builtHostile;
        public bool IsHostile => builtHostile;

        /// <summary>Haritayı kurar. Aynı harita zaten kuruluysa sadece birimleri temizler.</summary>
        public void Build(int mapIndex, bool hostile = false)
        {
            if (builtIndex == mapIndex && builtHostile == hostile && Map != null && generated != null)
            {
                ClearUnits();
                return;
            }
            ClearGenerated();
            builtIndex = mapIndex;
            builtHostile = hostile;
            Map = MapLibrary.Create(mapIndex);

            generated = Gen.Group(transform, "Harita: " + Map.Name);
            BuildGround(mapIndex, hostile);
            var trees = Gen.Group(generated, "Ağaçlar");
            var hedges = Gen.Group(generated, "Çalılar");
            var ruins = Gen.Group(generated, "Kale Surları");
            var rocks = Gen.Group(generated, "Kayalar");
            var houses = Gen.Group(generated, "Evler");
            var bridges = Gen.Group(generated, "Köprüler");
            var decor = Gen.Group(generated, "Süsler");
            for (int x = 0; x < Map.Width; x++)
                for (int y = 0; y < Map.Height; y++)
                {
                    var c = new Cell(x, y);
                    switch (Map.Get(c))
                    {
                        case Tile.Tree: BuildTree(trees, c, mapIndex, hostile); break;
                        case Tile.Hedge: BuildHedge(hedges, c, mapIndex); break;
                        case Tile.Wall: BuildRuin(ruins, c, mapIndex); break;
                        case Tile.Rock: BuildRocks(rocks, c, mapIndex); break;
                        case Tile.House: BuildHouse(houses, c, mapIndex); break;
                        case Tile.Bridge: BuildBridge(bridges, c); break;
                        case Tile.Grass: BuildGrassDecor(decor, c, mapIndex); break;
                        case Tile.Path: BuildPathDecor(decor, c, mapIndex); break;
                        case Tile.Start: BuildSign(decor, c); break;
                    }
                }
            BuildWater(mapIndex);
            BuildExit();
            overlayRoot = Gen.Group(generated, "Yerleştirme Göstergeleri");
            BuildGrid();
            ClearUnits();
            Gen.MarkGenerated(generated.gameObject);
        }

        public void ClearUnits()
        {
            if (generated == null) return;
            if (UnitsRoot) Gen.Kill(UnitsRoot.gameObject);
            UnitsRoot = Gen.Group(generated, "Birimler");
            if (!Application.isPlaying) UnitsRoot.gameObject.hideFlags = HideFlags.DontSave;
        }

        float R(Cell c, int salt) => Noise.Hash(c.X, c.Y, builtIndex * 1000 + salt);

        // ---------------- Zemin ----------------
        void BuildGround(int seed, bool hostile = false)
        {
            const int ppc = 28; // hücre başına piksel
            int w = Map.Width * ppc, h = Map.Height * ppc;
            var px = new Color[w * h];
            var rng = new System.Random(seed * 977 + 13);

            Color grassA = hostile ? new Color(0.74f, 0.44f, 0.24f) : Art.GrassA;
            Color grassB = hostile ? new Color(0.62f, 0.34f, 0.18f) : Art.GrassB;
            Color meadowTint = hostile ? new Color(0.85f, 0.55f, 0.24f) : new Color(0.74f, 0.84f, 0.42f);

            // her hücre için yakın çevre bilgisi (kenar koyulaştırma için)
            for (int py = 0; py < h; py++)
                for (int pxi = 0; pxi < w; pxi++)
                {
                    float fx = (pxi + 0.5f) / ppc, fy = (py + 0.5f) / ppc;
                    float wx = fx + (Noise.Value(fx * 1.6f, fy * 1.6f, seed + 5) - 0.5f) * 0.5f;
                    float wy = fy + (Noise.Value(fx * 1.6f, fy * 1.6f, seed + 9) - 0.5f) * 0.5f;
                    var tile = Map.Get(Mathf.FloorToInt(wx), Mathf.FloorToInt(wy));
                    float big = Noise.Value(fx * 0.22f, fy * 0.22f, seed + 1);
                    float n = big * 0.55f + Noise.Value(fx * 0.9f, fy * 0.9f, seed + 2) * 0.3f + Noise.Value(fx * 3f, fy * 3f, seed + 3) * 0.15f;
                    Color c;
                    switch (tile)
                    {
                        case Tile.Path: case Tile.Start: case Tile.Exit: case Tile.Bridge:
                            c = Color.Lerp(Art.PathB, Art.PathA, n); break;
                        case Tile.Water: c = Color.Lerp(new Color(0.78f, 0.70f, 0.50f), new Color(0.62f, 0.56f, 0.40f), n); break;
                        case Tile.Wall: case Tile.House: c = Color.Lerp(new Color(0.58f, 0.55f, 0.46f), new Color(0.66f, 0.62f, 0.50f), n); break;
                        case Tile.Rock: c = Color.Lerp(hostile ? new Color(0.58f, 0.30f, 0.22f) : Art.GrassDark, new Color(0.55f, 0.55f, 0.45f), n); break;
                        default:
                            c = Color.Lerp(grassA, grassB, n);
                            // açık çayır / yanık toprak lekeleri
                            float meadow = Mathf.Clamp01((Noise.Value(fx * 0.35f, fy * 0.35f, seed + 21) - 0.6f) * 4f);
                            c = Color.Lerp(c, meadowTint, meadow * 0.5f);
                            break;
                    }

                    // engellerin dibine yumuşak gölge ve yol kenarlarına koyuluk
                    float occ = 0f, pathEdge = 0f, bank = 0f;
                    int cx = Mathf.FloorToInt(fx), cy = Mathf.FloorToInt(fy);
                    bool onPath = tile == Tile.Path || tile == Tile.Start || tile == Tile.Exit;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            var t = Map.Get(cx + ox, cy + oy);
                            float dx = Mathf.Max(Mathf.Abs(fx - (cx + ox + 0.5f)) - 0.5f, 0f);
                            float dy = Mathf.Max(Mathf.Abs(fy - (cy + oy + 0.5f)) - 0.5f, 0f);
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (GridMap.BlocksSight(t)) occ = Mathf.Max(occ, 1f - Mathf.Clamp01(d / 0.5f));
                            if (onPath && !(t == Tile.Path || t == Tile.Start || t == Tile.Exit || t == Tile.Bridge)) pathEdge = Mathf.Max(pathEdge, 1f - Mathf.Clamp01(d / 0.3f));
                            if (t == Tile.Water && tile != Tile.Water) bank = Mathf.Max(bank, 1f - Mathf.Clamp01(d / 0.35f));
                        }
                    c = Color.Lerp(c, new Color(0.86f, 0.78f, 0.56f), bank * 0.7f);
                    c = Color.Lerp(c, c * 0.78f, pathEdge * 0.6f);
                    c = Color.Lerp(c, c * 0.62f, occ * 0.55f);
                    c.a = 1f;
                    px[py * w + pxi] = c;
                }

            // fırça darbeleri
            int dabs = Map.Width * Map.Height * 16;
            for (int i = 0; i < dabs; i++)
            {
                float cx = (float)rng.NextDouble() * w, cy = (float)rng.NextDouble() * h;
                var tile = Map.Get(Mathf.FloorToInt(cx / ppc), Mathf.FloorToInt(cy / ppc));
                bool path = tile == Tile.Path || tile == Tile.Start || tile == Tile.Exit || tile == Tile.Bridge || tile == Tile.Wall || tile == Tile.House;
                Color light = path ? new Color(0.92f, 0.80f, 0.58f) : (hostile ? new Color(0.88f, 0.52f, 0.26f) : new Color(0.72f, 0.88f, 0.44f));
                Color dark = path ? new Color(0.60f, 0.46f, 0.30f) : (hostile ? new Color(0.55f, 0.28f, 0.18f) : new Color(0.30f, 0.52f, 0.22f));
                var col = rng.NextDouble() < 0.55 ? light : dark;
                float r = 2.5f + (float)rng.NextDouble() * 5f;
                float ang = (float)rng.NextDouble();
                Art.Dab(px, w, h, cx, cy, r * (1.3f + ang), r * (0.8f + ang * 0.3f), col, 0.10f + (float)rng.NextDouble() * 0.14f, false);
            }

            var tex = Art.MakeTex(px, w, h, TextureWrapMode.Clamp);
            tex.hideFlags = HideFlags.DontSave;

            var mesh = new Mesh { name = "Ground", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(Map.Width, 0, 0), new Vector3(0, 0, Map.Height), new Vector3(Map.Width, 0, Map.Height) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            var mat = new Material(Art.Lit(Color.white)) { hideFlags = HideFlags.DontSave };
            mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex;
            Gen.MeshObj(generated, "Zemin", mesh, mat, Vector3.zero, Vector3.one);
        }

        // ---------------- Engeller ----------------
        public static void Tree(Transform parent, Vector3 pos, float r1, float r2, float r3, float scale, bool hostile = false)
        {
            var root = new GameObject("Ağaç").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, r1 * 360f, 0);
            root.localScale = Vector3.one * scale;
            root.gameObject.AddComponent<WindSway>().Amount = r2 < 0.38f ? 1.6f : 2.4f;
            var bark = Art.Lit(Art.Wood, Art.BarkTex);
            if (r2 < 0.38f)
            {
                // çam
                Gen.MeshObj(root, "Gövde", MeshGen.Cylinder(6, 0.1f, 0.07f), bark, Vector3.zero, new Vector3(1f, 0.5f, 1f));
                var pine = Art.Lit(Art.Vary(hostile ? new Color(0.52f, 0.32f, 0.22f) : new Color(0.20f, 0.45f, 0.30f), r3, 0.18f));
                var pineLight = Art.Lit(Art.Vary(hostile ? new Color(0.68f, 0.44f, 0.26f) : new Color(0.30f, 0.56f, 0.34f), r3, 0.18f));
                Gen.MeshObj(root, "Dal1", MeshGen.Cone(7), pine, new Vector3(0, 0.35f, 0), new Vector3(1.05f, 0.75f, 1.05f));
                Gen.MeshObj(root, "Dal2", MeshGen.Cone(7), pineLight, new Vector3(0, 0.75f, 0), new Vector3(0.82f, 0.65f, 0.82f), new Vector3(0, 25, 0));
                Gen.MeshObj(root, "Dal3", MeshGen.Cone(7), pine, new Vector3(0, 1.12f, 0), new Vector3(0.55f, 0.6f, 0.55f), new Vector3(0, 50, 0));
            }
            else
            {
                // yuvarlak yapraklı ağaç
                Gen.MeshObj(root, "Gövde", MeshGen.Cylinder(6, 0.13f, 0.08f), bark, Vector3.zero, new Vector3(1f, 0.75f, 1f));
                var leaf = Art.Lit(Art.Vary(hostile ? new Color(0.88f, 0.38f, 0.18f) : Art.Leaf, r3, 0.2f));
                var leafLight = Art.Lit(Art.Vary(hostile ? new Color(0.96f, 0.58f, 0.22f) : Art.LeafLight, r3, 0.2f));
                int v = (int)(r3 * 6);
                Gen.MeshObj(root, "Yaprak1", MeshGen.Blob(v), leaf, new Vector3(0, 0.95f, 0), new Vector3(1.0f, 0.82f, 1.0f));
                Gen.MeshObj(root, "Yaprak2", MeshGen.Blob(v + 1), leafLight, new Vector3(0.18f, 1.25f, 0.08f), new Vector3(0.68f, 0.6f, 0.68f));
                Gen.MeshObj(root, "Yaprak3", MeshGen.Blob(v + 2), leaf, new Vector3(-0.22f, 1.12f, -0.12f), new Vector3(0.6f, 0.52f, 0.6f));
                Gen.MeshObj(root, "Yaprak4", MeshGen.Blob(v + 3), leafLight, new Vector3(0.05f, 1.42f, -0.12f), new Vector3(0.42f, 0.38f, 0.42f));
                if (r1 > 0.7f) // elmalar
                    for (int i = 0; i < 3; i++)
                        Visuals.Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Cos(i * 2.1f) * 0.42f, 0.95f + i * 0.08f, Mathf.Sin(i * 2.1f) * 0.42f), Vector3.one * 0.09f, Art.Lit(new Color(0.85f, 0.22f, 0.18f)));
            }
        }

        void BuildTree(Transform g, Cell c, int seed, bool hostile = false)
        {
            bool edge = c.X == 0 || c.Y == 0 || c.X == Map.Width - 1 || c.Y == Map.Height - 1;
            float s = (edge ? 1.05f : 0.9f) + R(c, 1) * 0.35f;
            Tree(g, CellToWorld(c) + new Vector3((R(c, 2) - 0.5f) * 0.25f, 0, (R(c, 3) - 0.5f) * 0.25f), R(c, 4), R(c, 5), R(c, 6), s, hostile);
        }

        void BuildHedge(Transform g, Cell c, int seed)
        {
            var root = Gen.Group(g, "Çalı");
            root.localPosition = CellToWorld(c);
            var dark = Art.Lit(Art.Vary(new Color(0.24f, 0.48f, 0.22f), R(c, 1), 0.12f));
            var light = Art.Lit(Art.Vary(new Color(0.36f, 0.62f, 0.28f), R(c, 2), 0.12f));
            int v = (int)(R(c, 3) * 6);
            Gen.MeshObj(root, "Gövde", MeshGen.Blob(v), dark, new Vector3(0, 0.38f, 0), new Vector3(1.05f, 0.8f, 1.05f));
            root.gameObject.AddComponent<WindSway>().Amount = 1.1f;
            Gen.MeshObj(root, "Üst", MeshGen.Blob(v + 1), light, new Vector3((R(c, 4) - 0.5f) * 0.3f, 0.62f, (R(c, 5) - 0.5f) * 0.3f), new Vector3(0.65f, 0.45f, 0.65f));
            // komşu çalılarla birleşsin
            if (Map.Get(c.X + 1, c.Y) == Tile.Hedge) Gen.MeshObj(root, "Bağ", MeshGen.Blob(v + 2), dark, new Vector3(0.5f, 0.36f, 0), new Vector3(0.8f, 0.7f, 0.85f));
            if (Map.Get(c.X, c.Y + 1) == Tile.Hedge) Gen.MeshObj(root, "Bağ", MeshGen.Blob(v + 3), dark, new Vector3(0, 0.36f, 0.5f), new Vector3(0.85f, 0.7f, 0.8f));
            if (R(c, 6) > 0.6f) // çiçekler
            {
                var f = Art.Lit(R(c, 7) > 0.5f ? new Color(1f, 0.85f, 0.95f) : new Color(1f, 0.95f, 0.6f));
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 1.7f + R(c, 8) * 6f;
                    Visuals.Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Cos(a) * 0.4f, 0.55f + Mathf.Sin(a * 3f) * 0.12f, Mathf.Sin(a) * 0.4f), Vector3.one * 0.08f, f);
                }
            }
        }

        // ---------------- Kale surları ve kuleler ('#') ----------------
        bool IsWall(int x, int y) => Map.Get(x, y) == Tile.Wall;

        void BuildRuin(Transform g, Cell c, int seed)
        {
            bool l = IsWall(c.X - 1, c.Y), r = IsWall(c.X + 1, c.Y), d = IsWall(c.X, c.Y - 1), u = IsWall(c.X, c.Y + 1);
            bool horiz = l || r, vert = u || d;
            int count = (l ? 1 : 0) + (r ? 1 : 0) + (d ? 1 : 0) + (u ? 1 : 0);
            bool tower = (horiz && vert) || count <= 1
                         || (horiz && !vert && c.X % 6 == 0) || (vert && !horiz && c.Y % 6 == 0);
            if (tower) BuildTower(g, c);
            else BuildWallSegment(g, c, vert && !horiz);
        }

        void BuildWallSegment(Transform g, Cell c, bool alongZ)
        {
            var root = Gen.Group(g, "Sur");
            root.localPosition = CellToWorld(c);
            root.localRotation = Quaternion.Euler(0, alongZ ? 90 : 0, 0);
            var brick = Art.Lit(Art.Vary(Color.white, R(c, 1), 0.08f), Art.BrickTex);
            var cap = Art.Lit(Art.Vary(new Color(0.80f, 0.76f, 0.68f), R(c, 2), 0.06f), Art.StoneTex);
            bool damaged = R(c, 3) < 0.28f;
            float h = damaged ? 0.6f + R(c, 4) * 0.15f : 0.88f;
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, h / 2f, 0), new Vector3(1.0f, h, 0.56f), brick);
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, h + 0.03f, 0), new Vector3(1.0f, 0.06f, 0.62f), cap);
            // mazgallar (diş diş üst kenar), hasarlı surda biri eksik
            for (int i = 0; i < 2; i++)
            {
                if (damaged && i == (R(c, 5) > 0.5f ? 0 : 1)) continue;
                float x = i == 0 ? -0.25f : 0.25f;
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(x, h + 0.16f, 0.18f), new Vector3(0.26f, 0.22f, 0.18f), brick);
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(x, h + 0.16f, -0.18f), new Vector3(0.26f, 0.22f, 0.18f), brick);
            }
            // payanda taşları
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.12f, 0.31f), new Vector3(0.18f, 0.24f, 0.08f), cap);
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.12f, -0.31f), new Vector3(0.18f, 0.24f, 0.08f), cap);
            if (damaged) // dibe düşmüş taşlar
                Gen.MeshObj(root, "Moloz", MeshGen.Rock((int)(R(c, 6) * 8)), cap, new Vector3((R(c, 7) - 0.5f) * 0.6f, 0.08f, 0.38f), new Vector3(0.24f, 0.18f, 0.2f), new Vector3(0, R(c, 8) * 180f, 0));
            if (R(c, 9) > 0.6f) // sarmaşık
            {
                var ivy = Art.Lit(Art.Vary(new Color(0.30f, 0.52f, 0.24f), R(c, 10), 0.12f));
                Gen.MeshObj(root, "Sarmaşık", MeshGen.Blob((int)(R(c, 11) * 6)), ivy, new Vector3((R(c, 12) - 0.5f) * 0.5f, h * 0.55f, 0.29f), new Vector3(0.42f, h * 0.8f, 0.08f));
            }
        }

        void BuildTower(Transform g, Cell c)
        {
            var root = Gen.Group(g, "Kule");
            root.localPosition = CellToWorld(c);
            root.localRotation = Quaternion.Euler(0, R(c, 1) * 360f, 0);
            Tower(root, R(c, 2), R(c, 3), 1f);
        }

        /// <summary>Yuvarlak taş kule: konik çatılı ya da mazgallı.</summary>
        public static void Tower(Transform root, float r1, float r2, float scale)
        {
            var brick = Art.Lit(Art.Vary(Color.white, r1, 0.08f), Art.BrickTex);
            var cap = Art.Lit(new Color(0.80f, 0.76f, 0.68f), Art.StoneTex);
            float h = (1.15f + r2 * 0.35f) * scale;
            float rad = 0.8f * scale;
            Gen.MeshObj(root, "Gövde", MeshGen.Tower(10), brick, Vector3.zero, new Vector3(rad, h, rad));
            Gen.MeshObj(root, "Taban", MeshGen.Tower(10), cap, Vector3.zero, new Vector3(rad * 1.08f, 0.14f * scale, rad * 1.08f));
            Gen.MeshObj(root, "Kuşak", MeshGen.Tower(10), cap, new Vector3(0, h - 0.04f * scale, 0), new Vector3(rad * 1.12f, 0.08f * scale, rad * 1.12f));
            // mazgal pencereleri
            var dark = Art.Lit(new Color(0.12f, 0.10f, 0.10f));
            for (int i = 0; i < 2; i++)
            {
                float a = (i * 180f + 30f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rad * 0.5f;
                Visuals.Part(PrimitiveType.Cube, root, p + Vector3.up * h * 0.6f, new Vector3(0.05f, 0.2f, 0.05f) * scale, dark, new Vector3(0, -a * Mathf.Rad2Deg, 0));
            }
            if (r1 > 0.35f)
            {
                // konik çatı + bayrak
                var roofCol = r2 > 0.5f ? new Color(0.30f, 0.36f, 0.58f) : new Color(0.62f, 0.26f, 0.20f);
                Gen.MeshObj(root, "Çatı", MeshGen.Cone(10), Art.Lit(roofCol), new Vector3(0, h, 0), new Vector3(rad * 1.25f, 0.75f * scale, rad * 1.25f));
                Visuals.Part(PrimitiveType.Sphere, root, new Vector3(0, h + 0.76f * scale, 0), Vector3.one * 0.06f * scale, Art.Lit(new Color(0.95f, 0.76f, 0.3f), null, 0.5f));
                var flag = new GameObject("Flama").transform;
                flag.SetParent(root, false);
                flag.localPosition = new Vector3(0, h + 0.82f * scale, 0);
                Visuals.Part(PrimitiveType.Cube, flag, new Vector3(0, 0.12f, 0), new Vector3(0.015f, 0.26f, 0.015f), Art.Lit(Art.Wood));
                Visuals.Part(PrimitiveType.Cube, flag, new Vector3(0.1f, 0.2f, 0), new Vector3(0.2f, 0.1f, 0.01f), Art.Lit(new Color(0.85f, 0.2f, 0.2f), Art.ClothTex));
                flag.gameObject.AddComponent<Waver>();
            }
            else
            {
                // açık tepe: mazgal halkası
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2f;
                    Visuals.Part(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rad * 0.5f + Vector3.up * (h + 0.1f * scale),
                        new Vector3(0.16f, 0.2f, 0.12f) * scale, brick, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
                }
            }
        }

        void BuildRocks(Transform g, Cell c, int seed)
        {
            var root = Gen.Group(g, "Kaya");
            root.localPosition = CellToWorld(c);
            var m1 = Art.Lit(Art.Vary(new Color(0.60f, 0.60f, 0.62f), R(c, 1), 0.12f), Art.StoneTex);
            var m2 = Art.Lit(Art.Vary(new Color(0.70f, 0.68f, 0.64f), R(c, 2), 0.12f), Art.StoneTex);
            float s = 0.75f + R(c, 3) * 0.35f;
            Gen.MeshObj(root, "Kaya", MeshGen.Rock((int)(R(c, 4) * 8)), m1, new Vector3(0, 0.28f * s, 0), new Vector3(0.95f, 0.8f, 0.9f) * s, new Vector3(0, R(c, 5) * 360f, 0));
            Gen.MeshObj(root, "Kaya", MeshGen.Rock((int)(R(c, 6) * 8)), m2, new Vector3(0.3f, 0.14f, -0.25f), Vector3.one * 0.4f, new Vector3(0, R(c, 7) * 360f, 0));
            if (R(c, 8) > 0.5f)
                Gen.MeshObj(root, "Kaya", MeshGen.Rock((int)(R(c, 9) * 8)), m2, new Vector3(-0.3f, 0.1f, 0.28f), Vector3.one * 0.3f, new Vector3(0, R(c, 10) * 360f, 0));
            if (R(c, 11) > 0.6f)
                Gen.MeshObj(root, "Yosun", MeshGen.Blob(2), Art.Lit(Art.LeafLight), new Vector3(0, 0.52f * s, 0), new Vector3(0.45f, 0.1f, 0.4f) * s);
        }

        /// <summary>Yarı ahşap (kirişli) ortaçağ köy evi: taş temel, sıvalı duvarlar, kiremit ya da saman çatı.</summary>
        public static void House(Transform parent, Vector3 pos, float r1, float r2, float r3)
        {
            var root = new GameObject("Ev").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, r1 > 0.5f ? 0 : 90, 0);
            var plaster = Art.Lit(Art.Vary(new Color(0.98f, 0.93f, 0.82f), r2, 0.05f), Art.PlasterTex);
            var beam = Art.Lit(new Color(0.32f, 0.20f, 0.12f), Art.BarkTex);
            var stone = Art.Lit(new Color(0.66f, 0.63f, 0.58f), Art.StoneTex);
            bool thatch = r3 < 0.33f;
            bool twoStory = r2 > 0.45f;
            var roofMat = thatch ? Art.Lit(Color.white, Art.ThatchTex)
                : Art.Lit(r3 > 0.66f ? new Color(0.62f, 0.66f, 0.80f) : Color.white, Art.RoofTex);

            // taş temel ve zemin kat
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.06f, 0), new Vector3(0.84f, 0.12f, 0.76f), stone);
            float wallTop = 0.6f;
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.36f, 0), new Vector3(0.78f, 0.48f, 0.7f), plaster);
            // ikinci kat bir parça dışa taşar (cumba)
            if (twoStory)
            {
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.79f, 0), new Vector3(0.84f, 0.38f, 0.76f), plaster);
                wallTop = 0.98f;
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.6f, 0), new Vector3(0.86f, 0.05f, 0.78f), beam);
            }
            // kirişler: köşeler, kuşak, ön yüzde çapraz destekler
            float fz = twoStory ? 0.385f : 0.355f;
            foreach (var x in new[] { -0.4f, 0.4f })
                foreach (var z in new[] { -0.355f, 0.355f })
                    Visuals.Part(PrimitiveType.Cube, root, new Vector3(x, wallTop / 2f + 0.06f, z), new Vector3(0.05f, wallTop - 0.1f, 0.05f), beam);
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, wallTop, 0), new Vector3(0.88f, 0.05f, 0.8f), beam);
            float braceY = twoStory ? 0.79f : 0.36f;
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.22f, braceY, fz), new Vector3(0.04f, 0.36f, 0.02f), beam, new Vector3(0, 0, 40));
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(-0.22f, braceY, fz), new Vector3(0.04f, 0.36f, 0.02f), beam, new Vector3(0, 0, -40));
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.4f, braceY, 0), new Vector3(0.02f, 0.36f, 0.04f), beam, new Vector3(40, 0, 0));

            // çatı: iki eğimli kiremit/saman yüzey + üçgen alınlıklar
            float span = 0.5f, pitch = 48f, roofLen = 0.98f;
            float rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * span * 0.9f;
            for (int side = -1; side <= 1; side += 2)
            {
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, wallTop + rise / 2f, side * span / 2f), new Vector3(roofLen, thatch ? 0.09f : 0.05f, span / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.08f),
                    roofMat, new Vector3(side * pitch, 0, 0));
            }
            Gen.MeshObj(root, "Alınlık", MeshGen.Prism(), plaster, new Vector3(0, wallTop, 0), new Vector3(0.74f, rise, 0.86f), new Vector3(0, 90, 0));
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, wallTop + rise + 0.02f, 0), new Vector3(roofLen + 0.02f, 0.05f, 0.06f), beam);
            // baca
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.26f, wallTop + rise * 0.7f, -0.16f), new Vector3(0.13f, rise * 0.9f, 0.13f), stone);
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.26f, wallTop + rise * 1.15f, -0.16f), new Vector3(0.16f, 0.04f, 0.16f), stone);
            var smoke = new GameObject("Baca Dumanı").transform;
            smoke.SetParent(root, false);
            smoke.localPosition = new Vector3(0.26f, wallTop + rise * 1.2f, -0.16f);
            smoke.gameObject.AddComponent<ChimneySmoke>();
            // kapı (kemerli), pencereler, çiçek saksısı
            var door = Art.Lit(new Color(0.42f, 0.26f, 0.15f), Art.WoodTex);
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(-0.12f, 0.27f, 0.356f), new Vector3(0.17f, 0.3f, 0.02f), door);
            Gen.MeshObj(root, "Kemer", MeshGen.HalfCylinder(6), door, new Vector3(-0.12f, 0.42f, 0.356f), new Vector3(0.02f, 0.17f, 0.17f), new Vector3(0, 90, 0));
            Visuals.Part(PrimitiveType.Sphere, root, new Vector3(-0.07f, 0.27f, 0.37f), Vector3.one * 0.025f, Art.Lit(new Color(0.3f, 0.3f, 0.3f), null, 0.5f));
            var win = Art.Glow(new Color(1f, 0.82f, 0.45f), 0.9f);
            float winY = twoStory ? 0.8f : 0.4f;
            foreach (var x in twoStory ? new[] { -0.18f, 0.18f } : new[] { 0.18f })
            {
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(x, winY, fz + 0.005f), new Vector3(0.14f, 0.14f, 0.02f), win);
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(x, winY, fz + 0.015f), new Vector3(0.15f, 0.02f, 0.01f), beam);
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(x, winY, fz + 0.015f), new Vector3(0.02f, 0.15f, 0.01f), beam);
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(x, winY - 0.1f, fz + 0.04f), new Vector3(0.17f, 0.04f, 0.05f), beam);
                var flowers = Art.Lit(r1 > 0.3f ? new Color(0.95f, 0.35f, 0.35f) : new Color(1f, 0.85f, 0.3f));
                for (int k = 0; k < 3; k++)
                    Visuals.Part(PrimitiveType.Sphere, root, new Vector3(x - 0.05f + k * 0.05f, winY - 0.06f, fz + 0.045f), Vector3.one * 0.04f, k == 1 ? Art.Lit(Art.LeafLight) : flowers);
            }
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.405f, twoStory ? 0.36f : 0.38f, 0), new Vector3(0.02f, 0.13f, 0.13f), win);
            // yanında fıçı
            if (r2 < 0.5f)
                Gen.MeshObj(root, "Fıçı", MeshGen.Cylinder(8, 0.5f, 0.5f), Art.Lit(Art.WoodLight, Art.WoodTex), new Vector3(0.42f, 0, 0.36f), new Vector3(0.16f, 0.2f, 0.16f));
        }

        void BuildHouse(Transform g, Cell c, int seed) => House(g, CellToWorld(c), R(c, 1), R(c, 2), R(c, 3));

        void BuildBridge(Transform g, Cell c)
        {
            var root = Gen.Group(g, "Köprü");
            root.localPosition = CellToWorld(c);
            // su sağda/solda ise köprü x yönünde uzanır
            bool alongX = Map.Get(c.X - 1, c.Y) == Tile.Water || Map.Get(c.X + 1, c.Y) == Tile.Water || Map.Get(c.X - 1, c.Y) == Tile.Bridge || Map.Get(c.X + 1, c.Y) == Tile.Bridge;
            root.localRotation = Quaternion.Euler(0, alongX ? 0 : 90, 0);
            var plank = Art.Lit(Art.WoodLight, Art.WoodTex);
            var dark = Art.Lit(Art.Wood, Art.BarkTex);
            for (int i = 0; i < 5; i++)
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(-0.4f + i * 0.2f, 0.06f, 0), new Vector3(0.18f, 0.06f, 0.9f), i % 2 == 0 ? plank : Art.Lit(Art.Vary(Art.WoodLight, 0.3f, 0.1f), Art.WoodTex), new Vector3(0, (i - 2) * 1.5f, 0));
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.28f, 0.44f), new Vector3(1f, 0.05f, 0.05f), dark);
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.28f, -0.44f), new Vector3(1f, 0.05f, 0.05f), dark);
            foreach (var x in new[] { -0.45f, 0.45f })
                foreach (var z in new[] { -0.44f, 0.44f })
                    Visuals.Part(PrimitiveType.Cube, root, new Vector3(x, 0.15f, z), new Vector3(0.07f, 0.3f, 0.07f), dark);
        }

        void BuildWater(int seed)
        {
            var cells = new List<Vector2Int>();
            for (int x = 0; x < Map.Width; x++)
                for (int y = 0; y < Map.Height; y++)
                    if (Map.Get(x, y) == Tile.Water || Map.Get(x, y) == Tile.Bridge) cells.Add(new Vector2Int(x, y));
            if (cells.Count == 0) return;
            var g = Gen.Group(generated, "Su");
            var mat = new Material(Art.Lit(Art.WaterA, Art.WaterTex, 0.8f)) { hideFlags = HideFlags.DontSave };
            var go = Gen.MeshObj(g, "Su Yüzeyi", MeshGen.CellQuads(cells, 0.035f, -0.04f, 0.5f), mat, Vector3.zero, Vector3.one);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<WaterFlow>();
            // nilüferler
            var lily = Art.Lit(Art.LeafLight);
            foreach (var c in cells)
            {
                var cell = new Cell(c.x, c.y);
                if (Map.Get(cell) != Tile.Water || R(cell, 40) < 0.7f) continue;
                Gen.MeshObj(g, "Nilüfer", MeshGen.Disc(10), lily, CellToWorld(cell) + new Vector3((R(cell, 41) - 0.5f) * 0.5f, 0.045f, (R(cell, 42) - 0.5f) * 0.5f), new Vector3(0.3f, 1, 0.3f));
                if (R(cell, 43) > 0.5f)
                    Visuals.Part(PrimitiveType.Sphere, g, CellToWorld(cell) + new Vector3((R(cell, 41) - 0.5f) * 0.5f, 0.07f, (R(cell, 42) - 0.5f) * 0.5f), Vector3.one * 0.07f, Art.Lit(new Color(1f, 0.75f, 0.85f)));
            }
        }

        void BuildGrassDecor(Transform g, Cell c, int seed)
        {
            float r = R(c, 20);
            var pos = CellToWorld(c) + new Vector3((R(c, 21) - 0.5f) * 0.6f, 0, (R(c, 22) - 0.5f) * 0.6f);
            if (r < 0.28f)
            {
                var m = Art.Lit(Art.Vary(Art.GrassDark, R(c, 23), 0.25f));
                for (int i = 0; i < 4; i++)
                    Gen.MeshObj(g, "Ot", MeshGen.Cone(4), m, pos + new Vector3((i - 1.5f) * 0.07f, 0, (i % 2) * 0.06f), new Vector3(0.06f, 0.16f + (i % 2) * 0.06f, 0.06f), new Vector3((i - 1.5f) * 12f, 0, 0));
            }
            else if (r > 0.92f)
            {
                Color[] petals = { new Color(1f, 0.95f, 0.55f), new Color(0.98f, 0.6f, 0.7f), new Color(0.75f, 0.7f, 1f), Color.white };
                var m = Art.Lit(petals[(int)(R(c, 24) * 3.99f)]);
                for (int i = 0; i < 4; i++)
                {
                    var off = new Vector3(Mathf.Cos(i * 1.6f + R(c, 25) * 6f) * 0.2f, 0.07f, Mathf.Sin(i * 1.6f + R(c, 25) * 6f) * 0.2f);
                    Visuals.Part(PrimitiveType.Sphere, g, pos + off, Vector3.one * 0.08f, m);
                }
            }
            else if (r > 0.89f)
            {
                // mantar
                Gen.MeshObj(g, "Mantar", MeshGen.Cylinder(5, 0.05f, 0.04f), Art.Lit(new Color(0.95f, 0.92f, 0.85f)), pos, new Vector3(1, 0.12f, 1));
                Gen.MeshObj(g, "Mantar", MeshGen.Blob(1), Art.Lit(new Color(0.85f, 0.22f, 0.18f)), pos + Vector3.up * 0.13f, new Vector3(0.18f, 0.1f, 0.18f));
            }
        }

        void BuildPathDecor(Transform g, Cell c, int seed)
        {
            if (R(c, 30) > 0.8f)
                Gen.MeshObj(g, "Çakıl", MeshGen.Rock((int)(R(c, 31) * 8)), Art.Lit(new Color(0.72f, 0.68f, 0.6f)), CellToWorld(c) + new Vector3((R(c, 32) - 0.5f) * 0.7f, 0.02f, (R(c, 33) - 0.5f) * 0.7f), Vector3.one * 0.12f);
            // yol kenarında fener direği
            if (R(c, 34) > (Atmosphere.ForMap(builtIndex) == TimeOfDay.Night ? 0.87f : 0.93f)) // gece daha çok fener
            {
                var root = Gen.Group(g, "Fener");
                root.localPosition = CellToWorld(c) + new Vector3(R(c, 35) > 0.5f ? 0.38f : -0.38f, 0, R(c, 36) > 0.5f ? 0.38f : -0.38f);
                Lantern(root, Atmosphere.ForMap(builtIndex) != TimeOfDay.Morning);
            }
        }

        public static void Lantern(Transform root, bool withLight = false)
        {
            var wood = Art.Lit(Art.Wood, Art.BarkTex);
            var iron = Art.Lit(new Color(0.2f, 0.2f, 0.22f), null, 0.4f);
            Gen.MeshObj(root, "Direk", MeshGen.Cylinder(6, 0.5f, 0.4f), wood, Vector3.zero, new Vector3(0.06f, 0.75f, 0.06f));
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.07f, 0.72f, 0), new Vector3(0.16f, 0.03f, 0.03f), wood);
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.14f, 0.62f, 0), new Vector3(0.08f, 0.1f, 0.08f), Art.Glow(new Color(1f, 0.75f, 0.35f), 2.2f));
            Gen.MeshObj(root, "Şapka", MeshGen.Cone(4), iron, new Vector3(0.14f, 0.67f, 0), new Vector3(0.12f, 0.06f, 0.12f), new Vector3(0, 45, 0));
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.14f, 0.565f, 0), new Vector3(0.1f, 0.015f, 0.1f), iron);
            if (withLight)
            {
                // akşam ve gece fenerler yanar
                var light = new GameObject("Işık").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(0.14f, 0.6f, 0);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.38f);
                light.range = 2.6f;
                light.intensity = 1.8f;
                light.shadows = LightShadows.None;
                root.gameObject.AddComponent<TorchFlicker>().Light = light;
            }
        }

        /// <summary>Titreyen alevli meşale (isteğe bağlı nokta ışığı ile).</summary>
        public static void Torch(Transform parent, Vector3 pos, bool withLight)
        {
            var root = new GameObject("Meşale").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            Gen.MeshObj(root, "Sap", MeshGen.Cylinder(6, 0.5f, 0.7f), Art.Lit(Art.Wood, Art.BarkTex), Vector3.zero, new Vector3(0.05f, 0.2f, 0.05f));
            Gen.MeshObj(root, "Tas", MeshGen.Cylinder(6, 0.6f, 0.8f), Art.Lit(new Color(0.25f, 0.24f, 0.24f), null, 0.4f), new Vector3(0, 0.18f, 0), new Vector3(0.09f, 0.06f, 0.09f));
            var flame = Gen.MeshObj(root, "Alev", MeshGen.Cone(6), Art.Glow(new Color(1f, 0.55f, 0.15f), 3f), new Vector3(0, 0.23f, 0), new Vector3(0.09f, 0.18f, 0.09f));
            var core = Gen.MeshObj(root, "Çekirdek", MeshGen.Cone(5), Art.Glow(new Color(1f, 0.9f, 0.5f), 4f), new Vector3(0, 0.23f, 0), new Vector3(0.05f, 0.11f, 0.05f));
            flame.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Light light = null;
            if (withLight)
            {
                light = new GameObject("Işık").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(0, 0.35f, 0);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.65f, 0.3f);
                light.range = 3.2f;
                light.intensity = 2.2f;
                light.shadows = LightShadows.None;
            }
            var f = root.gameObject.AddComponent<TorchFlicker>();
            f.Flame = flame.transform; f.Light = light;
        }

        void BuildSign(Transform g, Cell c)
        {
            // yol ayrımı tabelası
            var root = Gen.Group(g, "Tabela");
            root.localPosition = CellToWorld(c) + new Vector3(-0.35f, 0, 0.35f);
            var wood = Art.Lit(Art.Wood, Art.BarkTex);
            var plank = Art.Lit(Art.WoodLight, Art.WoodTex);
            Gen.MeshObj(root, "Direk", MeshGen.Cylinder(6, 0.5f, 0.45f), wood, Vector3.zero, new Vector3(0.06f, 0.62f, 0.06f));
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0.1f, 0.52f, 0), new Vector3(0.26f, 0.08f, 0.025f), plank, new Vector3(0, -25, 0));
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(-0.08f, 0.4f, 0), new Vector3(0.22f, 0.07f, 0.025f), plank, new Vector3(0, 40, 0));
        }

        void BuildExit()
        {
            var root = Gen.Group(generated, "Çıkış Kapısı");
            root.localPosition = CellToWorld(Map.Exit);
            ExitPortal = root;
            // kapı haritanın kenarında; yerel -z haritanın içine bakar
            float yaw = Map.Exit.X == Map.Width - 1 ? 90 : Map.Exit.X == 0 ? -90 : Map.Exit.Y == Map.Height - 1 ? 0 : 180;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var brick = Art.Lit(Color.white, Art.BrickTex);
            var cap = Art.Lit(new Color(0.80f, 0.76f, 0.68f), Art.StoneTex);

            // iki yanda konik çatılı kuleler
            for (int side = -1; side <= 1; side += 2)
            {
                var t = new GameObject(side < 0 ? "Sol Kule" : "Sağ Kule").transform;
                t.SetParent(root, false);
                t.localPosition = new Vector3(side * 0.68f, 0, 0.05f);
                Tower(t, 0.9f, 0.2f, 0.72f);
                // içe bakan sancak
                var banner = new GameObject("Sancak").transform;
                banner.SetParent(t, false);
                banner.localPosition = new Vector3(0, 0.75f, -0.3f);
                Visuals.Part(PrimitiveType.Cube, banner, new Vector3(0, 0.18f, 0), new Vector3(0.24f, 0.03f, 0.03f), Art.Lit(Art.Wood));
                var cloth = Art.Lit(new Color(0.22f, 0.38f, 0.75f), Art.ClothTex);
                Visuals.Part(PrimitiveType.Cube, banner, new Vector3(0, -0.02f, 0), new Vector3(0.2f, 0.38f, 0.015f), cloth);
                Gen.MeshObj(banner, "Arma", MeshGen.Disc(6), Art.Lit(new Color(0.95f, 0.78f, 0.3f), null, 0.5f), new Vector3(0, 0.02f, -0.01f), new Vector3(0.1f, 1, 0.12f), new Vector3(90, 0, 0));
                Torch(t, new Vector3(0, 0.42f, -0.34f), true);
            }
            // kemer: lento + mazgallar
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 1.32f, 0.05f), new Vector3(0.9f, 0.34f, 0.46f), brick);
            Visuals.Part(PrimitiveType.Cube, root, new Vector3(0, 1.5f, 0.05f), new Vector3(0.94f, 0.05f, 0.5f), cap);
            for (int i = -1; i <= 1; i++)
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(i * 0.3f, 1.62f, -0.12f), new Vector3(0.18f, 0.2f, 0.14f), brick);
            // portal
            var portal = Gen.MeshObj(root, "Portal", MeshGen.Disc(20), Art.Transparent(new Color(0.55f, 0.95f, 1f, 0.55f)), new Vector3(0, 0.62f, 0.05f), new Vector3(0.62f, 1, 1.15f), new Vector3(90, 0, 0));
            portal.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Gen.MeshObj(root, "Portal Arka", MeshGen.Disc(20), Art.Transparent(new Color(0.55f, 0.95f, 1f, 0.55f)), new Vector3(0, 0.62f, 0.05f), new Vector3(0.62f, 1, 1.15f), new Vector3(-90, 0, 0))
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // eşikte rün taşları
            for (int i = 0; i < 5; i++)
                Visuals.Part(PrimitiveType.Cube, root, new Vector3(-0.3f + i * 0.15f, 0.015f, -0.25f), new Vector3(0.1f, 0.02f, 0.1f), Art.Glow(new Color(0.55f, 0.9f, 1f), 1.5f), new Vector3(0, 45, 0));
            var light = new GameObject("Işık").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0, 0.8f, -0.4f);
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.9f, 1f);
            light.range = 4f;
            light.intensity = 3f;
            root.gameObject.AddComponent<Spinner>().Target = portal.transform;
        }

        // ---------------- Yerleştirme göstergeleri ----------------
        void BuildGrid()
        {
            const int ppc = 32;
            int w = Map.Width * ppc, h = Map.Height * ppc;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int cx = x / ppc, cy = y / ppc;
                    bool line = x % ppc == 0 || y % ppc == 0 || x % ppc == ppc - 1 || y % ppc == ppc - 1;
                    px[y * w + x] = line && Map.IsWalkable(cx, cy) ? new Color(1f, 1f, 0.9f, 0.22f) : new Color(0, 0, 0, 0);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels(px); tex.Apply();
            var mat = Art.Transparent(Color.white);
            mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex;
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(0, 0.012f, 0), new Vector3(Map.Width, 0.012f, 0), new Vector3(0, 0.012f, Map.Height), new Vector3(Map.Width, 0.012f, Map.Height) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            gridOverlay = Gen.MeshObj(overlayRoot, "Izgara", mesh, mat, Vector3.zero, Vector3.one);
            gridOverlay.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gridOverlay.SetActive(false);
        }

        /// <summary>Savunmada çıkışa çok yakın (yasak) hücreleri kırmızı, destekte kahramanın başlangıç bölgesini mavi gösterir.</summary>
        public void ShowOverlay(OverlayMode mode)
        {
            if (generated == null) return;
            if (gridOverlay) gridOverlay.SetActive(mode != OverlayMode.None);
            if (mode == overlayMode) return;
            overlayMode = mode;
            var old = overlayRoot.Find("Bölge");
            if (old) Gen.Kill(old.gameObject);
            if (mode == OverlayMode.None) return;
            var cells = new List<Vector2Int>();
            for (int x = 0; x < Map.Width; x++)
                for (int y = 0; y < Map.Height; y++)
                {
                    var c = new Cell(x, y);
                    if (!Map.IsWalkable(c) || Map.Get(c) == Tile.Exit) continue;
                    bool mark = mode == OverlayMode.Defense ? Map.StepsToExit(c) < MatchRules.MinDefenseStepsFromExit : Map.CanSpawnHero(c);
                    if (mark) cells.Add(new Vector2Int(x, y));
                }
            var color = mode == OverlayMode.Defense ? new Color(0.95f, 0.25f, 0.2f, 0.3f) : new Color(0.3f, 0.6f, 1f, 0.22f);
            var go = Gen.MeshObj(overlayRoot, "Bölge", MeshGen.CellQuads(cells, 0.015f, 0.04f), Art.Transparent(color), Vector3.zero, Vector3.one);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetHover(Cell? cell, Color color)
        {
            if (generated == null) return;
            if (hover == null)
            {
                hover = new GameObject("Seçili Hücre");
                hover.transform.SetParent(overlayRoot, false);
                hoverMat = Art.Transparent(color);
                var q = Gen.MeshObj(hover.transform, "Kare", MeshGen.CellQuads(new[] { Vector2Int.zero }, 0.02f, 0.03f), hoverMat, new Vector3(-0.5f, 0, -0.5f), Vector3.one);
                q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            hover.SetActive(cell.HasValue);
            if (!cell.HasValue) return;
            hover.transform.localPosition = CellToWorld(cell.Value);
            color.a = 0.4f;
            Art.SetColor(hoverMat, color);
        }

        /// <summary>Fare ışınının zemin düzlemiyle kesiştiği hücre.</summary>
        public bool ScreenToCell(Camera cam, Vector2 screen, out Cell cell)
        {
            cell = default;
            if (Map == null || cam == null) return false;
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float dist)) return false;
            var p = transform.InverseTransformPoint(ray.GetPoint(dist));
            cell = new Cell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
            return Map.InBounds(cell);
        }
    }

    /// <summary>Meşale alevi ve ışığının titremesi.</summary>
    public class TorchFlicker : MonoBehaviour
    {
        public Transform Flame;
        public Light Light;
        Vector3 baseScale;
        float baseIntensity, seed;
        void Start()
        {
            if (Flame) baseScale = Flame.localScale;
            if (Light) baseIntensity = Light.intensity;
            seed = Random.value * 100f;
        }
        void Update()
        {
            float t = Time.time * 9f + seed;
            float f = 0.85f + 0.15f * Mathf.Sin(t) * Mathf.Sin(t * 1.7f + 1.3f) + 0.08f * Mathf.Sin(t * 3.1f);
            if (Flame) Flame.localScale = new Vector3(baseScale.x * (0.9f + 0.1f * f), baseScale.y * f, baseScale.z * (0.9f + 0.1f * f));
            if (Light) Light.intensity = baseIntensity * (0.8f + 0.25f * f);
        }
    }

    /// <summary>Portal nabız efekti.</summary>
    public class Spinner : MonoBehaviour
    {
        public Transform Target;
        Vector3 baseScale;
        void Start() { if (Target) baseScale = Target.localScale; }
        void Update()
        {
            if (Target) Target.localScale = baseScale * (1f + Mathf.Sin(Time.time * 3f) * 0.05f);
        }
    }

    /// <summary>Su dokusunu yavaşça kaydırır.</summary>
    public class WaterFlow : MonoBehaviour
    {
        Material mat;
        void Start() { mat = GetComponent<MeshRenderer>().sharedMaterial; }
        void Update()
        {
            if (mat == null) return;
            var off = new Vector2(Time.time * 0.03f, Time.time * 0.05f);
            if (mat.HasProperty("_BaseMap")) mat.SetTextureOffset("_BaseMap", off);
        }
    }
}

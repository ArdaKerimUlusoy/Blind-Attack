using BlindAttack.Core;
using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>
    /// Haritanın etrafındaki dünya: orman, tepeler, göl, küçük köy, çiçek tarlaları.
    /// Sade mavi arka plan yerine oyun alanını çevreleyen bir manzara oluşturur.
    /// Edit modunda da görünür (parçalar sahneye kaydedilmez, her açılışta yeniden üretilir).
    /// </summary>
    [ExecuteAlways]
    public class EnvironmentView : MonoBehaviour
    {
        [Tooltip("Oyun alanının boyutu (haritalar 30x20)")]
        public Vector2 playArea = new Vector2(30, 20);
        [Range(60, 200)] public float worldSize = 140f;
        public int seed = 7;

        public static readonly Color FarGroundFriendly = new Color(0.40f, 0.58f, 0.30f);
        public static readonly Color FarGroundHostile = new Color(0.68f, 0.40f, 0.24f);
        public static Color FarGround => CurrentHostile ? FarGroundHostile : FarGroundFriendly;
        public static bool CurrentHostile { get; private set; }

        Transform generated;

        void OnEnable() => Build(false);

        void OnDisable()
        {
            if (generated) Gen.Kill(generated.gameObject);
            generated = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(false); };
        }
#endif

        public void Build(bool hostile = false)
        {
            CurrentHostile = hostile;
            if (generated) Gen.Kill(generated.gameObject);
            generated = Gen.Group(transform, "Manzara");
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            var center = new Vector3(playArea.x / 2f, 0, playArea.y / 2f);

            // ---- zemin ----
            var c1 = hostile ? new Color(0.70f, 0.42f, 0.24f) : new Color(0.46f, 0.64f, 0.32f);
            var c2 = hostile ? new Color(0.58f, 0.30f, 0.18f) : new Color(0.34f, 0.52f, 0.25f);
            var c3 = hostile ? new Color(0.84f, 0.54f, 0.28f) : new Color(0.62f, 0.76f, 0.38f);
            var tex = Art.Painted(256, c1, c2, c3, seed + 3, 900, 9f);
            var groundMat = new Material(Art.Lit(Color.white)) { hideFlags = HideFlags.DontSave };
            groundMat.SetTexture("_BaseMap", tex); groundMat.mainTexture = tex;
            groundMat.SetTextureScale("_BaseMap", Vector2.one * (worldSize / 12f));
            var plane = Gen.MeshObj(generated, "Çevre Zemini", MeshGen.Disc(48), groundMat, center + Vector3.down * 0.03f, new Vector3(worldSize * 2f, 1, worldSize * 2f));
            plane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // uzak zemin düz renk; yakın çevre dokulu kare
            var quad = new Mesh { hideFlags = HideFlags.DontSave, name = "EnvQuad" };
            float hs = worldSize / 2f;
            quad.vertices = new[] { new Vector3(-hs, 0, -hs), new Vector3(hs, 0, -hs), new Vector3(-hs, 0, hs), new Vector3(hs, 0, hs) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateNormals();
            plane.GetComponent<MeshRenderer>().sharedMaterial = Art.Lit(FarGround);
            Gen.MeshObj(generated, "Çimen", quad, groundMat, center + Vector3.down * 0.02f, Vector3.one);

            // haritanın dışında mı?
            bool Outside(Vector3 p, float margin) =>
                p.x < -margin || p.z < -margin || p.x > playArea.x + margin || p.z > playArea.y + margin;
            float EdgeDist(Vector3 p)
            {
                float dx = Mathf.Max(-p.x, p.x - playArea.x, 0f), dz = Mathf.Max(-p.z, p.z - playArea.y, 0f);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            // ---- göl (bir köşede) ----
            var lakePos = new Vector3(-9f, 0, playArea.y + 6f);
            var lake = Gen.Group(generated, "Göl");
            Gen.MeshObj(lake, "Kumsal", MeshGen.Blob(4), Art.Lit(new Color(0.86f, 0.78f, 0.56f)), lakePos + Vector3.down * 0.02f, new Vector3(17f, 0.06f, 11f));
            var water = Gen.MeshObj(lake, "Su", MeshGen.Disc(32), new Material(Art.Lit(Art.WaterA, Art.WaterTex, 0.8f)) { hideFlags = HideFlags.DontSave }, lakePos + Vector3.up * 0.02f, new Vector3(15f, 1, 9.5f));
            water.AddComponent<WaterFlow>();
            for (int i = 0; i < 9; i++)
                Gen.MeshObj(lake, "Nilüfer", MeshGen.Disc(10), Art.Lit(Art.LeafLight), lakePos + new Vector3((R() - 0.5f) * 10f, 0.04f, (R() - 0.5f) * 6f), new Vector3(0.5f, 1, 0.5f));
            bool InLake(Vector3 p) { var d = p - lakePos; return (d.x * d.x) / (9.5f * 9.5f) + (d.z * d.z) / (6.5f * 6.5f) < 1f; }

            // ---- köy (diğer köşede) ----
            var village = Gen.Group(generated, "Köy");
            var vPos = new Vector3(playArea.x + 9f, 0, -7f);
            bool InVillage(Vector3 p) => (p - vPos).magnitude < 7.5f;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + 0.3f;
                var p = vPos + new Vector3(Mathf.Cos(a) * 4.2f, 0, Mathf.Sin(a) * 3.6f);
                MapView.House(village, p, R(), R(), R());
            }
            var well = Gen.Group(village, "Kuyu");
            well.localPosition = vPos;
            Gen.MeshObj(well, "Duvar", MeshGen.Cylinder(8, 0.5f, 0.5f), Art.Lit(Art.Stone, Art.StoneTex), Vector3.zero, new Vector3(1, 0.5f, 1));
            Gen.MeshObj(well, "Çatı", MeshGen.Cone(6), Art.Lit(new Color(0.78f, 0.32f, 0.24f)), Vector3.up * 1.1f, new Vector3(1.4f, 0.5f, 1.4f));
            Visuals.Part(PrimitiveType.Cube, well, new Vector3(0.45f, 0.8f, 0), new Vector3(0.06f, 0.7f, 0.06f), Art.Lit(Art.Wood));
            Visuals.Part(PrimitiveType.Cube, well, new Vector3(-0.45f, 0.8f, 0), new Vector3(0.06f, 0.7f, 0.06f), Art.Lit(Art.Wood));
            // tarla
            for (int r = 0; r < 5; r++)
                Gen.MeshObj(village, "Tarla", MeshGen.Blob(r), Art.Lit(r % 2 == 0 ? new Color(0.85f, 0.75f, 0.35f) : new Color(0.65f, 0.72f, 0.30f)), vPos + new Vector3(-6.5f + r * 1.1f, 0.0f, 6.5f), new Vector3(0.9f, 0.18f, 4f));

            // çit ve saman balyaları
            var fence = Gen.Group(village, "Çit");
            var fWood = Art.Lit(Art.Wood, Art.BarkTex);
            var fieldMin = vPos + new Vector3(-7.3f, 0, 4.2f); var fieldMax = vPos + new Vector3(-1.4f, 0, 8.8f);
            void FenceLine(Vector3 a, Vector3 b)
            {
                int posts = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.9f));
                for (int k = 0; k <= posts; k++)
                    Visuals.Part(PrimitiveType.Cube, fence, Vector3.Lerp(a, b, k / (float)posts) + Vector3.up * 0.25f, new Vector3(0.08f, 0.5f, 0.08f), fWood);
                var mid = (a + b) / 2f; var dir = b - a;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                Visuals.Part(PrimitiveType.Cube, fence, mid + Vector3.up * 0.38f, new Vector3(0.04f, 0.06f, dir.magnitude), fWood, new Vector3(0, yaw, 0));
                Visuals.Part(PrimitiveType.Cube, fence, mid + Vector3.up * 0.2f, new Vector3(0.04f, 0.06f, dir.magnitude), fWood, new Vector3(0, yaw, 0));
            }
            FenceLine(fieldMin, new Vector3(fieldMax.x, 0, fieldMin.z));
            FenceLine(new Vector3(fieldMax.x, 0, fieldMin.z), fieldMax);
            FenceLine(fieldMax, new Vector3(fieldMin.x, 0, fieldMax.z));
            FenceLine(new Vector3(fieldMin.x, 0, fieldMax.z), fieldMin);
            var hay = Art.Lit(Color.white, Art.ThatchTex);
            for (int k = 0; k < 4; k++)
                Gen.MeshObj(village, "Saman Balyası", MeshGen.Cylinder(10, 0.5f, 0.5f), hay, vPos + new Vector3(-1f + k * 0.9f, 0.35f, 9.6f + (k % 2) * 0.5f), new Vector3(0.7f, 0.6f, 0.7f), new Vector3(90, R() * 60f, 0));

            // yel değirmeni
            var mill = Gen.Group(village, "Yel Değirmeni");
            mill.localPosition = vPos + new Vector3(6.8f, 0, 1.5f);
            mill.localRotation = Quaternion.Euler(0, 225, 0); // kanatlar haritaya/kameraya baksın
            Gen.MeshObj(mill, "Taban", MeshGen.Cylinder(8, 0.5f, 0.36f), Art.Lit(Color.white, Art.PlasterTex), Vector3.zero, new Vector3(1.8f, 2.6f, 1.8f));
            Gen.MeshObj(mill, "Taş Etek", MeshGen.Cylinder(8, 0.5f, 0.5f), Art.Lit(Color.white, Art.BrickTex), Vector3.zero, new Vector3(1.9f, 0.5f, 1.9f));
            Gen.MeshObj(mill, "Başlık", MeshGen.Cone(8), Art.Lit(Color.white, Art.ThatchTex), new Vector3(0, 2.55f, 0), new Vector3(1.6f, 1.1f, 1.6f));
            Visuals.Part(PrimitiveType.Cube, mill, new Vector3(0, 0.45f, 0.9f), new Vector3(0.4f, 0.8f, 0.05f), Art.Lit(new Color(0.42f, 0.26f, 0.15f), Art.WoodTex));
            var rotor = Gen.Group(mill, "Kanatlar");
            rotor.localPosition = new Vector3(0, 2.6f, 0.85f);
            Visuals.Part(PrimitiveType.Sphere, rotor, Vector3.zero, Vector3.one * 0.25f, fWood);
            var sail = Art.Lit(new Color(0.95f, 0.92f, 0.85f), Art.ClothTex);
            for (int k = 0; k < 4; k++)
            {
                var arm = Gen.Group(rotor, "Kanat");
                arm.localRotation = Quaternion.Euler(0, 0, k * 90f);
                Visuals.Part(PrimitiveType.Cube, arm, new Vector3(0, 1.0f, 0.05f), new Vector3(0.07f, 2.0f, 0.05f), fWood);
                Visuals.Part(PrimitiveType.Cube, arm, new Vector3(0.18f, 1.15f, 0.07f), new Vector3(0.32f, 1.5f, 0.02f), sail);
            }
            rotor.gameObject.AddComponent<Rotator>().Speed = 28f;

            // ---- tepede uzak kale ----
            var castlePos = new Vector3(playArea.x + 17f, 0, playArea.y + 17f);
            bool InCastle(Vector3 p) => (p - castlePos).magnitude < 11f;
            var castle = Gen.Group(generated, "Uzak Kale");
            castle.localPosition = castlePos;
            castle.localRotation = Quaternion.Euler(0, 225, 0);
            Gen.MeshObj(castle, "Tepe", MeshGen.Blob(5), Art.Lit(new Color(0.40f, 0.58f, 0.30f)), Vector3.zero, new Vector3(18f, 4.2f, 16f));
            Gen.MeshObj(castle, "Kayalık", MeshGen.Rock(3), Art.Lit(Art.Stone, Art.StoneTex), new Vector3(1.5f, 0.6f, -3.5f), new Vector3(5f, 2.5f, 3f));
            var cBase = Gen.Group(castle, "Kale");
            cBase.localPosition = new Vector3(0, 1.75f, 0);
            var cBrick = Art.Lit(Color.white, Art.BrickTex);
            const float half = 2.6f;
            for (int k = 0; k < 4; k++)
            {
                var side = Gen.Group(cBase, "Sur");
                side.localRotation = Quaternion.Euler(0, k * 90f, 0);
                Visuals.Part(PrimitiveType.Cube, side, new Vector3(0, 0.7f, half), new Vector3(half * 2f, 1.4f, 0.45f), cBrick);
                for (int m = -3; m <= 3; m++)
                    Visuals.Part(PrimitiveType.Cube, side, new Vector3(m * 0.7f, 1.55f, half), new Vector3(0.35f, 0.3f, 0.45f), cBrick);
                var tw = Gen.Group(side, "Köşe Kulesi");
                tw.localPosition = new Vector3(half, 0, half);
                MapView.Tower(tw, 0.8f, k % 2 == 0 ? 0.7f : 0.2f, 2.2f);
            }
            var keep = Gen.Group(cBase, "İç Kale");
            Visuals.Part(PrimitiveType.Cube, keep, new Vector3(0, 1.9f, 0), new Vector3(2.2f, 3.8f, 2.2f), cBrick);
            for (int m = 0; m < 4; m++)
            {
                var q = Quaternion.Euler(0, m * 90f, 0);
                for (int n2 = -1; n2 <= 1; n2++)
                    Visuals.Part(PrimitiveType.Cube, keep, q * new Vector3(n2 * 0.7f, 3.95f, 1.0f), new Vector3(0.35f, 0.3f, 0.25f), cBrick, new Vector3(0, m * 90f, 0));
            }
            var keepTower = Gen.Group(keep, "Gözetleme Kulesi");
            keepTower.localPosition = new Vector3(0.5f, 3.8f, 0.5f);
            MapView.Tower(keepTower, 0.9f, 0.8f, 1.6f);
            Visuals.Part(PrimitiveType.Cube, cBase, new Vector3(0, 0.45f, half + 0.24f), new Vector3(0.9f, 0.9f, 0.05f), Art.Lit(new Color(0.32f, 0.2f, 0.12f), Art.WoodTex));

            // ---- orman ----
            var forest = Gen.Group(generated, "Orman");
            int trees = 0;
            for (int i = 0; i < 3000 && trees < 420; i++)
            {
                var p = center + new Vector3((R() - 0.5f) * worldSize * 0.9f, 0, (R() - 0.5f) * worldSize * 0.9f);
                if (!Outside(p, 0.6f) || InLake(p) || InVillage(p) || InCastle(p)) continue;
                float d = EdgeDist(p);
                // haritaya yakın yerlerde sık, uzakta seyrek orman; arada açıklıklar
                float density = d < 6f ? 0.9f : d < 14f ? 0.55f : 0.25f;
                float clearing = Noise.Value(p.x * 0.12f, p.z * 0.12f, seed);
                if (R() > density * (clearing > 0.35f ? 1f : 0.25f)) continue;
                MapView.Tree(forest, p, R(), R() < 0.55f ? 0.1f : 0.8f, R(), 0.9f + R() * 0.7f + Mathf.Min(d, 20f) * 0.02f, hostile);
                trees++;
            }

            // ---- tepeler (uzakta) ----
            var hills = Gen.Group(generated, "Tepeler");
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f + R() * 0.3f;
                float dist = Mathf.Max(playArea.x, playArea.y) * 0.5f + 26f + R() * 14f;
                var p = center + new Vector3(Mathf.Cos(a) * dist, 0, Mathf.Sin(a) * dist * 0.85f);
                if (InLake(p) || InCastle(p)) continue;
                float s = 10f + R() * 10f;
                var hillCol = hostile ? new Color(0.68f, 0.40f, 0.26f) : new Color(0.42f, 0.62f, 0.32f);
                Gen.MeshObj(hills, "Tepe", MeshGen.Blob(i % 6), Art.Lit(Art.Vary(hillCol, R(), 0.15f)), p, new Vector3(s, s * (0.35f + R() * 0.25f), s * 0.9f));
            }

            // ---- kayalar, çiçekler ----
            var deco = Gen.Group(generated, "Süsler");
            for (int i = 0; i < 160; i++)
            {
                var p = center + new Vector3((R() - 0.5f) * worldSize * 0.7f, 0, (R() - 0.5f) * worldSize * 0.7f);
                if (!Outside(p, 1f) || InLake(p) || InVillage(p) || InCastle(p)) continue;
                if (R() < 0.4f)
                    Gen.MeshObj(deco, "Kaya", MeshGen.Rock(i % 8), Art.Lit(Art.Vary(new Color(0.62f, 0.62f, 0.62f), R(), 0.12f), Art.StoneTex), p + Vector3.up * 0.1f, Vector3.one * (0.4f + R() * 0.9f), new Vector3(0, R() * 360, 0));
                else
                {
                    Color[] petals = { new Color(1f, 0.95f, 0.55f), new Color(0.98f, 0.6f, 0.7f), new Color(0.75f, 0.7f, 1f), Color.white };
                    var m = Art.Lit(petals[i % 4]);
                    for (int k = 0; k < 6; k++)
                        Visuals.Part(PrimitiveType.Sphere, deco, p + new Vector3((R() - 0.5f) * 1.2f, 0.06f, (R() - 0.5f) * 1.2f), Vector3.one * 0.1f, m);
                }
            }

            // performans: yüzlerce küçük parçayı birkaç mesh'e indir
            MeshGen.CombineChildren(forest);
            MeshGen.CombineChildren(deco);
            MeshGen.CombineChildren(hills);
            Gen.MarkGenerated(generated.gameObject);
        }
    }

    /// <summary>Yel değirmeni kanatlarını döndürür.</summary>
    public class Rotator : MonoBehaviour
    {
        public float Speed = 30f;
        void Update() => transform.Rotate(0, 0, Speed * Time.deltaTime, Space.Self);
    }
}

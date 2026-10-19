using System.Collections.Generic;
using UnityEngine;

namespace BlindAttack.Game
{
    public enum TimeOfDay { Morning, Sunset, Night }

    /// <summary>Bir günün saatine ait ışık ve renk ayarları.</summary>
    public struct LookPreset
    {
        public Vector3 SunEuler;
        public Color Sun; public float SunIntensity, Shadow;
        public Color Sky, Equator, Ground;
        public Color Haze; public float FogStart, FogEnd;
        public float DayLight;           // uzak zeminin aydınlığı (gece karanlık)
        public float Saturation, Exposure, Bloom;
        public Color Filter;
    }

    /// <summary>
    /// Her haritanın kendi havası: Nehir Vadisi sabah sisi, Kale Bahçesi gün batımı, Taş Ocağı ay ışığında gece.
    /// Ayrıca oyun alanında uçuşan canlılık: kuş sürüleri, sis, ateş böcekleri, düşen yapraklar.
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        public static TimeOfDay ForMap(int mapIndex)
        {
            switch (((mapIndex % 3) + 3) % 3)
            {
                case 1: return TimeOfDay.Sunset;
                case 2: return TimeOfDay.Night;
                default: return TimeOfDay.Morning;
            }
        }

        public static string Name(TimeOfDay t) => t == TimeOfDay.Morning ? "Sabah" : t == TimeOfDay.Sunset ? "Gün batımı" : "Gece";

        public static LookPreset Preset(TimeOfDay t, bool hostile)
        {
            LookPreset p;
            switch (t)
            {
                case TimeOfDay.Sunset:
                    p = new LookPreset
                    {
                        SunEuler = new Vector3(16f, 48f, 0f), Sun = new Color(1.0f, 0.66f, 0.40f), SunIntensity = 1.55f, Shadow = 0.72f,
                        Sky = new Color(0.58f, 0.50f, 0.74f), Equator = new Color(0.86f, 0.58f, 0.48f), Ground = new Color(0.36f, 0.26f, 0.22f),
                        Haze = new Color(0.95f, 0.66f, 0.50f), FogStart = 45f, FogEnd = 130f, DayLight = 0.85f,
                        Saturation = 16f, Exposure = 0.05f, Bloom = 0.7f, Filter = new Color(1f, 0.93f, 0.86f),
                    };
                    break;
                case TimeOfDay.Night:
                    p = new LookPreset
                    {
                        SunEuler = new Vector3(55f, 30f, 0f), Sun = new Color(0.62f, 0.72f, 1.0f), SunIntensity = 0.8f, Shadow = 0.6f,
                        Sky = new Color(0.34f, 0.40f, 0.66f), Equator = new Color(0.26f, 0.29f, 0.44f), Ground = new Color(0.14f, 0.14f, 0.20f),
                        Haze = new Color(0.13f, 0.16f, 0.30f), FogStart = 35f, FogEnd = 120f, DayLight = 0.3f,
                        Saturation = 2f, Exposure = 0.28f, Bloom = 1.1f, Filter = new Color(0.88f, 0.93f, 1.08f),
                    };
                    break;
                default:
                    p = new LookPreset
                    {
                        SunEuler = new Vector3(26f, 105f, 0f), Sun = new Color(1.0f, 0.93f, 0.82f), SunIntensity = 1.3f, Shadow = 0.55f,
                        Sky = new Color(0.68f, 0.76f, 0.92f), Equator = new Color(0.78f, 0.77f, 0.70f), Ground = new Color(0.42f, 0.37f, 0.29f),
                        Haze = new Color(0.78f, 0.84f, 0.86f), FogStart = 32f, FogEnd = 115f, DayLight = 1f,
                        Saturation = 10f, Exposure = 0.12f, Bloom = 0.5f, Filter = new Color(0.98f, 1f, 1.02f),
                    };
                    break;
            }
            if (hostile)
            {
                // rakip bölge: aynı saat, ama kızıl bir tona bürünür
                var warm = new Color(0.86f, 0.52f, 0.40f);
                p.Haze = Color.Lerp(p.Haze, warm * (0.4f + p.DayLight * 0.6f), 0.35f);
                p.Equator = Color.Lerp(p.Equator, warm, 0.25f);
                p.Sun = Color.Lerp(p.Sun, new Color(1f, 0.78f, 0.6f), 0.25f);
                p.Filter = Color.Lerp(p.Filter, new Color(1f, 0.9f, 0.84f), 0.5f);
                p.Saturation -= 6f;
            }
            return p;
        }

        // ================= Canlılık =================
        static Atmosphere instance;
        TimeOfDay tod;
        Bounds area;
        Transform root;
        float birdTimer;
        readonly List<Bird> birds = new List<Bird>();
        readonly List<Floater> floaters = new List<Floater>();

        public static void Setup(int mapIndex, bool hostile, Bounds playArea)
        {
            if (!Application.isPlaying) return;
            if (instance == null) instance = new GameObject("Atmosfer").AddComponent<Atmosphere>();
            AudioManager.SetNature(ForMap(mapIndex));
            instance.Build(ForMap(mapIndex), playArea);
        }

        void Build(TimeOfDay t, Bounds b)
        {
            if (root != null && tod == t && area.center == b.center) return; // aynı hava zaten kurulu
            tod = t; area = b;
            if (root) Destroy(root.gameObject);
            root = new GameObject("Hava").transform;
            root.SetParent(transform, false);
            birds.Clear(); floaters.Clear();
            birdTimer = 1.5f;

            if (t == TimeOfDay.Morning)
            {
                // yerde süzülen sis bulutları
                var mist = Art.Transparent(new Color(0.95f, 0.97f, 1f, 0.09f));
                for (int i = 0; i < 14; i++)
                {
                    var go = Gen.MeshObj(root, "Sis", MeshGen.Disc(20), mist, RandomPoint(0.25f + Random.value * 0.4f), new Vector3(6f + Random.value * 6f, 1f, 3.5f + Random.value * 3f));
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    floaters.Add(new Floater { T = go.transform, Kind = 0, Vel = new Vector3(0.25f + Random.value * 0.2f, 0, 0.08f), Phase = Random.value * 10f });
                }
            }
            if (t != TimeOfDay.Night)
            {
                // rüzgârla süzülen yaprak/çiçek tozu
                var leafCol = t == TimeOfDay.Morning ? new Color(0.98f, 0.92f, 0.70f) : new Color(0.95f, 0.55f, 0.25f);
                var leaf = Art.Lit(leafCol);
                for (int i = 0; i < 16; i++)
                {
                    var go = Visuals.Part(PrimitiveType.Cube, root, RandomPoint(1f + Random.value * 4f), new Vector3(0.09f, 0.01f, 0.06f), leaf, Random.rotation.eulerAngles);
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    floaters.Add(new Floater { T = go.transform, Kind = 1, Vel = new Vector3(0.6f, -0.25f, 0.2f) * (0.7f + Random.value * 0.6f), Phase = Random.value * 10f });
                }
            }
            if (t != TimeOfDay.Morning)
            {
                // ateş böcekleri
                int n = t == TimeOfDay.Night ? 45 : 18;
                var glow = Art.Glow(new Color(0.85f, 1f, 0.45f), t == TimeOfDay.Night ? 3.5f : 2.2f);
                for (int i = 0; i < n; i++)
                {
                    var go = Visuals.Part(PrimitiveType.Sphere, root, RandomPoint(0.3f + Random.value * 1.4f), Vector3.one * 0.05f, glow);
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    floaters.Add(new Floater { T = go.transform, Kind = 2, Home = go.transform.position, Phase = Random.value * 20f });
                }
            }
        }

        Vector3 RandomPoint(float y)
        {
            var e = area.extents;
            return new Vector3(area.center.x + (Random.value * 2f - 1f) * (e.x + 4f), y, area.center.z + (Random.value * 2f - 1f) * (e.z + 4f));
        }

        void Update()
        {
            if (root == null) return;
            float dt = Time.deltaTime, tm = Time.time;
            var e = area.extents;
            var c = area.center;

            foreach (var f in floaters)
            {
                if (!f.T) continue;
                switch (f.Kind)
                {
                    case 0: // sis
                        f.T.position += f.Vel * dt;
                        if (f.T.position.x > c.x + e.x + 10f) f.T.position = new Vector3(c.x - e.x - 10f, f.T.position.y, c.z + (Random.value * 2f - 1f) * (e.z + 3f));
                        break;
                    case 1: // yaprak
                        f.T.position += (f.Vel + new Vector3(Mathf.Sin(tm * 1.3f + f.Phase) * 0.4f, Mathf.Sin(tm * 2.1f + f.Phase) * 0.15f, 0)) * dt;
                        f.T.Rotate(160f * dt, 90f * dt, 40f * dt);
                        if (f.T.position.y < 0.05f || f.T.position.x > c.x + e.x + 6f)
                            f.T.position = new Vector3(c.x - e.x - 2f + Random.value * e.x * 2f, 3.5f + Random.value * 2f, c.z + (Random.value * 2f - 1f) * (e.z + 2f));
                        break;
                    case 2: // ateş böceği
                        float ph = tm * 0.6f + f.Phase;
                        f.T.position = f.Home + new Vector3(Mathf.Sin(ph * 1.1f) * 1.2f, Mathf.Sin(ph * 1.7f) * 0.35f, Mathf.Cos(ph * 0.9f) * 1.2f);
                        float blink = Mathf.Clamp01(Mathf.Sin(tm * 2.3f + f.Phase * 3f) * 1.6f + 0.2f);
                        f.T.localScale = Vector3.one * (0.015f + 0.05f * blink);
                        break;
                }
            }

            // kuş sürüleri (gündüz)
            if (tod != TimeOfDay.Night)
            {
                birdTimer -= dt;
                if (birdTimer <= 0f)
                {
                    birdTimer = Random.Range(7f, 15f);
                    SpawnFlock();
                }
            }
            for (int i = birds.Count - 1; i >= 0; i--)
            {
                var b = birds[i];
                if (!b.Root) { birds.RemoveAt(i); continue; }
                b.Root.position += b.Vel * dt;
                float flap = Mathf.Sin(tm * b.FlapSpeed + b.Phase) * 38f;
                if (b.WingL) b.WingL.localRotation = Quaternion.Euler(0, 0, flap);
                if (b.WingR) b.WingR.localRotation = Quaternion.Euler(0, 0, -flap);
                if ((b.Root.position - c).magnitude > 60f) { Destroy(b.Root.gameObject); birds.RemoveAt(i); }
            }
        }

        void SpawnFlock()
        {
            var e = area.extents; var c = area.center;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            var start = c - dir * 45f + new Vector3(-dir.z, 0, dir.x) * Random.Range(-e.z, e.z);
            float h = Random.Range(5.5f, 8f);
            int count = Random.Range(3, 7);
            var mat = Art.Lit(tod == TimeOfDay.Sunset ? new Color(0.16f, 0.12f, 0.14f) : new Color(0.22f, 0.22f, 0.26f));
            var side = new Vector3(-dir.z, 0, dir.x);
            for (int i = 0; i < count; i++)
            {
                // V düzeni
                int row = (i + 1) / 2; int s = i % 2 == 0 ? 1 : -1;
                var pos = start - dir * row * 0.9f + side * row * 0.7f * s + Vector3.up * (h + Random.Range(-0.2f, 0.2f));
                var bird = new GameObject("Kuş").transform;
                bird.SetParent(root, false);
                bird.position = pos;
                bird.rotation = Quaternion.LookRotation(dir);
                bird.localScale = Vector3.one * 1.7f;
                Visuals.Part(PrimitiveType.Cube, bird, Vector3.zero, new Vector3(0.07f, 0.06f, 0.24f), mat);
                var wl = new GameObject("Kanat").transform; wl.SetParent(bird, false); wl.localPosition = new Vector3(0.03f, 0, 0);
                Visuals.Part(PrimitiveType.Cube, wl, new Vector3(0.16f, 0, 0), new Vector3(0.32f, 0.015f, 0.12f), mat);
                var wr = new GameObject("Kanat").transform; wr.SetParent(bird, false); wr.localPosition = new Vector3(-0.03f, 0, 0);
                Visuals.Part(PrimitiveType.Cube, wr, new Vector3(-0.16f, 0, 0), new Vector3(0.32f, 0.015f, 0.12f), mat);
                foreach (var r in bird.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                birds.Add(new Bird { Root = bird, WingL = wl, WingR = wr, Vel = dir * Random.Range(2.6f, 3.2f), FlapSpeed = Random.Range(11f, 14f), Phase = Random.value * 6f });
            }
        }

        class Bird { public Transform Root, WingL, WingR; public Vector3 Vel; public float FlapSpeed, Phase; }
        class Floater { public Transform T; public int Kind; public Vector3 Vel, Home; public float Phase; }
    }
}

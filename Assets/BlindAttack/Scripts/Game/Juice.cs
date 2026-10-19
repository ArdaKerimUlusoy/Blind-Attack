using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlindAttack.Game
{
    /// <summary>
    /// Oyun hissini güçlendiren küçük efektler: menzil halkası, "!" işareti, konuşma balonu,
    /// yerden çıkma, portala çekilme, ekran parlaması, baca dumanı, rüzgârda sallanma.
    /// </summary>
    public static class Juice
    {
        // ---------------- Menzil halkası ----------------
        static Mesh ringMesh;

        /// <summary>XZ düzleminde, dış yarıçapı 1 olan halka (iç yarıçap 0.94).</summary>
        public static Mesh RingMesh
        {
            get
            {
                if (ringMesh) return ringMesh;
                const int seg = 48; const float inner = 0.94f;
                var v = new Vector3[seg * 2]; var tris = new int[seg * 6]; var n = new Vector3[seg * 2];
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v[i * 2] = d; v[i * 2 + 1] = d * inner;
                    n[i * 2] = n[i * 2 + 1] = Vector3.up;
                    int j = (i + 1) % seg;
                    int t = i * 6;
                    tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = j * 2;
                    tris[t + 3] = j * 2; tris[t + 4] = i * 2 + 1; tris[t + 5] = j * 2 + 1;
                }
                ringMesh = new Mesh { name = "Ring", vertices = v, triangles = tris, normals = n, hideFlags = HideFlags.DontSave };
                ringMesh.RecalculateBounds();
                return ringMesh;
            }
        }

        /// <summary>Yarıçapı 1 olan, içi hafif dolu, kenarı belirgin menzil halkası. Ölçekle büyütülür.</summary>
        public static GameObject RangeRing(Transform parent, Color color)
        {
            var root = new GameObject("Menzil");
            root.transform.SetParent(parent, false);
            var fill = Gen.MeshObj(root.transform, "Dolgu", MeshGen.Disc(40), Art.Transparent(new Color(color.r, color.g, color.b, 0.10f)), new Vector3(0, 0.03f, 0), new Vector3(2f, 1f, 2f));
            var edge = Gen.MeshObj(root.transform, "Kenar", RingMesh, Art.Transparent(new Color(color.r, color.g, color.b, 0.55f)), new Vector3(0, 0.035f, 0), Vector3.one);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.AddComponent<RingPulse>();
            return root;
        }

        public static void SetRingColor(GameObject ring, Color color)
        {
            if (!ring) return;
            var rs = ring.GetComponentsInChildren<MeshRenderer>();
            if (rs.Length > 0) Art.SetColor(rs[0].sharedMaterial, new Color(color.r, color.g, color.b, 0.10f));
            if (rs.Length > 1) Art.SetColor(rs[1].sharedMaterial, new Color(color.r, color.g, color.b, 0.55f));
        }

        // ---------------- "!" işareti ----------------
        public static void AlertMark(Transform target, float height, Color color)
        {
            if (!target) return;
            var go = new GameObject("Fark Etti");
            go.transform.SetParent(target.parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Effects.Font; tm.text = "!"; tm.fontSize = 96; tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold; tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = Effects.Font.material;
            go.AddComponent<FollowMark>().Init(target, height, 1.1f, true);
        }

        // ---------------- Konuşma balonu ----------------
        static readonly Dictionary<Transform, GameObject> bubbles = new Dictionary<Transform, GameObject>();

        public static void Say(Transform target, string text, float height, float duration = 2.2f)
        {
            if (!target) return;
            if (bubbles.TryGetValue(target, out var old) && old) Object.Destroy(old);
            var go = new GameObject("Konuşma Balonu");
            go.transform.SetParent(target.parent, false);
            float w = text.Length * 0.115f + 0.3f, h = 0.4f;
            var bg = Visuals.Part(PrimitiveType.Quad, go.transform, new Vector3(0, 0, 0.02f), new Vector3(w, h, 1f), Art.Unlit(new Color(0.99f, 0.95f, 0.85f)));
            var border = Visuals.Part(PrimitiveType.Quad, go.transform, new Vector3(0, 0, 0.03f), new Vector3(w + 0.06f, h + 0.06f, 1f), Art.Unlit(new Color(0.35f, 0.22f, 0.12f)));
            var tail = Visuals.Part(PrimitiveType.Quad, go.transform, new Vector3(-w * 0.25f, -h * 0.55f, 0.025f), new Vector3(0.14f, 0.14f, 1f), Art.Unlit(new Color(0.99f, 0.95f, 0.85f)), new Vector3(0, 0, 45));
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var txt = new GameObject("Yazı");
            txt.transform.SetParent(go.transform, false);
            var tm = txt.AddComponent<TextMesh>();
            tm.font = Effects.Font; tm.text = text; tm.fontSize = 64; tm.characterSize = 0.032f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(0.23f, 0.16f, 0.10f);
            txt.GetComponent<MeshRenderer>().sharedMaterial = Effects.Font.material;
            go.AddComponent<FollowMark>().Init(target, height, duration, false);
            bubbles[target] = go;
        }

        // ---------------- Yerden çıkma ----------------
        public static void Rise(GameObject unit, float delay, Transform fxRoot, bool sound)
        {
            if (!unit) return;
            var r = unit.AddComponent<Riser>();
            r.Init(delay, fxRoot, sound);
        }

        // ---------------- Portala çekilme ----------------
        public static void PortalSuck(GameObject hero, Vector3 portalWorld, float duration)
        {
            if (!hero) return;
            hero.AddComponent<PortalSuck>().Init(portalWorld, duration);
        }

        // ---------------- Ekran parlaması ----------------
        public static void ScreenFlash(Color color, float duration)
        {
            if (UiKit.Canvas == null) return;
            var go = new GameObject("Parlama");
            var rt = UiKit.Rect(go, UiKit.Canvas.transform);
            UiKit.Stretch(rt);
            var img = go.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
            go.transform.SetAsLastSibling();
            go.AddComponent<FlashFade>().Init(img, color, duration);
        }

        // ---------------- Paylaşılan duman malzemesi ----------------
        static Material smokeMat;
        public static Material SmokeMat => smokeMat ? smokeMat : (smokeMat = Art.Transparent(new Color(0.82f, 0.80f, 0.78f, 0.45f)));
    }

    /// <summary>Kameraya bakar, hedefi takip eder, süre bitince söner. "!" işareti ve konuşma balonu.</summary>
    public class FollowMark : MonoBehaviour
    {
        Transform target; float height, life, t; bool pop;
        Vector3 baseScale;
        public void Init(Transform tgt, float h, float l, bool popAnim)
        {
            target = tgt; height = h; life = l; pop = popAnim;
            baseScale = transform.localScale;
            Place();
        }
        void Place()
        {
            if (target) transform.position = target.position + Vector3.up * height;
            var cam = Camera.main;
            if (cam) transform.rotation = cam.transform.rotation;
        }
        void LateUpdate()
        {
            t += Time.deltaTime;
            if (!target || !target.gameObject.activeInHierarchy || t >= life) { Destroy(gameObject); return; }
            Place();
            float s;
            if (pop) s = t < 0.15f ? Mathf.Lerp(0.2f, 1.35f, t / 0.15f) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((t - 0.15f) * 6f));
            else s = t < 0.12f ? Mathf.Lerp(0.5f, 1f, t / 0.12f) : 1f;
            if (life - t < 0.2f) s *= (life - t) / 0.2f;
            transform.localScale = baseScale * s;
            if (pop) transform.position += Vector3.up * Mathf.Sin(t * 10f) * 0.04f;
        }
    }

    /// <summary>Birimi yerin altından zıplatarak çıkarır (toz ve gürleme ile).</summary>
    public class Riser : MonoBehaviour
    {
        float delay, t = -1f; Transform fx; bool sound;
        Vector3 basePos, baseScale;
        public void Init(float d, Transform fxRoot, bool snd)
        {
            delay = d; fx = fxRoot; sound = snd;
            basePos = transform.localPosition; baseScale = transform.localScale;
            transform.localScale = Vector3.zero;
        }
        /// <summary>Hemen bitir (atla).</summary>
        public void Finish()
        {
            transform.localPosition = basePos; transform.localScale = baseScale;
            Destroy(this);
        }
        void Update()
        {
            if (delay > 0f) { delay -= Time.deltaTime; return; }
            if (t < 0f)
            {
                t = 0f;
                if (fx)
                {
                    Effects.Burst(fx, transform.position + Vector3.up * 0.1f, new Color(0.55f, 0.42f, 0.28f), 10, 2.6f, 0.09f);
                    Effects.Ring(fx, transform.position, new Color(0.45f, 0.33f, 0.2f, 0.7f), 0.8f);
                }
                if (sound) AudioManager.Play(SoundId.EarthRumble, 0.55f, 0.15f);
            }
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.38f);
            // OutBack: biraz fazla çıkar, geri oturur
            const float c1 = 1.9f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
            transform.localPosition = basePos + Vector3.up * Mathf.Lerp(-0.7f, 0f, e);
            float sq = Mathf.Sin(k * Mathf.PI) * 0.18f;
            transform.localScale = new Vector3(baseScale.x * (e - sq * 0.5f), baseScale.y * (e + sq), baseScale.z * (e - sq * 0.5f));
            if (k >= 1f) Finish();
        }
    }

    /// <summary>Kahraman portala dönerek küçülür ve kaybolur.</summary>
    public class PortalSuck : MonoBehaviour
    {
        Vector3 target, start, baseScale; float dur, t;
        public void Init(Vector3 portal, float d)
        {
            target = portal; dur = d; start = transform.position; baseScale = transform.localScale;
            foreach (var a in GetComponents<UnitAnim>()) a.enabled = false;
        }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = k * k;
            transform.position = Vector3.Lerp(start, target, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.5f;
            transform.Rotate(0, 900f * Time.deltaTime * (0.3f + k), 0, Space.World);
            transform.localScale = baseScale * (1f - e);
            if (k >= 1f) gameObject.SetActive(false);
        }
    }

    public class FlashFade : MonoBehaviour
    {
        Image img; Color c; float dur, t;
        public void Init(Image i, Color col, float d) { img = i; c = col; dur = d; }
        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            if (img) img.color = new Color(c.r, c.g, c.b, c.a * (1f - k) * (1f - k));
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Menzil halkasının yavaş nabzı.</summary>
    public class RingPulse : MonoBehaviour
    {
        Transform edge;
        void Start() { edge = transform.Find("Kenar"); }
        void Update()
        {
            if (edge) edge.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.015f);
        }
    }

    /// <summary>Baca dumanı: oyun sırasında yavaşça yükselen, rüzgârla kayan puflar.</summary>
    public class ChimneySmoke : MonoBehaviour
    {
        float next;
        readonly List<(Transform t, float age, float life, Vector3 vel)> puffs = new List<(Transform, float, float, Vector3)>();
        public static Vector3 Wind = new Vector3(0.25f, 0f, 0.12f);

        void Start() { next = Random.Range(0f, 1.2f); }

        void Update()
        {
            if (!Application.isPlaying) return;
            next -= Time.deltaTime;
            if (next <= 0f && puffs.Count < 6)
            {
                next = Random.Range(0.6f, 1.1f);
                var p = Visuals.Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.06f, Juice.SmokeMat);
                p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.transform.position = transform.position;
                puffs.Add((p.transform, 0f, Random.Range(2.2f, 3f), new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(0.35f, 0.5f), Random.Range(-0.05f, 0.05f))));
            }
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var (t, age, life, vel) = puffs[i];
                if (!t) { puffs.RemoveAt(i); continue; }
                age += Time.deltaTime;
                t.position += (vel + Wind * Mathf.Clamp01(age)) * Time.deltaTime;
                float k = age / life;
                float s = Mathf.Lerp(0.06f, 0.32f, Mathf.Sqrt(k)) * (k > 0.75f ? (1f - k) / 0.25f : 1f);
                t.localScale = Vector3.one * Mathf.Max(0.001f, s) / Mathf.Max(0.001f, transform.lossyScale.x);
                if (age >= life) { Destroy(t.gameObject); puffs.RemoveAt(i); continue; }
                puffs[i] = (t, age, life, vel);
            }
        }
    }

    /// <summary>Ağaç ve çalıların rüzgârda hafifçe sallanması.</summary>
    public class WindSway : MonoBehaviour
    {
        public float Amount = 2.2f;
        Quaternion baseRot; float phase;
        void Start()
        {
            baseRot = transform.localRotation;
            var p = transform.position;
            phase = p.x * 0.35f + p.z * 0.22f;
        }
        void Update()
        {
            if (!Application.isPlaying) return;
            float t = Time.time;
            // ana dalga + rüzgâr esintisi (haritada ilerleyen dalga)
            float gust = Mathf.Sin(t * 0.5f - phase * 0.6f) * 0.5f + 0.5f;
            float a = Mathf.Sin(t * 1.7f + phase) * Amount * (0.6f + gust * 0.8f);
            float b = Mathf.Sin(t * 1.3f + phase * 1.3f) * Amount * 0.5f;
            transform.localRotation = baseRot * Quaternion.Euler(a, 0, b);
        }
    }
}

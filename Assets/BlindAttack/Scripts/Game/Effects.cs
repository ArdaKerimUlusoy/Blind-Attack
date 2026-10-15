using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>Hasar yazıları, parçacık patlamaları, oklar ve can barları.</summary>
    public static class Effects
    {
        static Font font;
        public static Font Font => font ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static void FloatText(Transform parent, Vector3 pos, string text, Color color, float size = 1f)
        {
            var go = new GameObject("FloatText");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.035f * size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = Font.material;
            go.AddComponent<FloatAway>().Init(color);
        }

        public static void Burst(Transform parent, Vector3 pos, Color color, int count, float speed, float size = 0.12f)
        {
            var mat = Art.Lit(color);
            for (int i = 0; i < count; i++)
            {
                var p = Visuals.Part(PrimitiveType.Cube, parent, pos, Vector3.one * size * Random.Range(0.6f, 1.3f), mat, Random.rotation.eulerAngles);
                p.transform.position = pos;
                var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.3f;
                p.AddComponent<Particle>().Init(dir.normalized * speed * Random.Range(0.5f, 1.2f), Random.Range(0.4f, 0.8f));
            }
        }

        /// <summary>Yerçekimsiz küçük parlak parçacık (alev, toz, parıltı).</summary>
        public static void Spark(Transform parent, Vector3 pos, Color color, Vector3 velocity, float size, float life)
        {
            var p = Visuals.Part(PrimitiveType.Cube, parent, pos, Vector3.one * size, Art.Glow(color, 1.5f), Random.rotation.eulerAngles);
            p.transform.position = pos;
            p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.AddComponent<Particle>().Init(velocity, life, 0f);
        }

        public static void Ring(Transform parent, Vector3 pos, Color color, float radius)
        {
            var go = Visuals.Part(PrimitiveType.Cylinder, parent, pos + Vector3.up * 0.05f, new Vector3(0.2f, 0.01f, 0.2f), Art.Transparent(color));
            go.transform.position = pos + Vector3.up * 0.05f;
            go.AddComponent<Expander>().Init(radius * 2f, 0.45f, color);
        }

        public static void Arrow(Transform parent, Vector3 from, Vector3 to)
        {
            var go = new GameObject("Arrow");
            go.transform.SetParent(parent, false);
            Visuals.Part(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.03f, 0.03f, 0.45f), Art.Lit(Art.Wood));
            Visuals.Part(PrimitiveType.Cube, go.transform, new Vector3(0, 0, 0.24f), new Vector3(0.07f, 0.07f, 0.07f), Art.Lit(new Color(0.8f, 0.8f, 0.85f)), new Vector3(0, 0, 45));
            go.AddComponent<Projectile>().Init(from, to, 0.22f);
        }
    }

    public class FloatAway : MonoBehaviour
    {
        float t; Color color; TextMesh tm;
        public void Init(Color c) { color = c; tm = GetComponent<TextMesh>(); }
        void Update()
        {
            t += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 0.9f;
            var cam = Camera.main;
            if (cam) transform.rotation = cam.transform.rotation;
            float a = Mathf.Clamp01(1.6f - t * 1.6f);
            if (tm) tm.color = new Color(color.r, color.g, color.b, a);
            float s = t < 0.12f ? Mathf.Lerp(0.5f, 1.2f, t / 0.12f) : Mathf.Lerp(1.2f, 1f, (t - 0.12f) * 4f);
            transform.localScale = Vector3.one * s;
            if (t > 1.1f) Destroy(gameObject);
        }
    }

    public class Particle : MonoBehaviour
    {
        Vector3 vel; float life, t, gravity = 9f; Vector3 startScale;
        public void Init(Vector3 v, float l, float g = 9f) { vel = v; life = l; gravity = g; startScale = transform.localScale; }
        void Update()
        {
            t += Time.deltaTime;
            vel += Vector3.down * gravity * Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            if (transform.position.y < 0.03f) { var p = transform.position; p.y = 0.03f; transform.position = p; vel *= 0.5f; vel.y = Mathf.Abs(vel.y) * 0.3f; }
            transform.localScale = startScale * Mathf.Clamp01(1f - t / life);
            if (t >= life) Destroy(gameObject);
        }
    }

    public class Expander : MonoBehaviour
    {
        float size, life, t; Color color; Material mat;
        public void Init(float s, float l, Color c) { size = s; life = l; color = c; mat = GetComponent<Renderer>().sharedMaterial; }
        void Update()
        {
            t += Time.deltaTime;
            float k = t / life;
            float s = Mathf.Lerp(0.2f, size, 1f - (1f - k) * (1f - k));
            transform.localScale = new Vector3(s, 0.01f, s);
            Art.SetColor(mat, new Color(color.r, color.g, color.b, color.a * (1f - k)));
            if (t >= life) Destroy(gameObject);
        }
    }

    public class Projectile : MonoBehaviour
    {
        Vector3 a, b; float dur, t;
        public void Init(Vector3 from, Vector3 to, float d) { a = from; b = to; dur = d; transform.position = a; transform.rotation = Quaternion.LookRotation((b - a).normalized + Vector3.up * 0.0001f); }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            transform.position = Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f;
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Birimin üstünde kameraya bakan can barı.</summary>
    public class HpBar : MonoBehaviour
    {
        Transform fill;
        float value = 1f, shown = 1f;
        public bool AlwaysShow;

        public static HpBar Create(Transform parent, float height, Color color, float width = 0.7f)
        {
            var root = new GameObject("HpBar");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0, height, 0);
            var bar = root.AddComponent<HpBar>();
            Visuals.Part(PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(width + 0.06f, 0.12f, 0.01f), Art.Unlit(new Color(0.15f, 0.1f, 0.08f)));
            var pivot = new GameObject("FillPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(-width / 2f, 0, -0.01f);
            Visuals.Part(PrimitiveType.Cube, pivot, new Vector3(width / 2f, 0, 0), new Vector3(width, 0.07f, 0.01f), Art.Unlit(color));
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bar.fill = pivot;
            return bar;
        }

        public void Set(float v) => value = Mathf.Clamp01(v);

        void LateUpdate()
        {
            shown = Mathf.MoveTowards(shown, value, Time.deltaTime * 2.5f);
            fill.localScale = new Vector3(Mathf.Max(0.0001f, shown), 1f, 1f);
            var cam = Camera.main;
            if (cam) transform.rotation = cam.transform.rotation;
            // tam canda gizle (kalabalık görünmesin)
            bool show = AlwaysShow || value < 0.999f;
            foreach (Transform c in transform) c.gameObject.SetActive(show);
        }
    }
}

using System.Collections.Generic;
using BlindAttack.Core;
using PrimeTween;
using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>
    /// Kahramanın izleyeceği rotayı noktalı çizgi ve numaralı rozetlerle gösterir.
    /// Rota: başlangıç -> en yakın hedef -> kalanların en yakını -> ... -> çıkış.
    /// </summary>
    public class RoutePreview : MonoBehaviour
    {
        Transform root;
        readonly List<Transform> dots = new List<Transform>();
        Material dotMat, badgeMat;
        float alpha = 0.9f;

        public void Show(MapView view, IList<Placement> supplies, float opacity = 0.9f)
        {
            Clear();
            if (view == null || view.Map == null) return;
            alpha = opacity;
            root = Gen.Group(view.UnitsRoot, "Rota");
            dotMat = Art.Transparent(new Color(1f, 1f, 1f, 0.85f * alpha));
            badgeMat = Art.Transparent(new Color(0.15f, 0.12f, 0.1f, 0.75f * alpha));
            var plan = RoutePlanner.Make(view.Map, supplies);

            // noktalı çizgi
            var pts = new List<Vector3>();
            foreach (var c in plan.FullPath) pts.Add(MapView.CellToWorld(c) + Vector3.up * 0.06f);
            float carry = 0f;
            const float spacing = 0.33f;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var b = pts[i];
                float len = Vector3.Distance(a, b);
                float d = carry;
                while (d < len)
                {
                    var p = Vector3.Lerp(a, b, d / len);
                    var dot = Gen.MeshObj(root, "Nokta", MeshGen.Disc(8), dotMat, p, new Vector3(0.12f, 1, 0.12f)).transform;
                    dot.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    dots.Add(dot);
                    d += spacing;
                }
                carry = d - len;
            }

            // numaralı rozetler
            for (int k = 0; k < plan.Order.Count; k++)
            {
                var p = supplies[plan.Order[k]];
                Badge(MapView.CellToWorld(p.Cell), (k + 1).ToString(), Visuals.IconColor(p.Type));
            }
            Badge(MapView.CellToWorld(view.Map.Exit), "ÇIKIŞ", new Color(0.55f, 0.9f, 1f), 0.8f);
        }

        void Badge(Vector3 pos, string text, Color color, float size = 1f)
        {
            var b = new GameObject("Rozet " + text).transform;
            b.SetParent(root, false);
            b.localPosition = pos + Vector3.up * 1.25f;
            b.localScale = Vector3.zero;
            Tween.Scale(b, Vector3.one, 0.22f, Ease.OutBack);
            var bg = Gen.MeshObj(b, "Arka", MeshGen.Disc(20), badgeMat, Vector3.zero, new Vector3(0.42f * size * (text.Length > 2 ? 2.2f : 1f), 1, 0.42f * size), new Vector3(-90, 0, 0));
            bg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var go = new GameObject("Yazı");
            go.transform.SetParent(b, false);
            go.transform.localPosition = new Vector3(0, 0, -0.01f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Effects.Font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.03f * size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = Color.Lerp(color, Color.white, 0.35f);
            go.GetComponent<MeshRenderer>().sharedMaterial = Effects.Font.material;
            b.gameObject.AddComponent<Billboard>();
        }

        public void Clear()
        {
            if (root) Gen.Kill(root.gameObject);
            root = null;
            dots.Clear();
        }

        void Update()
        {
            // yürüyen ışık: rotanın yönünü belli eder
            if (dots.Count == 0 || dots[0] == null) return;
            float head = (Time.time * 9f) % (dots.Count + 8);
            for (int i = 0; i < dots.Count; i++)
            {
                float k = Mathf.Clamp01(1f - Mathf.Abs(i - head) / 4f);
                dots[i].localScale = new Vector3(0.12f + k * 0.1f, 1, 0.12f + k * 0.1f);
            }
        }
    }

    /// <summary>Nesneyi her zaman kameraya döndürür.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam) transform.rotation = cam.transform.rotation;
        }
    }
}

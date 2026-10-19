using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlindAttack.Game
{
    /// <summary>Düşük poligonlu, stilize prosedürel mesh'ler (kaya, çalı, koni, çatı...).</summary>
    public static class MeshGen
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = make();
            m.name = key;
            m.hideFlags = HideFlags.DontSave;
            cache[key] = m;
            return m;
        }

        // ---------------- Icosphere ----------------
        static void Icosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var mid = new Dictionary<long, int>();
                var nt = new List<int>();
                var vv = verts;
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int idx)) return idx;
                    vv.Add(((vv[a] + vv[b]) * 0.5f).normalized);
                    mid[key] = vv.Count - 1;
                    return vv.Count - 1;
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = nt;
            }
        }

        static float Bump(Vector3 v, int seed, float amp)
        {
            float p1 = seed * 1.7f, p2 = seed * 2.9f, p3 = seed * 0.6f;
            float n = Mathf.Sin(v.x * 2.3f + p1) * Mathf.Sin(v.y * 2.1f + p2) * Mathf.Sin(v.z * 2.7f + p3)
                    + 0.5f * Mathf.Sin(v.x * 5.1f + p2) * Mathf.Sin(v.y * 4.7f + p3) * Mathf.Sin(v.z * 5.3f + p1);
            return 1f + n * amp;
        }

        /// <summary>Üçgenlerin dışa baktığından emin ol (Unity'de ön yüz = saat yönü).</summary>
        static void FixWinding(List<Vector3> v, List<int> t, Vector3 center)
        {
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - center) < 0f) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
        }

        static Mesh Build(List<Vector3> verts, List<int> tris, bool flat, Vector3? center = null)
        {
            if (center.HasValue) FixWinding(verts, tris, center.Value);
            var m = new Mesh();
            if (flat)
            {
                var fv = new Vector3[tris.Count];
                var ft = new int[tris.Count];
                for (int i = 0; i < tris.Count; i++) { fv[i] = verts[tris[i]]; ft[i] = i; }
                m.vertices = fv; m.triangles = ft;
            }
            else { m.SetVertices(verts); m.SetTriangles(tris, 0); }
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Köşeli, yontulmuş kaya. Merkez (0,0,0), yarıçap ~0.5.</summary>
        public static Mesh Rock(int variant) => Cached("Rock" + variant, () =>
        {
            Icosphere(1, out var v, out var t);
            for (int i = 0; i < v.Count; i++)
            {
                var p = v[i] * Bump(v[i], variant + 3, 0.28f) * 0.5f;
                p.y *= 0.75f;
                v[i] = p;
            }
            return Build(v, t, true, Vector3.zero);
        });

        /// <summary>Yumuşak, tombul küre (çalı ve ağaç yaprakları). Yarıçap ~0.5.</summary>
        public static Mesh Blob(int variant) => Cached("Blob" + variant, () =>
        {
            Icosphere(2, out var v, out var t);
            for (int i = 0; i < v.Count; i++) v[i] = v[i] * Bump(v[i], variant + 11, 0.12f) * 0.5f;
            return Build(v, t, false, Vector3.zero);
        });

        /// <summary>Koni: taban yarıçapı 0.5, yükseklik 1, taban y=0.</summary>
        public static Mesh Cone(int segments = 7) => Cached("Cone" + segments, () =>
        {
            var v = new List<Vector3>(); var t = new List<int>();
            v.Add(new Vector3(0, 1, 0)); v.Add(Vector3.zero);
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % segments;
                t.AddRange(new[] { 0, b, a, 1, a, b });
            }
            return Build(v, t, true, new Vector3(0, 0.3f, 0));
        });

        /// <summary>Konik silindir: alt yarıçap rb, üst rt, yükseklik 1, taban y=0.</summary>
        public static Mesh Cylinder(int segments, float rb, float rt) => Cached($"Cyl{segments}_{rb}_{rt}", () =>
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * rb, 0, Mathf.Sin(a) * rb));
                v.Add(new Vector3(Mathf.Cos(a) * rt, 1, Mathf.Sin(a) * rt));
            }
            int top = v.Count; v.Add(new Vector3(0, 1, 0));
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1, b0 = ((i + 1) % segments) * 2, b1 = b0 + 1;
                t.AddRange(new[] { a0, a1, b0, b0, a1, b1, a1, top, b1 });
            }
            return Build(v, t, true, new Vector3(0, 0.5f, 0));
        });

        /// <summary>Üçgen prizma (çatı): genişlik x=1, derinlik z=1, yükseklik 1, taban y=0.</summary>
        public static Mesh Prism() => Cached("Prism", () =>
        {
            var v = new List<Vector3>
            {
                new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0, 1, -0.5f),
                new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0, 1, 0.5f),
            };
            var t = new List<int> { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 4, 0, 1, 4, 0, 4, 3 };
            return Build(v, t, true, new Vector3(0, 0.33f, 0));
        });

        /// <summary>Yuvarlak kule gövdesi: yarıçap 0.5, yükseklik 1, taban y=0, tuğla dokusu için UV'li.</summary>
        public static Mesh Tower(int segments = 10) => Cached("Tower" + segments, () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f);
                v.Add(p); v.Add(p + Vector3.up);
                float u = i / (float)segments * 3f;
                uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, 1.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                t.AddRange(new[] { a0, a1, b0, b0, a1, b1 });
            }
            int c = v.Count; v.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++) t.AddRange(new[] { i * 2 + 1, c, i * 2 + 3 });
            FixWinding(v, t, new Vector3(0, 0.5f, 0));
            var m = new Mesh();
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        });

        /// <summary>Yarım silindir (sandık kapağı): uzunluk x=1, yarıçap 0.5, düz yüz aşağıda (y=0).</summary>
        public static Mesh HalfCylinder(int segments = 8) => Cached("HalfCyl" + segments, () =>
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI;
                float z = Mathf.Cos(a) * 0.5f, y = Mathf.Sin(a) * 0.5f;
                v.Add(new Vector3(-0.5f, y, z)); v.Add(new Vector3(0.5f, y, z));
            }
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                t.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
            // yan kapaklar
            int cl = v.Count; v.Add(new Vector3(-0.5f, 0, 0));
            int cr = v.Count; v.Add(new Vector3(0.5f, 0, 0));
            for (int i = 0; i < segments; i++)
            {
                t.AddRange(new[] { cl, i * 2, i * 2 + 2 });
                t.AddRange(new[] { cr, i * 2 + 3, i * 2 + 1 });
            }
            return Build(v, t, true, new Vector3(0, 0.2f, 0));
        });

        /// <summary>Düz daire (y=0, yukarı bakar), yarıçap 0.5.</summary>
        public static Mesh Disc(int segments = 16) => Cached("Disc" + segments, () =>
        {
            var v = new List<Vector3> { Vector3.zero }; var t = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < segments; i++) t.AddRange(new[] { 0, 1 + (i + 1) % segments, 1 + i });
            return Build(v, t, false);
        });

        /// <summary>Hücre karelerinden tek bir mesh (bölge gösterimi, su yüzeyi). UV dünya x/z.</summary>
        public static Mesh CellQuads(IEnumerable<Vector2Int> cells, float y, float inset = 0f, float uvScale = 1f)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            foreach (var c in cells)
            {
                int b = v.Count;
                float x0 = c.x + inset, x1 = c.x + 1 - inset, z0 = c.y + inset, z1 = c.y + 1 - inset;
                v.Add(new Vector3(x0, y, z0)); v.Add(new Vector3(x1, y, z0)); v.Add(new Vector3(x0, y, z1)); v.Add(new Vector3(x1, y, z1));
                uv.Add(new Vector2(x0, z0) * uvScale); uv.Add(new Vector2(x1, z0) * uvScale); uv.Add(new Vector2(x0, z1) * uvScale); uv.Add(new Vector2(x1, z1) * uvScale);
                t.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            var m = new Mesh { indexFormat = v.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16, hideFlags = HideFlags.DontSave };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Bir grubun altındaki tüm mesh'leri malzemeye göre birleştirir (çok sayıda dekor için performans).</summary>
        public static void CombineChildren(Transform group)
        {
            var byMat = new Dictionary<Material, List<CombineInstance>>();
            var filters = group.GetComponentsInChildren<MeshFilter>(true);
            var toWorld = group.worldToLocalMatrix;
            foreach (var f in filters)
            {
                var r = f.GetComponent<MeshRenderer>();
                if (r == null || f.sharedMesh == null) continue;
                if (!byMat.TryGetValue(r.sharedMaterial, out var list)) byMat[r.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = f.sharedMesh, transform = toWorld * f.transform.localToWorldMatrix });
            }
            var children = new List<GameObject>();
            foreach (Transform c in group) children.Add(c.gameObject);
            foreach (var c in children) Gen.Kill(c);
            foreach (var kv in byMat)
            {
                var m = new Mesh { indexFormat = IndexFormat.UInt32, name = group.name + "_" + kv.Key.name, hideFlags = HideFlags.DontSave };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject(group.name + " (birleşik)");
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
        }
    }

    /// <summary>Edit modunda ve oyunda güvenle nesne oluşturma/silme yardımcıları.</summary>
    public static class Gen
    {
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        public static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>Edit modunda oluşturulan nesneler sahneye kaydedilmesin (her açılışta yeniden üretilir).</summary>
        public static void MarkGenerated(GameObject root)
        {
            if (Application.isPlaying) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
        }

        public static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}

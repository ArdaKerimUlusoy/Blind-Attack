using System.Collections.Generic;
using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>
    /// Malzeme ve prosedürel "el boyaması" doku üretimi.
    /// Gerçek hand-painted asset'ler geldiğinde Visuals/MapView içindeki modeller değiştirilebilir.
    /// </summary>
    public static class Art
    {
        // Sıcak, masalsı bir palet
        public static readonly Color GrassA = new Color(0.47f, 0.68f, 0.30f);
        public static readonly Color GrassB = new Color(0.62f, 0.78f, 0.36f);
        public static readonly Color GrassDark = new Color(0.30f, 0.50f, 0.22f);
        public static readonly Color PathA = new Color(0.80f, 0.66f, 0.44f);
        public static readonly Color PathB = new Color(0.70f, 0.55f, 0.36f);
        public static readonly Color Stone = new Color(0.66f, 0.62f, 0.56f);
        public static readonly Color StoneDark = new Color(0.45f, 0.42f, 0.40f);
        public static readonly Color WaterA = new Color(0.30f, 0.62f, 0.78f);
        public static readonly Color WaterB = new Color(0.46f, 0.78f, 0.88f);
        public static readonly Color Wood = new Color(0.55f, 0.36f, 0.20f);
        public static readonly Color WoodLight = new Color(0.72f, 0.52f, 0.30f);
        public static readonly Color Leaf = new Color(0.30f, 0.58f, 0.26f);
        public static readonly Color LeafLight = new Color(0.50f, 0.74f, 0.32f);
        public static readonly Color Skin = new Color(0.96f, 0.80f, 0.64f);

        static Material litBase, unlitBase, glowBase, transparentBase;
        static readonly Dictionary<(Color, int, float), Material> cache = new Dictionary<(Color, int, float), Material>();
        static Texture2D stoneTex, barkTex, leafTex, woodTex, waterTex;

        static Material LitBase
        {
            get
            {
                if (litBase == null)
                {
                    litBase = Resources.Load<Material>("BlindAttackLit");
                    if (litBase == null)
                    {
                        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
                        litBase = new Material(sh);
                    }
                }
                return litBase;
            }
        }

        static Material UnlitBase
        {
            get
            {
                if (unlitBase == null)
                {
                    unlitBase = Resources.Load<Material>("BlindAttackUnlit");
                    if (unlitBase == null)
                    {
                        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                        unlitBase = new Material(sh);
                    }
                }
                return unlitBase;
            }
        }

        static Material GlowBase
        {
            get
            {
                if (glowBase == null)
                {
                    glowBase = Resources.Load<Material>("BlindAttackLitGlow");
                    if (glowBase == null) glowBase = LitBase;
                }
                return glowBase;
            }
        }

        static Material TransparentBase
        {
            get
            {
                if (transparentBase == null)
                {
                    transparentBase = Resources.Load<Material>("BlindAttackUnlitTransparent");
                    if (transparentBase == null) transparentBase = UnlitBase;
                }
                return transparentBase;
            }
        }

        /// <summary>Mat, ışık alan malzeme (önbellekli).</summary>
        public static Material Lit(Color c, Texture tex = null, float smooth = 0.05f)
        {
            var key = (c, tex ? tex.GetInstanceID() : 0, smooth);
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(LitBase) { hideFlags = HideFlags.DontSave };
            SetColor(m, c);
            if (tex) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            cache[key] = m;
            return m;
        }

        /// <summary>Kendine özel (paylaşılmayan) kopya; rengi sonradan değiştirilecek nesneler için.</summary>
        public static Material LitInstance(Color c, Texture tex = null) => new Material(Lit(c, tex));

        public static Material Unlit(Color c)
        {
            var m = new Material(UnlitBase);
            SetColor(m, c);
            return m;
        }

        static readonly Dictionary<(Color, float), Material> glowCache = new Dictionary<(Color, float), Material>();

        /// <summary>Kendi ışığını yayan malzeme (önbellekli, paylaşılır).</summary>
        public static Material Glow(Color c, float intensity = 2f)
        {
            if (glowCache.TryGetValue((c, intensity), out var cached) && cached) return cached;
            var m = new Material(GlowBase) { hideFlags = HideFlags.DontSave };
            glowCache[(c, intensity)] = m;
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        public static Material Transparent(Color c)
        {
            var m = new Material(TransparentBase);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            SetColor(m, c);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        // ---------------- Prosedürel dokular ----------------

        public static Texture2D StoneTex => stoneTex ? stoneTex : (stoneTex = Painted(128, Stone, StoneDark, new Color(0.80f, 0.77f, 0.70f), 11, 260, 7f));
        public static Texture2D BarkTex => barkTex ? barkTex : (barkTex = Painted(64, Wood, new Color(0.38f, 0.24f, 0.14f), WoodLight, 23, 140, 4f, stretchY: 3f));
        public static Texture2D LeafTex => leafTex ? leafTex : (leafTex = Painted(128, Leaf, GrassDark, LeafLight, 37, 320, 6f));
        public static Texture2D WaterTex => waterTex ? waterTex : (waterTex = Painted(128, new Color(0.62f, 0.86f, 0.95f), new Color(0.80f, 0.94f, 1f), Color.white, 53, 220, 5f, stretchX: 3f));

        /// <summary>Rengi r'ye göre hafifçe değiştirir (4 basamak; malzeme sayısı patlamasın).</summary>
        public static Color Vary(Color c, float r, float amount)
        {
            float k = (Mathf.Floor(Mathf.Clamp01(r) * 4f) / 3f - 0.5f) * 2f * amount;
            return new Color(Mathf.Clamp01(c.r * (1f + k)), Mathf.Clamp01(c.g * (1f + k * 0.8f)), Mathf.Clamp01(c.b * (1f + k * 0.6f)), c.a);
        }

        public static Texture2D WoodTex => woodTex ? woodTex : (woodTex = Painted(64, WoodLight, Wood, new Color(0.85f, 0.66f, 0.42f), 41, 120, 3f, stretchX: 4f));

        /// <summary>Fırça darbesi lekeleriyle boyanmış küçük, döşenebilir doku.</summary>
        public static Texture2D Painted(int size, Color baseCol, Color dark, Color light, int seed, int dabs, float dabSize, float stretchX = 1f, float stretchY = 1f)
        {
            var px = new Color[size * size];
            var rng = new System.Random(seed);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Noise.Fbm(x / (float)size * 4f, y / (float)size * 4f, seed, true, 4);
                    px[y * size + x] = Color.Lerp(Color.Lerp(dark, baseCol, 0.6f), baseCol, n);
                }
            for (int i = 0; i < dabs; i++)
            {
                float cx = (float)rng.NextDouble() * size, cy = (float)rng.NextDouble() * size;
                float r = dabSize * (0.5f + (float)rng.NextDouble());
                var col = rng.NextDouble() < 0.5 ? light : dark;
                float a = 0.10f + (float)rng.NextDouble() * 0.18f;
                Dab(px, size, size, cx, cy, r * stretchX, r * stretchY, col, a, true);
            }
            return MakeTex(px, size, size, TextureWrapMode.Repeat);
        }

        public static Texture2D MakeTex(Color[] px, int w, int h, TextureWrapMode wrap)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave };
            t.SetPixels(px);
            t.wrapMode = wrap;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            t.Apply(true);
            return t;
        }

        /// <summary>Yumuşak kenarlı eliptik fırça lekesi.</summary>
        public static void Dab(Color[] px, int w, int h, float cx, float cy, float rx, float ry, Color col, float alpha, bool wrap)
        {
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx);
            int y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    int ix = x, iy = y;
                    if (wrap) { ix = ((ix % w) + w) % w; iy = ((iy % h) + h) % h; }
                    else if (ix < 0 || iy < 0 || ix >= w || iy >= h) continue;
                    float a = alpha * (1f - d * d);
                    int k = iy * w + ix;
                    px[k] = Color.Lerp(px[k], col, a);
                }
        }

        // ---------------- Ortaçağ dokuları ----------------
        static Texture2D brickTex, roofTex, thatchTex, plasterTex, clothTex;

        /// <summary>Elle boyanmış gibi duran taş tuğla örgüsü (kale surları, kuleler).</summary>
        public static Texture2D BrickTex => brickTex ? brickTex : (brickTex = MakeBricks(128, 4, 3, new Color(0.72f, 0.68f, 0.60f), new Color(0.50f, 0.46f, 0.42f), 61));

        /// <summary>Kiremit sıraları (kırmızı/kahve çatılar).</summary>
        public static Texture2D RoofTex => roofTex ? roofTex : (roofTex = MakeShingles(128, 6, new Color(0.80f, 0.40f, 0.28f), new Color(0.50f, 0.22f, 0.16f), 71));

        /// <summary>Saman çatı: uzun, sarımsı fırça darbeleri.</summary>
        public static Texture2D ThatchTex => thatchTex ? thatchTex : (thatchTex = Painted(128, new Color(0.82f, 0.68f, 0.40f), new Color(0.55f, 0.42f, 0.22f), new Color(0.95f, 0.84f, 0.55f), 81, 420, 3f, stretchY: 6f));

        /// <summary>Sıvalı duvar.</summary>
        public static Texture2D PlasterTex => plasterTex ? plasterTex : (plasterTex = Painted(64, new Color(0.96f, 0.91f, 0.80f), new Color(0.82f, 0.76f, 0.64f), Color.white, 91, 120, 6f));

        /// <summary>Kumaş (pelerin, sancak, tabard).</summary>
        public static Texture2D ClothTex => clothTex ? clothTex : (clothTex = Painted(64, new Color(0.9f, 0.9f, 0.9f), new Color(0.72f, 0.72f, 0.72f), Color.white, 97, 80, 2.5f, stretchY: 5f));

        static Texture2D MakeBricks(int size, int rows, int cols, Color stone, Color mortar, int seed)
        {
            var px = new Color[size * size];
            var rng = new System.Random(seed);
            int rowH = size / rows, colW = size / cols;
            var shade = new float[rows * 2 * cols * 2];
            for (int i = 0; i < shade.Length; i++) shade[i] = (float)rng.NextDouble();
            for (int y = 0; y < size; y++)
            {
                int r = y / rowH;
                int off = (r % 2) * colW / 2;
                for (int x = 0; x < size; x++)
                {
                    int xx = (x + off) % size;
                    int c = xx / colW;
                    int lx = xx % colW, ly = y % rowH;
                    float edge = Mathf.Min(Mathf.Min(lx, colW - 1 - lx), Mathf.Min(ly, rowH - 1 - ly));
                    float s = shade[(r * cols + c) % shade.Length];
                    float n = Noise.Fbm(x / (float)size * 4f, y / (float)size * 4f, seed, true, 3);
                    var col = Color.Lerp(stone * (0.82f + s * 0.3f), stone * 1.1f, n * 0.6f);
                    // tuğla kenarları hafif yuvarlak ve gölgeli
                    col = Color.Lerp(mortar, col, Mathf.Clamp01((edge - 1f) / 3f));
                    if (ly < 4 && edge > 1f) col = Color.Lerp(col, Color.white, 0.12f); // üst kenar ışığı
                    col.a = 1f;
                    px[y * size + x] = col;
                }
            }
            for (int i = 0; i < 160; i++)
                Dab(px, size, size, (float)rng.NextDouble() * size, (float)rng.NextDouble() * size, 2f + (float)rng.NextDouble() * 4f, 1.5f + (float)rng.NextDouble() * 2f,
                    rng.NextDouble() < 0.5 ? new Color(0.45f, 0.55f, 0.32f) : Color.white, 0.12f, true);
            return MakeTex(px, size, size, TextureWrapMode.Repeat);
        }

        static Texture2D MakeShingles(int size, int rows, Color tile, Color dark, int seed)
        {
            var px = new Color[size * size];
            var rng = new System.Random(seed);
            int rowH = size / rows;
            for (int y = 0; y < size; y++)
            {
                int r = y / rowH;
                float ly = (y % rowH) / (float)rowH;
                for (int x = 0; x < size; x++)
                {
                    float w = size / 8f;
                    float lx = ((x + (r % 2) * w / 2f) % w) / w;
                    // yuvarlak uçlu kiremitler: alt kenarda yay şeklinde koyuluk
                    float arc = 1f - Mathf.Abs(lx - 0.5f) * 2f;
                    float bottom = ly < 0.18f + (1f - arc * arc) * 0.25f ? 1f : 0f;
                    float n = Noise.Fbm(x / (float)size * 4f, y / (float)size * 4f, seed, true, 3);
                    var col = Color.Lerp(tile * (0.85f + n * 0.3f), dark, bottom * 0.65f);
                    col = Color.Lerp(col, col * 1.15f, ly * 0.3f);
                    col.a = 1f;
                    px[y * size + x] = col;
                }
            }
            return MakeTex(px, size, size, TextureWrapMode.Repeat);
        }

        // ---------------- UI sprite'ları ----------------
        static Sprite roundSprite, circleSprite;

        /// <summary>9-slice yuvarlak köşeli sprite (paneller ve butonlar için).</summary>
        public static Sprite RoundRect
        {
            get
            {
                if (roundSprite) return roundSprite;
                const int s = 64, r = 18;
                var px = new Color[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        float a = Mathf.Clamp01(r - d);
                        px[y * s + x] = new Color(1, 1, 1, a);
                    }
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                t.SetPixels(px); t.Apply();
                roundSprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return roundSprite;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circleSprite) return circleSprite;
                const int s = 64;
                var px = new Color[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
                        px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(s / 2f - d));
                    }
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                t.SetPixels(px); t.Apply();
                circleSprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return circleSprite;
            }
        }
    }

    /// <summary>Deterministik değer gürültüsü (dokular ve rastgele dekor için).</summary>
    public static class Noise
    {
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        public static float Value(float x, float y, int seed, int period = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x1 = xi + 1, y1 = yi + 1;
            if (period > 0) { xi = ((xi % period) + period) % period; yi = ((yi % period) + period) % period; x1 = ((x1 % period) + period) % period; y1 = ((y1 % period) + period) % period; }
            float a = Hash(xi, yi, seed), b = Hash(x1, yi, seed), c = Hash(xi, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Çok katmanlı gürültü. tile=true ise x,y [0,4) aralığında döşenebilir.</summary>
        public static float Fbm(float x, float y, int seed, bool tile, int octaves)
        {
            float sum = 0, amp = 0.5f, norm = 0; int period = 4;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y, seed + i * 13, tile ? period : 0) * amp;
                norm += amp; amp *= 0.5f; x *= 2; y *= 2; period *= 2;
            }
            return sum / norm;
        }
    }
}

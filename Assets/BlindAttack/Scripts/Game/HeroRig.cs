using System.Collections.Generic;
using BlindAttack.Core;
using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>
    /// Kahramanın aldığı güçlendirmeleri görünür ve hissedilir yapar:
    ///  Kılıç  -> kılıç büyür, parlar, alev saçar; kombo seti genişler (dönüş, zıplayıp yere vurma), kılıç izi renklenir
    ///  Zırh   -> 1. seviye: göğüslük + armalı tabard + omuzluk + miğfer; 2. seviye: kalkan, dizlik, bilezik, altın süsler.
    ///            Parçalar tek tek "takılır" (büyüyüp yerine oturur).
    ///  Çizme  -> kanatlı altın çizmeler, yürürken toz izi, daha hızlı adım ve vuruş
    ///  İksir  -> yeşil iyileşme parıltısı
    /// </summary>
    public class HeroRig : MonoBehaviour
    {
        UnitAnim anim;
        MeshRenderer blade;
        Transform bladeTip;
        SwordTrail trail;
        readonly List<(GameObject go, int tier)> armorParts = new List<(GameObject, int)>();
        readonly List<GameObject> bootParts = new List<GameObject>();
        readonly List<(Transform t, Vector3 scale, float time)> pops = new List<(Transform, Vector3, float)>();
        Transform aura;
        Material auraMat;
        float auraT = 1f;
        Color auraColor;
        int swordTier, armorTier, combo;
        float speedMul = 1f;
        float flameTimer, dustTimer;
        Vector3 lastPos;
        Vector3 bladeBaseScale;

        public float Damage { get; private set; } = AttackSimulation.HeroDamage;
        public float Armor { get; private set; }
        public float Speed => speedMul;
        public int SwordTier => swordTier;

        public void Init(UnitAnim anim, MeshRenderer blade, Transform bladeBase, Transform tip)
        {
            this.anim = anim; this.blade = blade; bladeTip = tip;
            bladeBaseScale = blade.transform.localScale;

            var a = Gen.MeshObj(transform, "Aura", MeshGen.Blob(0), Art.Transparent(new Color(1, 1, 1, 0)), new Vector3(0, 0.6f, 0), Vector3.one * 1.4f);
            a.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aura = a.transform;
            auraMat = a.GetComponent<MeshRenderer>().sharedMaterial;
            aura.gameObject.SetActive(false);

            trail = gameObject.AddComponent<SwordTrail>();
            trail.Base = bladeBase; trail.Tip = tip; trail.Anim = anim;
            trail.SetColor(new Color(1f, 1f, 1f, 0.45f));
        }

        /// <summary>Zırh parçası ekle (başta gizli). tier: 1 = ilk zırh, 2 = ikinci zırh.</summary>
        public void AddArmorPart(GameObject go, int tier)
        {
            go.SetActive(false);
            armorParts.Add((go, tier));
        }

        public void AddBootPart(GameObject go)
        {
            go.SetActive(false);
            bootParts.Add(go);
        }

        /// <summary>Simülasyondaki yeni değerleri uygula (kutu alındığında).</summary>
        public void SetStats(float damage, float armorReduction, float speed)
        {
            Damage = damage; Armor = armorReduction; speedMul = speed;

            int newSword = Mathf.RoundToInt((damage - AttackSimulation.HeroDamage) / AttackSimulation.SwordBonus);
            if (newSword != swordTier)
            {
                swordTier = newSword;
                float grow = 1f + Mathf.Min(swordTier, 3) * 0.18f;
                blade.transform.localScale = new Vector3(bladeBaseScale.x * (1f + Mathf.Min(swordTier, 3) * 0.25f), bladeBaseScale.y * grow, bladeBaseScale.z);
                blade.transform.localPosition = new Vector3(0, 0.3f * grow, 0);
                if (bladeTip) bladeTip.localPosition = new Vector3(0, 0.55f * grow, 0);
                blade.sharedMaterial = swordTier <= 0 ? Art.Lit(new Color(0.85f, 0.88f, 0.92f), null, 0.6f)
                    : swordTier == 1 ? Art.Glow(new Color(1f, 0.55f, 0.15f), 2f)
                    : Art.Glow(new Color(1f, 0.85f, 0.7f), 3.5f);
                trail.SetColor(TrailColor);
                Pop(blade.transform);
            }

            // kaç zırh alındı: (1 - azaltma) = ArmorFactor^n
            int newArmor = armorReduction <= 0.001f ? 0 : Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(1f - armorReduction) / Mathf.Log(AttackSimulation.ArmorFactor)), 1, 2);
            if (newArmor != armorTier)
            {
                armorTier = newArmor;
                foreach (var (go, tier) in armorParts)
                {
                    bool on = tier <= armorTier;
                    if (on && !go.activeSelf) { go.SetActive(true); Pop(go.transform); }
                    else if (!on) go.SetActive(false);
                }
            }

            bool boots = speed > 1.01f;
            foreach (var b in bootParts)
                if (boots && !b.activeSelf) { b.SetActive(true); Pop(b.transform); }
            if (anim) anim.SpeedMul = speed;
        }

        Color TrailColor => swordTier <= 0 ? new Color(1f, 1f, 1f, 0.45f)
            : swordTier == 1 ? new Color(1f, 0.62f, 0.2f, 0.6f)
            : new Color(1f, 0.92f, 0.55f, 0.75f);

        /// <summary>Kılıç seviyesine göre kombo seti. Seviye arttıkça daha gösterişli hareketler girer.</summary>
        AttackStyle[] ComboSet => swordTier <= 0
            ? new[] { AttackStyle.Chop, AttackStyle.Slash, AttackStyle.Thrust }
            : swordTier == 1
                ? new[] { AttackStyle.Slash, AttackStyle.Chop, AttackStyle.Spin, AttackStyle.Thrust }
                : new[] { AttackStyle.Slash, AttackStyle.Spin, AttackStyle.Chop, AttackStyle.LeapSlam, AttackStyle.Thrust, AttackStyle.Spin };

        public AttackStyle PeekAttack() { var set = ComboSet; return set[combo % set.Length]; }

        /// <summary>Sıradaki kombo hareketini başlat; darbe anı timeToImpact saniye sonra olur.</summary>
        public AttackStyle Attack(float timeToImpact)
        {
            var s = PeekAttack();
            combo++;
            anim.Attack(s, timeToImpact);
            float dur = UnitAnim.Duration(s) / Mathf.Max(0.3f, speedMul);
            float impact = UnitAnim.ImpactTime(s) / Mathf.Max(0.3f, speedMul);
            // iz: darbeden biraz önce başlayıp takip hareketinde biter
            trail.Emit(Mathf.Max(0f, timeToImpact - impact * 0.45f), Mathf.Min(dur, timeToImpact + 0.14f));
            return s;
        }

        /// <summary>Kahramanın etrafında kısa bir renkli parıltı.</summary>
        public void Pulse(Color c)
        {
            auraColor = c;
            auraT = 0f;
            aura.gameObject.SetActive(true);
        }

        /// <summary>Zırhlıyken darbe alınca mavi kıvılcım.</summary>
        public void Blocked()
        {
            if (armorTier > 0)
                Effects.Burst(transform.parent, transform.position + Vector3.up * 0.6f, new Color(0.6f, 0.85f, 1f), 5, 2.5f, 0.05f);
        }

        void Pop(Transform t)
        {
            pops.RemoveAll(p => p.t == t);
            pops.Add((t, t.localScale, 0f));
            t.localScale = Vector3.zero;
            Effects.Burst(transform.parent, t.position, new Color(1f, 0.95f, 0.7f), 4, 1.6f, 0.04f);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // parça takılma animasyonu: 0 -> %120 -> %100
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (t, scale, time) = pops[i];
                time += dt;
                if (t == null) { pops.RemoveAt(i); continue; }
                float k = Mathf.Clamp01(time / 0.35f);
                float s = k < 0.6f ? Mathf.Lerp(0f, 1.2f, k / 0.6f) : Mathf.Lerp(1.2f, 1f, (k - 0.6f) / 0.4f);
                t.localScale = scale * s;
                if (k >= 1f) { t.localScale = scale; pops.RemoveAt(i); }
                else pops[i] = (t, scale, time);
            }

            if (auraT < 1f)
            {
                auraT += dt * 1.6f;
                float k = Mathf.Clamp01(auraT);
                aura.localScale = Vector3.one * Mathf.Lerp(0.9f, 2.2f, k);
                Art.SetColor(auraMat, new Color(auraColor.r, auraColor.g, auraColor.b, 0.55f * (1f - k)));
                if (auraT >= 1f) aura.gameObject.SetActive(false);
            }

            // alevli kılıç
            if (swordTier > 0 && bladeTip && gameObject.activeInHierarchy)
            {
                flameTimer -= dt;
                if (flameTimer <= 0f)
                {
                    flameTimer = swordTier >= 2 ? 0.03f : 0.06f;
                    var col = Random.value < 0.5f ? new Color(1f, 0.55f, 0.1f) : new Color(1f, 0.85f, 0.3f);
                    var p = Vector3.Lerp(bladeTip.position, blade.transform.position, Random.value * 0.8f) + Random.insideUnitSphere * 0.05f;
                    Effects.Spark(transform.parent, p, col, new Vector3(0, 0.8f + Random.value, 0), 0.05f + swordTier * 0.015f, 0.4f);
                }
            }

            // çizme tozu
            var pos = transform.position;
            bool moving = (pos - lastPos).sqrMagnitude > 1e-6f;
            lastPos = pos;
            if (speedMul > 1.01f && moving)
            {
                dustTimer -= dt;
                if (dustTimer <= 0f)
                {
                    dustTimer = 0.07f;
                    Effects.Spark(transform.parent, pos + new Vector3(Random.Range(-0.15f, 0.15f), 0.05f, Random.Range(-0.15f, 0.15f)),
                        Random.value < 0.5f ? new Color(0.85f, 0.75f, 0.55f) : new Color(1f, 0.85f, 0.3f), new Vector3(0, 0.4f, 0), 0.07f, 0.45f);
                }
            }
        }
    }

    /// <summary>
    /// Kılıç izi: savuruş sırasında kılıcın taradığı alanı kısa süreli, uca doğru incelen parlak bir şerit olarak çizer.
    /// </summary>
    public class SwordTrail : MonoBehaviour
    {
        public Transform Base, Tip;
        public UnitAnim Anim;
        public float Life = 0.17f;

        struct Sample { public Vector3 b, t; public float time; }
        readonly List<Sample> samples = new List<Sample>();
        float clock, emitFrom = -1f, emitTo = -1f;
        GameObject obj;
        Mesh mesh;
        Material mat;
        Color color = new Color(1, 1, 1, 0.45f);

        public void SetColor(Color c)
        {
            color = c;
            if (mat) Art.SetColor(mat, c);
        }

        /// <summary>delay saniye sonra başla, duration saniye boyunca iz bırak (animasyon zamanında).</summary>
        public void Emit(float delay, float duration)
        {
            emitFrom = clock + delay;
            emitTo = clock + delay + duration;
        }

        void EnsureObject()
        {
            if (obj) return;
            obj = new GameObject("Kılıç İzi");
            obj.transform.SetParent(transform.parent, false);
            mesh = new Mesh { name = "SwordTrail" };
            mesh.MarkDynamic();
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = obj.AddComponent<MeshRenderer>();
            mat = Art.Transparent(color);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void LateUpdate()
        {
            if (Base == null || Tip == null) return;
            float scale = Anim ? Mathf.Max(0f, Anim.TimeScale) : 1f;
            clock += Time.deltaTime * scale;
            EnsureObject();
            bool emitting = clock >= emitFrom && clock <= emitTo && gameObject.activeInHierarchy;
            if (emitting && scale > 0f)
            {
                var b = Vector3.Lerp(Base.position, Tip.position, 0.25f);
                samples.Add(new Sample { b = b, t = Tip.position, time = clock });
            }
            samples.RemoveAll(s => clock - s.time > Life);

            mesh.Clear();
            if (samples.Count < 2) return;
            int n = samples.Count;
            var verts = new Vector3[n * 2];
            var tris = new List<int>((n - 1) * 12);
            var inv = obj.transform;
            for (int i = 0; i < n; i++)
            {
                var s = samples[i];
                float k = 1f - Mathf.Clamp01((clock - s.time) / Life); // yeni = 1, eski = 0
                verts[i * 2] = inv.InverseTransformPoint(Vector3.Lerp(s.t, s.b, k));   // eski uçta incelir
                verts[i * 2 + 1] = inv.InverseTransformPoint(s.t);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.AddRange(new[] { a, b, c, c, b, d, a, c, b, c, d, b }); // çift taraflı
            }
            mesh.vertices = verts;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            // iz bittikçe solar
            float newest = 1f - Mathf.Clamp01((clock - samples[n - 1].time) / Life);
            Art.SetColor(mat, new Color(color.r, color.g, color.b, color.a * Mathf.Lerp(0.3f, 1f, newest)));
        }

        void OnDestroy()
        {
            if (obj) Destroy(obj);
        }
    }
}

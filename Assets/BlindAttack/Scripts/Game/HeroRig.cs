using BlindAttack.Core;
using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>
    /// Kahramanın aldığı güçlendirmeleri görünür yapar:
    ///  Kılıç  -> kılıç büyür, turuncu sonra kırmızı-beyaz parlar, alev saçar
    ///  Zırh   -> göğüs zırhı + omuzluk + miğfer; ikinci zırhta altın renk ve kalkan
    ///  Çizme  -> kanatlı altın çizmeler, yürürken toz izi, daha hızlı adım
    ///  İksir  -> yeşil iyileşme parıltısı
    /// </summary>
    public class HeroRig : MonoBehaviour
    {
        UnitAnim anim;
        MeshRenderer blade;
        Transform bladeTip;
        GameObject armor, helmet, shield, boots;
        Material steel;
        Transform aura;
        Material auraMat;
        float auraT = 1f;
        Color auraColor;
        int swordTier, armorTier;
        float speedMul = 1f;
        float flameTimer, dustTimer;
        Vector3 lastPos;
        Vector3 bladeBaseScale;

        public float Damage { get; private set; } = AttackSimulation.HeroDamage;
        public float Armor { get; private set; }
        public float Speed => speedMul;

        public void Init(UnitAnim anim, MeshRenderer blade, Transform tip, GameObject armor, GameObject helmet, GameObject shield, GameObject boots, Material steel)
        {
            this.anim = anim; this.blade = blade; bladeTip = tip;
            this.armor = armor; this.helmet = helmet; this.shield = shield; this.boots = boots; this.steel = steel;
            bladeBaseScale = blade.transform.localScale;
            armor.SetActive(false); shield.SetActive(false); boots.SetActive(false);

            var a = Gen.MeshObj(transform, "Aura", MeshGen.Blob(0), Art.Transparent(new Color(1, 1, 1, 0)), new Vector3(0, 0.6f, 0), Vector3.one * 1.4f);
            a.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aura = a.transform;
            auraMat = a.GetComponent<MeshRenderer>().sharedMaterial;
            aura.gameObject.SetActive(false);
        }

        /// <summary>Simülasyondaki yeni değerleri uygula (kutu alındığında).</summary>
        public void SetStats(float damage, float armorReduction, float speed)
        {
            Damage = damage; Armor = armorReduction; speedMul = speed;

            int newSword = Mathf.RoundToInt((damage - AttackSimulation.HeroDamage) / 6f);
            if (newSword != swordTier)
            {
                swordTier = newSword;
                blade.transform.localScale = new Vector3(bladeBaseScale.x * (1f + swordTier * 0.25f), bladeBaseScale.y * (1f + swordTier * 0.2f), bladeBaseScale.z);
                blade.transform.localPosition = new Vector3(0, 0.3f * (1f + swordTier * 0.2f), 0);
                if (bladeTip) bladeTip.localPosition = new Vector3(0, 0.55f * (1f + swordTier * 0.2f), 0);
                blade.sharedMaterial = swordTier <= 0 ? Art.Lit(new Color(0.85f, 0.88f, 0.92f), null, 0.6f)
                    : swordTier == 1 ? Art.Glow(new Color(1f, 0.55f, 0.15f), 2f)
                    : Art.Glow(new Color(1f, 0.85f, 0.7f), 3.5f);
            }

            int newArmor = armorReduction <= 0.001f ? 0 : armorReduction < 0.45f ? 1 : 2;
            if (newArmor != armorTier)
            {
                armorTier = newArmor;
                armor.SetActive(armorTier > 0);
                shield.SetActive(armorTier >= 2);
                var mat = armorTier >= 2 ? Art.Lit(new Color(0.95f, 0.78f, 0.30f), null, 0.6f) : steel;
                foreach (var r in armor.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name != "Sorguç") r.sharedMaterial = mat;
            }

            boots.SetActive(speed > 1.01f);
            if (anim) anim.SpeedMul = speed;
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

        void Update()
        {
            float dt = Time.deltaTime;
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
                    var p = bladeTip.position + Random.insideUnitSphere * 0.08f + Vector3.down * Random.value * 0.3f;
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
}

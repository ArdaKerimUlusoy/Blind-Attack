using System;
using UnityEngine;

namespace BlindAttack.Game
{
    public enum AttackStyle
    {
        None,
        // kahraman (kılıç)
        Chop,       // tepeden indirme
        Slash,      // yatay kesiş
        Thrust,     // ileri saplama
        Spin,       // 360° dönüş (kılıç seviyesi 1+)
        LeapSlam,   // zıplayıp yere vurma (kılıç seviyesi 2+)
        // düşmanlar
        QuickStab,  // goblin hızlı saplama
        QuickSlash, // goblin hızlı kesiş
        HeavySmash, // dev: uzun hazırlıklı ezici darbe
        BowShot,    // okçu: yayı ger ve bırak
    }

    /// <summary>Beklerken yapılan karakterli hareket (kişilik).</summary>
    public enum FidgetKind { None, Goblin, Ogre, Archer, Hero }

    /// <summary>
    /// Prosedürel karakter animasyonu.
    ///  - Yürüme: kalçadan sallanan bacaklar, ters yönde kollar, adım başına iki kez yaylanma, öne eğilme, yalpalama.
    ///    Hareket başlarken/biterken yumuşak geçiş (aniden donmaz).
    ///  - Bekleme: nefes alma, hafif ağırlık aktarımı, ara sıra etrafa bakınma.
    ///  - Saldırı: anahtar kareli pozlar (hazırlık -> darbe -> takip -> toparlanma). Darbe anı hasar olayına denk getirilir.
    ///  - Darbe alma: yaylı geri savrulma. Ölüm: geriye sekerek düşme, sonra toprağa gömülme.
    /// </summary>
    public class UnitAnim : MonoBehaviour
    {
        // ---- rig ----
        Transform body, weapon, legL, legR, armL, head;
        Quaternion weaponRest, legLRest, legRRest, armLRest, headRest;
        Vector3 bodyBase, weaponBasePos;

        // ---- karakter ayarları ----
        public float StepRate = 10f;     // rad/s (bir tam döngü = iki adım)
        public float LegSwing = 32f;     // derece
        public float ArmSwing = 24f;
        public float Bob = 0.05f;
        public float Lean = 7f;
        public float Roll = 3f;
        public AttackStyle[] Styles = { AttackStyle.Chop };

        // ---- dışarıdan kontrol ----
        public bool Moving;
        public float SpeedMul = 1f;      // hız çizmesi: yürüme ve saldırı hızlanır
        public float TimeScale = 1f;     // oynatma hızı (2x, atla) ve vuruş anı donması
        public Action OnStep;
        /// <summary>Beklerken ara sıra yapılan hareket (goblin kafa kaşır, dev esner, okçu yayını yoklar).</summary>
        public FidgetKind Fidget = FidgetKind.None;

        float moveBlend, walkPhase, idleT, deadT = -1f, hurtT = 9f;
        float lastStepSign;
        AttackStyle style = AttackStyle.None;
        float atkT;
        int styleIndex;

        // kişilik hareketi
        Key[] fidKeys; float fidT, fidgetTimer = -1f;

        public bool IsDead => deadT >= 0f;
        public bool Attacking => style != AttackStyle.None;
        public AttackStyle CurrentStyle => style;

        public void Init(Transform body, Transform weapon)
        {
            this.body = body; this.weapon = weapon;
            weaponRest = weapon ? weapon.localRotation : Quaternion.identity;
            weaponBasePos = weapon ? weapon.localPosition : Vector3.zero;
            bodyBase = body.localPosition;
            idleT = UnityEngine.Random.Range(0f, 100f);
        }

        public void SetRig(Transform legL, Transform legR, Transform armL, Transform head)
        {
            this.legL = legL; this.legR = legR; this.armL = armL; this.head = head;
            if (legL) legLRest = legL.localRotation;
            if (legR) legRRest = legR.localRotation;
            if (armL) armLRest = armL.localRotation;
            if (head) headRest = head.localRotation;
        }

        /// <summary>Sıradaki varsayılan saldırı stili (karakterin kendi döngüsünden).</summary>
        public AttackStyle PeekStyle() => Styles == null || Styles.Length == 0 ? AttackStyle.Chop : Styles[styleIndex % Styles.Length];

        public void Attack() => Attack(-1f);

        /// <summary>Karakterin kendi stil döngüsünden bir saldırı başlat.</summary>
        public void Attack(float timeToImpact)
        {
            var s = PeekStyle();
            styleIndex++;
            Attack(s, timeToImpact);
        }

        /// <summary>Saldırı başlat. timeToImpact &gt;= 0 ise darbe anı tam o kadar saniye sonra olacak şekilde hazırlığın bir kısmı atlanır.</summary>
        public void Attack(AttackStyle s, float timeToImpact)
        {
            if (IsDead) return;
            style = s;
            float impact = ImpactTime(s);
            atkT = timeToImpact >= 0f ? Mathf.Max(0f, impact - timeToImpact * SpeedMul) : 0f;
        }

        public void Hurt() { if (!IsDead) { hurtT = 0f; fidKeys = null; } }

        /// <summary>Kahramanı fark etti: kısa zıplama.</summary>
        public void Alert() { if (!IsDead && style == AttackStyle.None) { fidKeys = AlertKeys; fidT = 0f; } }

        /// <summary>Kutlama: silahı havaya kaldır.</summary>
        public void Cheer() { if (!IsDead) { style = AttackStyle.None; fidKeys = CheerKeys; fidT = 0f; } }
        public void Die() { if (deadT < 0f) { deadT = 0f; style = AttackStyle.None; } }

        // ================= POZLAR =================
        struct Pose
        {
            public Vector3 arm;     // silah kolu dönüşü (derece)
            public Vector3 armOff;  // silah kolu kayması
            public Vector3 body;    // gövde: eğilme (x), dönme (y), yana yatma (z)
            public Vector3 bodyOff; // gövde: ileri (z), yukarı (y)
            public static Pose Lerp(Pose a, Pose b, float k) => new Pose
            {
                arm = Vector3.LerpUnclamped(a.arm, b.arm, k),
                armOff = Vector3.LerpUnclamped(a.armOff, b.armOff, k),
                body = Vector3.LerpUnclamped(a.body, b.body, k),
                bodyOff = Vector3.LerpUnclamped(a.bodyOff, b.bodyOff, k),
            };
        }

        enum Ease { Smooth, In, Out, Linear }
        struct Key { public float t; public Pose p; public Ease e; }

        static Pose P(Vector3 arm, Vector3 body = default, Vector3 bodyOff = default, Vector3 armOff = default)
            => new Pose { arm = arm, body = body, bodyOff = bodyOff, armOff = armOff };
        static Key K(float t, Pose p, Ease e = Ease.Smooth) => new Key { t = t, p = p, e = e };
        static readonly Pose Rest = default;

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // anahtar kareler: (zaman sn, poz). Darbe anı ImpactTime ile aynı.
        static readonly Key[] ChopKeys =
        {
            K(0f, Rest),
            K(0.16f, P(V(-198, 0, 12), V(-8, 8, 0), V(0, 0.02f, -0.03f)), Ease.Out),
            K(0.27f, P(V(-28, 0, 0), V(16, -4, 0), V(0, -0.03f, 0.10f)), Ease.In),
            K(0.38f, P(V(-14, 0, 0), V(11, -2, 0), V(0, -0.02f, 0.08f))),
            K(0.62f, Rest),
        };
        static readonly Key[] SlashKeys =
        {
            K(0f, Rest),
            K(0.15f, P(V(-95, 80, 0), V(0, 40, -4), V(0, -0.01f, -0.02f)), Ease.Out),
            K(0.25f, P(V(-95, -55, 0), V(6, -32, 4), V(0, -0.02f, 0.07f)), Ease.In),
            K(0.36f, P(V(-85, -85, 0), V(4, -40, 3), V(0, -0.01f, 0.06f))),
            K(0.62f, Rest),
        };
        static readonly Key[] ThrustKeys =
        {
            K(0f, Rest),
            K(0.15f, P(V(-60, 15, 0), V(-5, 18, 0), V(0, 0, -0.05f), V(0, 0, -0.08f)), Ease.Out),
            K(0.25f, P(V(-100, 0, 0), V(15, -6, 0), V(0, -0.03f, 0.18f), V(0, 0, 0.12f)), Ease.In),
            K(0.40f, P(V(-95, 0, 0), V(10, -2, 0), V(0, -0.02f, 0.14f), V(0, 0, 0.08f))),
            K(0.64f, Rest),
        };
        static readonly Key[] SpinKeys =
        {
            K(0f, Rest),
            K(0.10f, P(V(-90, 90, 0), V(0, 30, 0), V(0, -0.04f, 0)), Ease.Out),
            K(0.42f, P(V(-90, 90, 0), V(4, -330, 0), V(0, -0.02f, 0.06f)), Ease.Linear),
            K(0.62f, Rest),
        };
        static readonly Key[] LeapKeys =
        {
            K(0f, Rest),
            K(0.14f, P(V(-200, 0, 10), V(-6, 0, 0), V(0, -0.09f, 0)), Ease.Out),
            K(0.30f, P(V(-208, 0, 10), V(-12, 0, 0), V(0, 0.40f, 0.12f)), Ease.Out),
            K(0.42f, P(V(-22, 0, 0), V(22, 0, 0), V(0, -0.05f, 0.22f)), Ease.In),
            K(0.55f, P(V(-18, 0, 0), V(15, 0, 0), V(0, -0.04f, 0.20f))),
            K(0.82f, Rest),
        };
        static readonly Key[] QuickStabKeys =
        {
            K(0f, Rest),
            K(0.08f, P(V(-50, 10, 0), V(-4, 12, 0), V(0, 0, -0.03f), V(0, 0, -0.05f)), Ease.Out),
            K(0.15f, P(V(-95, 0, 0), V(14, -6, 0), V(0, -0.02f, 0.09f), V(0, 0, 0.08f)), Ease.In),
            K(0.36f, Rest),
        };
        static readonly Key[] QuickSlashKeys =
        {
            K(0f, Rest),
            K(0.09f, P(V(-100, 60, 0), V(0, 28, -5), V(0, -0.01f, 0)), Ease.Out),
            K(0.17f, P(V(-90, -50, 0), V(8, -22, 5), V(0, -0.02f, 0.06f)), Ease.In),
            K(0.38f, Rest),
        };
        static readonly Key[] HeavyKeys =
        {
            K(0f, Rest),
            K(0.40f, P(V(-200, 0, 15), V(-15, 10, 0), V(0, 0.03f, -0.06f)), Ease.Out),
            K(0.52f, P(V(-18, 0, 0), V(24, -4, 0), V(0, -0.07f, 0.13f)), Ease.In),
            K(0.76f, P(V(-14, 0, 0), V(17, -2, 0), V(0, -0.05f, 0.10f))),
            K(1.10f, Rest),
        };
        static readonly Key[] BowKeys =
        {
            K(0f, Rest),
            K(0.28f, P(V(-14, 0, 0), V(-7, 8, 0), V(0, 0, -0.02f), V(0, 0, 0.02f)), Ease.Out),
            K(0.32f, P(V(-8, 0, 0), V(-9, 8, 0), V(0, 0, -0.03f))),
            K(0.42f, P(V(-32, 0, 0), V(5, 0, 0), V(0, 0, 0.02f)), Ease.Out),
            K(0.72f, Rest),
        };

        // ---- kişilik hareketleri ----
        static readonly Key[] GoblinFidget =
        {
            K(0f, Rest),
            K(0.2f, P(V(-150, 30, -30), V(-4, 15, 6))),
            K(0.34f, P(V(-138, 42, -20), V(-4, 20, 8))),
            K(0.48f, P(V(-150, 30, -30), V(-4, 15, 6))),
            K(0.62f, P(V(-138, 42, -20), V(-4, 20, 8))),
            K(0.85f, Rest),
            K(0.95f, P(V(-10, 0, 0), V(-6, 0, 0), V(0, 0.11f, 0)), Ease.Out),
            K(1.1f, Rest),
        };
        static readonly Key[] OgreFidget =
        {
            K(0f, Rest),
            K(0.8f, P(V(-160, 0, 25), V(-16, 0, 0), V(0, 0.04f, 0)), Ease.Out),
            K(1.5f, P(V(-172, 0, 30), V(-19, 0, 4), V(0, 0.05f, 0))),
            K(2.1f, Rest),
        };
        static readonly Key[] ArcherFidget =
        {
            K(0f, Rest),
            K(0.3f, P(V(-40, 40, 0), V(0, 25, 0))),
            K(0.9f, P(V(-46, 52, 0), V(4, 32, 0))),
            K(1.25f, Rest),
        };
        static readonly Key[] HeroFidget =
        {
            K(0f, Rest),
            K(0.22f, P(V(-80, 60, 0), V(0, 12, 0))),
            K(0.5f, P(V(-85, -60, 0), V(0, -12, 0))),
            K(0.8f, Rest),
        };
        static readonly Key[] AlertKeys =
        {
            K(0f, Rest),
            K(0.1f, P(V(-30, 0, 0), V(8, 0, 0), V(0, 0.13f, 0)), Ease.Out),
            K(0.3f, Rest),
        };
        static readonly Key[] CheerKeys =
        {
            K(0f, Rest),
            K(0.2f, P(V(-175, 0, 0), V(-10, 0, 0), V(0, 0.16f, 0)), Ease.Out),
            K(0.45f, P(V(-180, 0, 0), V(-8, 0, 0), V(0, 0.02f, 0))),
            K(0.65f, P(V(-175, 0, 0), V(-10, 0, 0), V(0, 0.14f, 0)), Ease.Out),
            K(1.2f, P(V(-178, 0, 0), V(-6, 0, 0), V(0, 0, 0))),
            K(1.6f, Rest),
        };

        static Key[] FidgetKeys(FidgetKind k)
        {
            switch (k)
            {
                case FidgetKind.Goblin: return GoblinFidget;
                case FidgetKind.Ogre: return OgreFidget;
                case FidgetKind.Archer: return ArcherFidget;
                case FidgetKind.Hero: return HeroFidget;
            }
            return null;
        }

        static Key[] KeysOf(AttackStyle s)
        {
            switch (s)
            {
                case AttackStyle.Chop: return ChopKeys;
                case AttackStyle.Slash: return SlashKeys;
                case AttackStyle.Thrust: return ThrustKeys;
                case AttackStyle.Spin: return SpinKeys;
                case AttackStyle.LeapSlam: return LeapKeys;
                case AttackStyle.QuickStab: return QuickStabKeys;
                case AttackStyle.QuickSlash: return QuickSlashKeys;
                case AttackStyle.HeavySmash: return HeavyKeys;
                case AttackStyle.BowShot: return BowKeys;
            }
            return null;
        }

        /// <summary>Saldırının başından darbe anına kadar geçen süre (hız çarpanı 1 iken).</summary>
        public static float ImpactTime(AttackStyle s)
        {
            switch (s)
            {
                case AttackStyle.Chop: return 0.27f;
                case AttackStyle.Slash: return 0.25f;
                case AttackStyle.Thrust: return 0.25f;
                case AttackStyle.Spin: return 0.30f;
                case AttackStyle.LeapSlam: return 0.42f;
                case AttackStyle.QuickStab: return 0.15f;
                case AttackStyle.QuickSlash: return 0.17f;
                case AttackStyle.HeavySmash: return 0.52f;
                case AttackStyle.BowShot: return 0.32f;
            }
            return 0f;
        }

        public static float Duration(AttackStyle s)
        {
            var k = KeysOf(s);
            return k == null ? 0f : k[k.Length - 1].t;
        }

        static float Apply(Ease e, float k)
        {
            switch (e)
            {
                case Ease.In: return k * k * k;                          // hızlanarak vur
                case Ease.Out: return 1f - (1f - k) * (1f - k) * (1f - k); // yavaşlayarak hazırlan
                case Ease.Linear: return k;
                default: return k * k * (3f - 2f * k);
            }
        }

        static Pose Sample(Key[] keys, float t)
        {
            if (t <= keys[0].t) return keys[0].p;
            for (int i = 1; i < keys.Length; i++)
            {
                if (t <= keys[i].t)
                {
                    float k = (t - keys[i - 1].t) / Mathf.Max(1e-4f, keys[i].t - keys[i - 1].t);
                    return Pose.Lerp(keys[i - 1].p, keys[i].p, Apply(keys[i].e, Mathf.Clamp01(k)));
                }
            }
            return keys[keys.Length - 1].p;
        }

        // ================= GÜNCELLEME =================
        void Update()
        {
            if (body == null) return;
            float dt = Time.deltaTime * Mathf.Max(0f, TimeScale);
            idleT += dt;

            if (deadT >= 0f) { UpdateDeath(dt); return; }

            // yürümeye yumuşak geçiş
            moveBlend = Mathf.MoveTowards(moveBlend, Moving ? 1f : 0f, dt * 5f);
            float mb = moveBlend * moveBlend * (3f - 2f * moveBlend);
            if (mb > 0.001f) walkPhase += dt * StepRate * Mathf.Lerp(0.6f, 1f, mb) * Mathf.Sqrt(Mathf.Max(0.2f, SpeedMul));
            float s = Mathf.Sin(walkPhase);
            float stepSign = Mathf.Sign(s);
            if (mb > 0.3f && stepSign != lastStepSign) OnStep?.Invoke();
            lastStepSign = stepSign;

            // saldırı pozu
            Pose atk = default;
            float atkWeight = 0f;
            if (style != AttackStyle.None)
            {
                var keys = KeysOf(style);
                atkT += dt * SpeedMul;
                float dur = keys[keys.Length - 1].t;
                if (atkT >= dur) style = AttackStyle.None;
                else
                {
                    atk = Sample(keys, atkT);
                    atkWeight = 1f;
                }
            }

            // kişilik hareketi: sadece dururken ve saldırmıyorken
            if (atkWeight <= 0f)
            {
                if (fidKeys != null)
                {
                    if (Moving && fidKeys != AlertKeys && fidKeys != CheerKeys) fidKeys = null;
                    else
                    {
                        fidT += dt;
                        float fdur = fidKeys[fidKeys.Length - 1].t;
                        if (fidT >= fdur) fidKeys = null;
                        else { atk = Sample(fidKeys, fidT); atkWeight = 1f; }
                    }
                }
                else if (Fidget != FidgetKind.None && Application.isPlaying && !Moving && moveBlend < 0.05f)
                {
                    if (fidgetTimer < 0f) fidgetTimer = UnityEngine.Random.Range(2.5f, 9f);
                    fidgetTimer -= dt;
                    if (fidgetTimer <= 0f) { fidKeys = FidgetKeys(Fidget); fidT = 0f; fidgetTimer = UnityEngine.Random.Range(5f, 12f); }
                }
            }
            else fidKeys = null;

            // bekleme (nefes, ağırlık aktarımı, etrafa bakınma)
            float idle = 1f - mb;
            float breath = Mathf.Sin(idleT * 2.1f);
            float sway = Mathf.Sin(idleT * 0.7f);
            float look = (Mathf.PerlinNoise(idleT * 0.22f, 3.7f) - 0.5f) * 70f * idle * (1f - atkWeight);

            // darbe alma: yaylı geri savrulma
            hurtT += dt;
            float recoil = hurtT < 0.6f ? Mathf.Exp(-hurtT * 9f) * Mathf.Cos(hurtT * 26f) : 0f;
            float squash = hurtT < 0.18f ? Mathf.Sin(hurtT / 0.18f * Mathf.PI) * 0.12f : 0f;

            // gövde
            float bob = Mathf.Abs(s) * Bob * mb + breath * 0.008f * idle;
            float lean = Lean * mb + atk.body.x - recoil * 16f;
            float roll = s * Roll * mb + sway * 1.2f * idle + atk.body.z;
            float yaw = atk.body.y + Mathf.Sin(walkPhase) * 4f * mb;
            body.localPosition = bodyBase + new Vector3(0, bob + atk.bodyOff.y, atk.bodyOff.z - recoil * 0.06f);
            body.localRotation = Quaternion.Euler(lean, yaw, roll);
            body.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash);

            // bacaklar
            float stride = s * LegSwing * mb;
            if (atkWeight > 0f && (style == AttackStyle.Thrust || style == AttackStyle.LeapSlam || style == AttackStyle.HeavySmash))
                stride += Mathf.Clamp(atk.bodyOff.z * 120f, -25f, 25f); // hamle: bir ayak öne
            if (legL) legL.localRotation = legLRest * Quaternion.Euler(-stride - Mathf.Max(0f, -atk.bodyOff.y) * 200f, 0, 0);
            if (legR) legR.localRotation = legRRest * Quaternion.Euler(stride + Mathf.Max(0f, -atk.bodyOff.y) * 150f, 0, 0);

            // boş kol: yürürken sallanır, saldırırken dengeye geçer
            if (armL)
            {
                float armSwing = s * ArmSwing * mb + breath * 2f * idle;
                float balance = atkWeight * Mathf.Clamp(atk.body.y * 0.4f, -30f, 30f);
                armL.localRotation = armLRest * Quaternion.Euler(armSwing - atkWeight * 20f, balance, -atkWeight * 18f - recoil * 20f);
            }

            // silah kolu
            if (weapon)
            {
                var idleArm = new Vector3(-s * ArmSwing * 0.6f * mb + breath * 1.5f * idle, 0, 0);
                var armRot = Vector3.Lerp(idleArm, atk.arm, atkWeight);
                weapon.localRotation = weaponRest * Quaternion.Euler(armRot.x - recoil * 25f, armRot.y, armRot.z);
                weapon.localPosition = weaponBasePos + atk.armOff;
            }

            // kafa: gövdenin sallantısını dengeler, etrafa bakar
            if (head)
                head.localRotation = headRest * Quaternion.Euler(-lean * 0.45f + breath * 1.5f * idle + recoil * 12f, look - yaw * 0.25f, -roll * 0.7f);
        }

        void UpdateDeath(float dt)
        {
            deadT += dt;
            // geriye düş ve sek
            float k = Mathf.Clamp01(deadT / 0.45f);
            float fall = BounceOut(k);
            body.localRotation = Quaternion.Euler(-95f * fall, 0, 8f * fall);
            body.localPosition = bodyBase + new Vector3(0, -0.04f * fall, -0.12f * fall);
            if (weapon) weapon.localRotation = weaponRest * Quaternion.Euler(-120f * fall, 0, 30f * fall);
            if (armL) armL.localRotation = armLRest * Quaternion.Euler(-100f * fall, 0, -40f * fall);
            if (legL) legL.localRotation = legLRest * Quaternion.Euler(-25f * fall, 0, 0);
            if (legR) legR.localRotation = legRRest * Quaternion.Euler(10f * fall, 0, 0);
            if (deadT > 1.0f)
            {
                float sink = (deadT - 1.0f) / 0.7f;
                transform.localScale = Vector3.one * Mathf.Clamp01(1f - sink);
                if (sink >= 1f) gameObject.SetActive(false);
            }
        }

        static float BounceOut(float k)
        {
            if (k < 0.72f) { float a = k / 0.72f; return a * a; }
            float b = (k - 0.72f) / 0.28f;
            return 1f - Mathf.Sin(b * Mathf.PI) * 0.08f;
        }
    }
}

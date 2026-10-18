using System;
using System.Collections.Generic;
using BlindAttack.Core;
using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>Simülasyon sonucunu sahnede oynatır (izleme ekranı).</summary>
    public class Playback : MonoBehaviour
    {
        public float Speed = 1f;
        public bool Running { get; private set; }
        public float HeroHpFraction { get; private set; } = 1f;
        public string LastInfo { get; private set; } = "";
        /// <summary>Kahramanın güncel güçleri (arayüz için, zengin metin).</summary>
        public string StatsText => heroRig == null ? "" :
            $"<color=#C0501E><b>Saldırı {heroRig.Damage:0}</b></color>    <color=#3F6FB5><b>Zırh %{heroRig.Armor * 100f:0}</b></color>    <color=#B08A10><b>Hız x{heroRig.Speed:0.0#}</b></color>";
        public event Action Finished;

        SimResult res;
        MapView view;
        IsoCamera cam;
        float t, endDelay;
        int eventIdx;
        GameObject hero;
        UnitAnim heroAnim;
        HeroRig heroRig;
        HpBar heroBar;
        readonly List<GameObject> enemies = new List<GameObject>();
        readonly List<UnitAnim> enemyAnims = new List<UnitAnim>();
        readonly List<HpBar> enemyBars = new List<HpBar>();
        readonly List<GameObject> supplies = new List<GameObject>();
        readonly List<GameObject> traps = new List<GameObject>();
        Vector3 lastHeroPos;
        int heroFaceEnemy = -1; float heroFaceTimer;

        float hitstopTimer;
        float stepTimer;

        public void Play(SimResult result, MapView mapView, IsoCamera isoCam, bool viewerIsDefender)
        {
            res = result; view = mapView; cam = isoCam;
            t = 0f; endDelay = 0f; eventIdx = 0; Speed = 1f; hitstopTimer = 0f; stepTimer = 0f;
            enemies.Clear(); enemyAnims.Clear(); enemyBars.Clear(); supplies.Clear(); traps.Clear();
            var root = view.UnitsRoot;

            foreach (var s in res.Supplies)
            {
                if (s.Type == ItemType.Hero) { supplies.Add(new GameObject("Başlangıç")); supplies[supplies.Count - 1].transform.SetParent(root, false); continue; }
                var go = Visuals.Create(s.Type);
                go.transform.SetParent(root, false);
                go.transform.localPosition = MapView.CellToWorld(s.Cell);
                supplies.Add(go);
            }
            foreach (var tr in res.Traps)
            {
                var go = Visuals.Create(ItemType.SpikeTrap);
                go.transform.SetParent(root, false);
                go.transform.localPosition = MapView.CellToWorld(tr.Cell);
                // tuzaklar gizli: savunan kendi tuzaklarını soluk görür, saldıran hiç görmez
                go.transform.localScale = viewerIsDefender ? new Vector3(1f, 0.25f, 1f) : Vector3.zero;
                traps.Add(go);
            }
            foreach (var e in res.Enemies)
            {
                var go = Visuals.Create(e.Type);
                go.transform.SetParent(root, false);
                go.transform.localPosition = MapView.CellToWorld(e.Home);
                go.transform.localRotation = Quaternion.Euler(0, 225f, 0); // kameraya dönük
                enemies.Add(go);
                enemyAnims.Add(go.GetComponent<UnitAnim>());
                enemyBars.Add(HpBar.Create(go.transform, e.Type == ItemType.Ogre ? 1.75f : 1.25f, new Color(0.9f, 0.25f, 0.2f), e.Type == ItemType.Ogre ? 0.9f : 0.6f));
            }

            hero = Visuals.Hero();
            hero.transform.SetParent(root, false);
            hero.transform.localPosition = MapView.ToWorld(res.Frames[0].HeroPos);
            hero.transform.localRotation = Quaternion.Euler(0, 45f, 0);
            heroAnim = hero.GetComponent<UnitAnim>();
            heroRig = hero.GetComponent<HeroRig>();
            heroBar = HpBar.Create(hero.transform, 1.5f, new Color(0.3f, 0.85f, 0.35f), 0.8f);
            heroBar.AlwaysShow = true;
            lastHeroPos = hero.transform.position;
            HeroHpFraction = 1f;
            LastInfo = "";
            cam.Follow(hero.transform, 0.7f, 0.72f);
            Running = true;
        }

        public void Stop()
        {
            Running = false;
            if (cam) cam.Follow(null, 0f);
        }

        public void SkipToEnd()
        {
            if (!Running) return;
            Speed = 50f;
        }

        void Update()
        {
            if (!Running || res == null) return;
            if (hitstopTimer > 0f)
            {
                hitstopTimer -= Time.unscaledDeltaTime;
                return;
            }

            float dt = Time.deltaTime * Speed;
            t = Mathf.Min(t + dt, res.Duration);

            // ---- olaylar ----
            while (eventIdx < res.Events.Count && res.Events[eventIdx].Time <= t)
            {
                Handle(res.Events[eventIdx]);
                eventIdx++;
            }

            // ---- konum enterpolasyonu ----
            float fi = t / res.FrameInterval;
            int i0 = Mathf.Clamp(Mathf.FloorToInt(fi), 0, res.Frames.Count - 1);
            int i1 = Mathf.Min(i0 + 1, res.Frames.Count - 1);
            float k = Mathf.Clamp01(fi - i0);
            var f0 = res.Frames[i0]; var f1 = res.Frames[i1];

            var hp = Lerp(f0.HeroPos, f1.HeroPos, k);
            hero.transform.localPosition = hp;
            var delta = hero.transform.position - lastHeroPos;
            bool moving = delta.magnitude > 0.0005f;
            if (heroAnim) heroAnim.Moving = moving;
            if (moving)
            {
                stepTimer -= dt;
                if (stepTimer <= 0f)
                {
                    stepTimer = 0.28f / Mathf.Max(0.5f, heroRig != null ? heroRig.Speed : 1f);
                    AudioManager.Play(SoundId.Step, 0.32f);
                }
            }

            heroFaceTimer -= dt;
            if (heroFaceTimer > 0f && heroFaceEnemy >= 0 && heroFaceEnemy < enemies.Count)
                Face(hero.transform, enemies[heroFaceEnemy].transform.position - hero.transform.position, 20f);
            else if (moving) Face(hero.transform, delta, 12f);
            lastHeroPos = hero.transform.position;
            HeroHpFraction = Mathf.Lerp(f0.HeroHp, f1.HeroHp, k) / res.HeroMaxHp;
            heroBar.Set(HeroHpFraction);

            for (int i = 0; i < enemies.Count; i++)
            {
                var go = enemies[i];
                if (!go.activeSelf) continue;
                var prev = go.transform.localPosition;
                var p = Lerp(f0.EnemyPos[i], f1.EnemyPos[i], k);
                go.transform.localPosition = p;
                var d = p - prev;
                bool m = d.magnitude > 0.0005f;
                if (enemyAnims[i] && !enemyAnims[i].IsDead)
                {
                    enemyAnims[i].Moving = m;
                    if (m) Face(go.transform, d, 10f);
                    else if (Vector3.Distance(go.transform.position, hero.transform.position) < 5f) Face(go.transform, hero.transform.position - go.transform.position, 6f);
                }
                enemyBars[i].Set(Mathf.Lerp(f0.EnemyHp[i], f1.EnemyHp[i], k) / res.Enemies[i].MaxHp);
            }

            if (t >= res.Duration)
            {
                endDelay += Time.deltaTime;
                if (endDelay > 1.8f)
                {
                    Running = false;
                    cam.Follow(null, 0f);
                    Finished?.Invoke();
                }
            }
        }

        void Handle(SimEvent e)
        {
            var root = view.UnitsRoot;
            var pos = MapView.ToWorld(e.Pos);
            switch (e.Type)
            {
                case SimEventType.HeroAttack:
                    heroAnim?.Attack();
                    heroFaceEnemy = e.Index; heroFaceTimer = 0.5f;
                    if (e.Index < enemyAnims.Count && enemyAnims[e.Index]) enemyAnims[e.Index].Hurt();
                    Effects.FloatText(root, pos + Vector3.up * 1.3f, Mathf.RoundToInt(e.Amount).ToString(), new Color(1f, 0.95f, 0.6f));
                    Effects.Burst(root, pos + Vector3.up * 0.5f, new Color(1f, 0.9f, 0.5f), 5, 2.8f, 0.07f);
                    AudioManager.Play(SoundId.SwordSlash, 0.85f);
                    AudioManager.Play(SoundId.Impact, 0.75f);
                    hitstopTimer = 0.045f;
                    cam.Shake(0.22f);
                    break;
                case SimEventType.EnemyAttack:
                    if (e.Index < enemyAnims.Count && enemyAnims[e.Index]) enemyAnims[e.Index].Attack();
                    if (heroRig) heroRig.Blocked();
                    if (res.Enemies[e.Index].Type == ItemType.Archer)
                    {
                        Effects.Arrow(root, pos + Vector3.up * 0.7f, hero.transform.position + Vector3.up * 0.6f);
                        AudioManager.Play(SoundId.ArrowShoot, 0.85f);
                    }
                    else if (heroRig && heroRig.Armor > 0.01f)
                    {
                        AudioManager.Play(SoundId.ShieldBlock, 0.85f);
                    }
                    else
                    {
                        AudioManager.Play(SoundId.Impact, 0.85f);
                    }
                    heroAnim?.Hurt();
                    Effects.FloatText(root, hero.transform.position + Vector3.up * 1.6f, "-" + Mathf.RoundToInt(e.Amount), new Color(1f, 0.35f, 0.3f));
                    hitstopTimer = 0.04f;
                    cam.Shake(0.32f);
                    break;
                case SimEventType.EnemyDied:
                    if (e.Index < enemyAnims.Count && enemyAnims[e.Index]) enemyAnims[e.Index].Die();
                    if (e.Index < enemyBars.Count) enemyBars[e.Index].gameObject.SetActive(false);
                    Effects.Burst(root, pos + Vector3.up * 0.4f, Visuals.IconColor(res.Enemies[e.Index].Type), 12, 3.8f);
                    AudioManager.Play(SoundId.EnemyDie, 0.9f);
                    cam.Shake(0.25f);
                    break;
                case SimEventType.Pickup:
                {
                    var s = res.Supplies[e.Index];
                    var col = Visuals.IconColor(s.Type);
                    if (s.Type == ItemType.Flag)
                    {
                        supplies[e.Index].transform.localRotation = Quaternion.Euler(0, 0, 70);
                        Effects.Ring(root, pos, new Color(col.r, col.g, col.b, 0.6f), 0.9f);
                        LastInfo = "Bayrağa ulaştı";
                        AudioManager.Play(SoundId.UiCopy, 0.85f);
                        break;
                    }
                    supplies[e.Index].SetActive(false);
                    Effects.Burst(root, pos + Vector3.up * 0.4f, col, 14, 3f, 0.09f);
                    Effects.Ring(root, pos, new Color(col.r, col.g, col.b, 0.7f), 1.3f);
                    if (heroRig) { heroRig.SetStats(e.Dmg, e.Armor, e.Speed); heroRig.Pulse(col); }
                    string txt;
                    switch (s.Type)
                    {
                        case ItemType.SwordCrate:
                            txt = "+6 SALDIRI"; LastInfo = "Kılıç alev aldı!";
                            AudioManager.Play(SoundId.PlaceCrate, 0.9f);
                            break;
                        case ItemType.ArmorCrate:
                            txt = $"ZIRH  %{e.Armor * 100f:0}"; LastInfo = "Zırh giydi!";
                            AudioManager.Play(SoundId.PlaceCrate, 0.9f);
                            break;
                        case ItemType.Potion:
                            txt = "+45 CAN"; LastInfo = "İksir içti";
                            Effects.Burst(root, hero.transform.position + Vector3.up * 0.6f, new Color(0.4f, 1f, 0.5f), 16, 2f, 0.07f);
                            AudioManager.Play(SoundId.PotionDrink, 0.95f);
                            break;
                        case ItemType.SpeedBoots:
                            txt = $"HIZ x{e.Speed:0.0#}"; LastInfo = "Hız çizmesi giydi!";
                            AudioManager.Play(SoundId.PlaceCrate, 0.9f);
                            break;
                        case ItemType.Bomb:
                            txt = "BUM!"; LastInfo = "Bomba patladı!";
                            break;
                        default:
                            txt = Items.Get(s.Type).Name;
                            AudioManager.Play(SoundId.PlaceCrate, 0.9f);
                            break;
                    }
                    Effects.FloatText(root, hero.transform.position + Vector3.up * 1.9f, txt, col, 1.1f);
                    break;
                }
                case SimEventType.Bomb:
                    Effects.Burst(root, pos + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.15f), 26, 6f, 0.14f);
                    Effects.Burst(root, pos + Vector3.up * 0.3f, new Color(0.3f, 0.3f, 0.3f), 14, 3f, 0.18f);
                    Effects.Ring(root, pos, new Color(1f, 0.6f, 0.2f, 0.8f), 2.5f);
                    AudioManager.Play(SoundId.BombExplode, 1.0f);
                    hitstopTimer = 0.07f;
                    cam.Shake(1.1f);
                    break;
                case SimEventType.TrapTriggered:
                    traps[e.Index].transform.localScale = Vector3.one;
                    heroAnim?.Hurt();
                    Effects.Burst(root, pos + Vector3.up * 0.2f, new Color(0.8f, 0.8f, 0.85f), 8, 3f, 0.07f);
                    Effects.FloatText(root, pos + Vector3.up * 1.6f, "TUZAK! -" + Mathf.RoundToInt(e.Amount), new Color(1f, 0.3f, 0.25f));
                    AudioManager.Play(SoundId.TrapSnap, 0.95f);
                    hitstopTimer = 0.05f;
                    cam.Shake(0.45f);
                    LastInfo = "Gizli tuzağa bastı!";
                    break;
                case SimEventType.HeroDied:
                    heroAnim?.Die();
                    Effects.Burst(root, pos + Vector3.up * 0.5f, new Color(0.3f, 0.5f, 0.95f), 18, 4f);
                    AudioManager.Play(SoundId.HeroDie, 0.95f);
                    LastInfo = "Kahraman düştü!";
                    cam.Shake(0.6f);
                    break;
                case SimEventType.ReachedExit:
                    Effects.Burst(root, pos + Vector3.up * 0.8f, new Color(0.6f, 0.95f, 1f), 30, 5f, 0.1f);
                    Effects.Ring(root, pos, new Color(0.6f, 0.95f, 1f, 0.8f), 2f);
                    AudioManager.Play(SoundId.VictoryFanfare, 1.0f);
                    hero.SetActive(false);
                    LastInfo = "Çıkışa ulaştı!";
                    break;
                case SimEventType.Timeout:
                    LastInfo = "Süre doldu";
                    break;
            }
        }

        static Vector3 Lerp(Vec2 a, Vec2 b, float k) => new Vector3(Mathf.Lerp(a.X, b.X, k), 0f, Mathf.Lerp(a.Y, b.Y, k));

        static void Face(Transform tr, Vector3 dir, float speed)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) return;
            tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir), Time.deltaTime * speed);
        }
    }
}

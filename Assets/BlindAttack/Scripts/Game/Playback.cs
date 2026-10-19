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
        /// <summary>Ana menü arka planı: ses, sarsıntı, kamera takibi ve donma yok.</summary>
        public bool Quiet;
        /// <summary>Açılış sinematiği sürüyor mu?</summary>
        public bool InIntro => introActive;

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
        // saldırı animasyonları olaydan önce başlatılır ki darbe anı hasarla aynı kareye denk gelsin
        bool[] animStarted;
        readonly Dictionary<int, AttackStyle> heroStyles = new Dictionary<int, AttackStyle>();
        const float MaxLead = 0.6f;

        // açılış sinematiği
        public const float IntroDuration = 3.4f;
        bool introActive;
        float introT;
        readonly List<Riser> risers = new List<Riser>();
        // ağır çekim (ölüm/zafer anı)
        float slowmoTimer;
        bool graded;
        // kişilik: fark etme ve konuşma
        bool[] alerted;
        float sayCooldown, lastAlertSound;
        bool saidAggro, saidLowHp, saidExit, saidOgre, saidArcher;
        bool heroExiting;

        static readonly string[] OpenLines = { "Hadi bakalım!", "Çıkış beni bekler!", "Bu iş kolay!", "İleri!" };
        static readonly string[] AggroLines = { "Pusu!", "Gelin bakalım!", "Neredeydiniz siz?!", "Saklanmışlar!" };
        static readonly string[] LowHpLines = { "Zor durumdayım...", "Dayan, dayan!", "Biraz şifa lazım!" };
        static readonly string[] TrapLines = { "Ah! Tuzak!", "Ayağım!", "Bunu kim koydu?!" };
        static readonly string[] OgreLines = { "Bu ne büyüklük böyle!", "Dev mi o?!" };
        static readonly string[] ArcherLines = { "Ok yağıyor!", "Okçu var!" };
        static readonly string[] ExitLines = { "Çıkış göründü!", "Az kaldı!" };

        static string Pick(string[] a) => a[UnityEngine.Random.Range(0, a.Length)];

        void Sfx(SoundId id, float vol = 1f, float pv = 0.06f) { if (!Quiet) AudioManager.Play(id, vol, pv); }
        void Shake(float a) { if (!Quiet && cam) cam.Shake(a); }

        void Say(string text, bool force = false)
        {
            if (hero == null || !hero.activeInHierarchy) return;
            if (!force && sayCooldown > 0f) return;
            sayCooldown = 2.4f;
            Juice.Say(hero.transform, text, 2.0f, 2.1f);
        }

        void AlertEnemy(int i)
        {
            if (alerted == null || i < 0 || i >= alerted.Length || alerted[i]) return;
            alerted[i] = true;
            if (i < enemies.Count && enemies[i] && enemies[i].activeInHierarchy)
            {
                var type = res.Enemies[i].Type;
                float h = type == ItemType.Ogre ? 2.1f : type == ItemType.Archer ? 1.95f : 1.45f;
                Juice.AlertMark(enemies[i].transform, h, new Color(1f, 0.82f, 0.2f));
                if (enemyAnims[i]) enemyAnims[i].Alert();
                if (Time.unscaledTime - lastAlertSound > 0.25f) { lastAlertSound = Time.unscaledTime; Sfx(SoundId.AlertBlip, 0.6f, 0.1f); }
            }
            if (!saidAggro) { saidAggro = true; Say(Pick(AggroLines)); }
        }

        public void Play(SimResult result, MapView mapView, IsoCamera isoCam, bool viewerIsDefender, bool intro = false)
        {
            res = result; view = mapView; cam = isoCam;
            t = 0f; endDelay = 0f; eventIdx = 0; Speed = 1f; hitstopTimer = 0f; stepTimer = 0f;
            animStarted = new bool[res.Events.Count];
            alerted = new bool[res.Enemies.Count];
            saidAggro = saidLowHp = saidExit = saidOgre = saidArcher = false;
            sayCooldown = 0f; slowmoTimer = 0f; heroExiting = false;
            risers.Clear();
            if (graded) { GameApp.SetGrade(false); graded = false; }
            heroStyles.Clear();
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
                var ea = go.GetComponent<UnitAnim>();
                enemyAnims.Add(ea);
                if (e.Type == ItemType.Ogre && ea != null)
                {
                    // devin ağır adımları: toz ve hafif sarsıntı
                    var ogre = go.transform;
                    ea.OnStep = () =>
                    {
                        if (!ogre.gameObject.activeInHierarchy) return;
                        Effects.Burst(view.UnitsRoot, ogre.position + Vector3.up * 0.05f, new Color(0.62f, 0.52f, 0.38f), 3, 1.2f, 0.06f);
                        if (hero && Vector3.Distance(ogre.position, hero.transform.position) < 4f) Shake(0.05f);
                    };
                }
                enemyBars.Add(HpBar.Create(go.transform, e.Type == ItemType.Ogre ? 1.75f : e.Type == ItemType.Archer ? 1.6f : 1.1f, new Color(0.9f, 0.25f, 0.2f), e.Type == ItemType.Ogre ? 0.9f : 0.6f));
            }

            hero = Visuals.Hero();
            hero.transform.SetParent(root, false);
            hero.transform.localPosition = MapView.ToWorld(res.Frames[0].HeroPos);
            hero.transform.localRotation = Quaternion.Euler(0, 45f, 0);
            heroAnim = hero.GetComponent<UnitAnim>();
            heroRig = hero.GetComponent<HeroRig>();
            if (heroAnim) heroAnim.OnStep = () => Sfx(SoundId.Step, 0.3f);
            heroBar = HpBar.Create(hero.transform, 1.5f, new Color(0.3f, 0.85f, 0.35f), 0.8f);
            heroBar.AlwaysShow = true;
            lastHeroPos = hero.transform.position;
            HeroHpFraction = 1f;
            LastInfo = "";
            Running = true;
            introActive = false;
            if (intro && !Quiet)
            {
                // savunma yerden fışkırır: çıkıştan kahramana doğru süzülen kameranın önünde sırayla
                var exitW = view.ExitPortal ? view.ExitPortal.position : MapView.CellToWorld(view.Map.Exit);
                var order = new List<int>();
                for (int i = 0; i < enemies.Count; i++) order.Add(i);
                order.Sort((a, b) => Vector3.Distance(enemies[a].transform.position, exitW).CompareTo(Vector3.Distance(enemies[b].transform.position, exitW)));
                float span = IntroDuration - 1.2f;
                for (int k = 0; k < order.Count; k++)
                {
                    var go = enemies[order[k]];
                    Juice.Rise(go, 0.45f + span * k / Mathf.Max(1, order.Count), root, k % 2 == 0);
                    risers.Add(go.GetComponent<Riser>());
                }
                cam.BeginIntro(exitW, hero.transform.position);
                introActive = true; introT = 0f;
                Sfx(SoundId.CinematicSwell, 0.75f, 0f);
            }
            else if (!Quiet) StartFollow();
        }

        void StartFollow()
        {
            if (cam) cam.Follow(hero.transform, 0.7f, 0.72f);
        }

        void EndIntro()
        {
            if (!introActive) return;
            introActive = false;
            foreach (var r in risers) if (r) r.Finish();
            risers.Clear();
            if (cam) cam.EndIntro();
            StartFollow();
            Say(Pick(OpenLines), true);
        }

        public void Stop()
        {
            Running = false;
            if (introActive) { introActive = false; if (cam) cam.EndIntro(); }
            if (graded) { GameApp.SetGrade(false); graded = false; }
            if (cam) cam.Follow(null, 0f);
        }

        public void SkipToEnd()
        {
            if (!Running) return;
            if (introActive) EndIntro();
            Speed = 50f;
        }

        void Update()
        {
            if (!Running || res == null) return;
            if (Quiet) hitstopTimer = 0f;
            sayCooldown -= Time.deltaTime;

            // ---- açılış sinematiği: zaman akmaz, kamera süzülür, düşmanlar yerden çıkar ----
            if (introActive)
            {
                if (Speed > 10f) { EndIntro(); }
                else
                {
                    introT += Time.deltaTime * Mathf.Clamp(Speed, 1f, 2.5f);
                    if (cam) cam.SetIntroProgress(introT / IntroDuration);
                    SetAnimTimeScale(1f);
                    if (heroAnim) heroAnim.Moving = false;
                    if (introT >= IntroDuration) EndIntro();
                    return;
                }
            }

            if (hitstopTimer > 0f)
            {
                // vuruş anı donması: animasyonlar da durur
                hitstopTimer -= Time.unscaledDeltaTime;
                SetAnimTimeScale(0f);
                return;
            }
            float slow = 1f;
            if (slowmoTimer > 0f) { slowmoTimer -= Time.unscaledDeltaTime; slow = 0.3f; }
            SetAnimTimeScale(Speed * slow);

            float dt = Time.deltaTime * Speed * slow;
            t = Mathf.Min(t + dt, res.Duration);

            // ---- saldırı animasyonlarını önden başlat ----
            for (int i = eventIdx; i < res.Events.Count && res.Events[i].Time <= t + MaxLead; i++)
            {
                if (animStarted[i]) continue;
                var ev = res.Events[i];
                float lead = AttackLead(ev);
                if (lead < 0f) { animStarted[i] = true; continue; }
                if (ev.Time - lead <= t) StartAttackAnim(i, ev, Mathf.Max(0f, ev.Time - t));
            }

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

            if (!heroExiting)
            {
                var hp = Lerp(f0.HeroPos, f1.HeroPos, k);
                hero.transform.localPosition = hp;
                var delta = hero.transform.position - lastHeroPos;
                bool moving = delta.magnitude > 0.0005f;
                if (heroAnim) heroAnim.Moving = moving;

                heroFaceTimer -= dt;
                if (heroFaceTimer > 0f && heroFaceEnemy >= 0 && heroFaceEnemy < enemies.Count)
                    Face(hero.transform, enemies[heroFaceEnemy].transform.position - hero.transform.position, 20f);
                else if (moving) Face(hero.transform, delta, 12f);
                lastHeroPos = hero.transform.position;
            }
            HeroHpFraction = Mathf.Lerp(f0.HeroHp, f1.HeroHp, k) / res.HeroMaxHp;
            heroBar.Set(HeroHpFraction);
            if (!saidLowHp && HeroHpFraction < 0.3f && HeroHpFraction > 0f) { saidLowHp = true; Say(Pick(LowHpLines), true); }
            if (!saidExit && hero.activeInHierarchy && HeroHpFraction > 0f && view.Map != null &&
                Vector3.Distance(hero.transform.localPosition, MapView.CellToWorld(view.Map.Exit)) < 3.2f) { saidExit = true; Say(Pick(ExitLines), true); }

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
                    if (m && !alerted[i]) AlertEnemy(i);
                    enemyAnims[i].Moving = m;
                    if (m) Face(go.transform, d, 10f);
                    else if (Vector3.Distance(go.transform.position, hero.transform.position) < 5f) Face(go.transform, hero.transform.position - go.transform.position, 6f);
                }
                enemyBars[i].Set(Mathf.Lerp(f0.EnemyHp[i], f1.EnemyHp[i], k) / res.Enemies[i].MaxHp);
            }

            if (t >= res.Duration)
            {
                endDelay += Time.deltaTime;
                if (endDelay > (res.HeroDied ? 2.4f : 1.8f))
                {
                    Running = false;
                    if (cam) cam.Follow(null, 0f);
                    if (graded) { GameApp.SetGrade(false); graded = false; }
                    Finished?.Invoke();
                }
            }
        }

        void SetAnimTimeScale(float scale)
        {
            if (heroAnim) heroAnim.TimeScale = scale;
            foreach (var a in enemyAnims) if (a) a.TimeScale = scale;
        }

        /// <summary>Saldırı olayından ne kadar önce animasyonun başlaması gerektiği (-1 = saldırı değil).</summary>
        float AttackLead(SimEvent e)
        {
            if (e.Type == SimEventType.HeroAttack && heroRig != null)
                return Mathf.Min(0.45f, UnitAnim.ImpactTime(heroRig.PeekAttack()) / Mathf.Max(0.3f, heroRig.Speed));
            if (e.Type == SimEventType.EnemyAttack && e.Index < enemyAnims.Count && enemyAnims[e.Index] != null)
                return Mathf.Min(MaxLead, UnitAnim.ImpactTime(enemyAnims[e.Index].PeekStyle()));
            return -1f;
        }

        void StartAttackAnim(int idx, SimEvent e, float timeToImpact)
        {
            animStarted[idx] = true;
            if (e.Type == SimEventType.HeroAttack && heroRig != null)
            {
                heroStyles[idx] = heroRig.Attack(timeToImpact);
                heroFaceEnemy = e.Index; heroFaceTimer = timeToImpact + 0.5f;
            }
            else if (e.Type == SimEventType.EnemyAttack && e.Index < enemyAnims.Count && enemyAnims[e.Index] && !enemyAnims[e.Index].IsDead)
            {
                AlertEnemy(e.Index);
                enemyAnims[e.Index].Attack(timeToImpact);
            }
        }

        void Handle(SimEvent e)
        {
            var root = view.UnitsRoot;
            var pos = MapView.ToWorld(e.Pos);
            switch (e.Type)
            {
                case SimEventType.HeroAttack:
                {
                    int evIdx = eventIdx;
                    if (!heroStyles.TryGetValue(evIdx, out var style) && heroRig != null)
                        style = heroRig.Attack(0f); // önden başlatılamadıysa (ör. "Atla") hemen vur
                    heroFaceEnemy = e.Index; heroFaceTimer = 0.4f;
                    if (e.Index < enemyAnims.Count && enemyAnims[e.Index]) enemyAnims[e.Index].Hurt();
                    int tier = heroRig != null ? heroRig.SwordTier : 0;
                    var spark = tier <= 0 ? new Color(1f, 0.92f, 0.6f) : tier == 1 ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.85f, 0.45f);
                    float big = 1f + tier * 0.15f;
                    Effects.FloatText(root, pos + Vector3.up * 1.3f, Mathf.RoundToInt(e.Amount).ToString(), spark, big);
                    Effects.Burst(root, pos + Vector3.up * 0.5f, spark, 6 + tier * 4, 2.8f + tier * 0.6f, 0.07f);
                    Sfx(SoundId.SwordSlash, 0.85f);
                    Sfx(SoundId.Impact, 0.75f);
                    switch (style)
                    {
                        case AttackStyle.Spin:
                            Effects.Ring(root, hero.transform.position, new Color(spark.r, spark.g, spark.b, 0.6f), 1.4f);
                            hitstopTimer = 0.06f; Shake(0.3f);
                            break;
                        case AttackStyle.LeapSlam:
                            Effects.Ring(root, pos, new Color(1f, 0.9f, 0.6f, 0.8f), 2.2f);
                            Effects.Burst(root, pos + Vector3.up * 0.1f, new Color(0.6f, 0.5f, 0.38f), 14, 3.5f, 0.1f);
                            Sfx(SoundId.BombExplode, 0.45f);
                            hitstopTimer = 0.09f; Shake(0.6f);
                            break;
                        case AttackStyle.Chop:
                            hitstopTimer = 0.055f; Shake(0.26f);
                            break;
                        default:
                            hitstopTimer = 0.045f; Shake(0.2f);
                            break;
                    }
                    break;
                }
                case SimEventType.HeroCleave:
                {
                    // savuruş yandaki düşmana da değdi: ayrı animasyon yok, sadece darbe
                    if (e.Index < enemyAnims.Count && enemyAnims[e.Index]) enemyAnims[e.Index].Hurt();
                    var c = new Color(1f, 0.8f, 0.5f);
                    Effects.FloatText(root, pos + Vector3.up * 1.2f, Mathf.RoundToInt(e.Amount).ToString(), c, 0.8f);
                    Effects.Burst(root, pos + Vector3.up * 0.5f, c, 4, 2.2f, 0.06f);
                    break;
                }
                case SimEventType.EnemyAttack:
                    AlertEnemy(e.Index);
                    if (!animStarted[eventIdx] && e.Index < enemyAnims.Count && enemyAnims[e.Index]) enemyAnims[e.Index].Attack(0f);
                    if (!saidOgre && res.Enemies[e.Index].Type == ItemType.Ogre) { saidOgre = true; Say(Pick(OgreLines), true); }
                    else if (!saidArcher && res.Enemies[e.Index].Type == ItemType.Archer) { saidArcher = true; Say(Pick(ArcherLines)); }
                    if (res.Enemies[e.Index].Type == ItemType.Ogre)
                    {
                        // dev yere vurur: toz halkası ve güçlü sarsıntı
                        Effects.Ring(root, hero.transform.position, new Color(0.7f, 0.6f, 0.45f, 0.7f), 1.6f);
                        Effects.Burst(root, hero.transform.position + Vector3.up * 0.1f, new Color(0.62f, 0.52f, 0.38f), 10, 2.5f, 0.08f);
                        Shake(0.5f);
                    }
                    if (heroRig) heroRig.Blocked();
                    if (res.Enemies[e.Index].Type == ItemType.Archer)
                    {
                        Effects.Arrow(root, pos + Vector3.up * 0.7f, hero.transform.position + Vector3.up * 0.6f);
                        Sfx(SoundId.ArrowShoot, 0.85f);
                    }
                    else if (heroRig && heroRig.Armor > 0.01f)
                    {
                        Sfx(SoundId.ShieldBlock, 0.85f);
                    }
                    else
                    {
                        Sfx(SoundId.Impact, 0.85f);
                    }
                    heroAnim?.Hurt();
                    Effects.FloatText(root, hero.transform.position + Vector3.up * 1.6f, "-" + Mathf.RoundToInt(e.Amount), new Color(1f, 0.35f, 0.3f));
                    hitstopTimer = 0.04f;
                    Shake(0.32f);
                    break;
                case SimEventType.EnemyDied:
                    if (e.Index < enemyAnims.Count && enemyAnims[e.Index]) enemyAnims[e.Index].Die();
                    if (e.Index < enemyBars.Count) enemyBars[e.Index].gameObject.SetActive(false);
                    Effects.Burst(root, pos + Vector3.up * 0.4f, Visuals.IconColor(res.Enemies[e.Index].Type), 12, 3.8f);
                    Sfx(SoundId.EnemyDie, 0.9f);
                    Shake(0.25f);
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
                        Sfx(SoundId.UiCopy, 0.85f);
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
                            txt = "+" + (int)AttackSimulation.SwordBonus + " SALDIRI"; LastInfo = "Kılıç alev aldı!"; Say("Bu kılıç yanıyor!", true);
                            Sfx(SoundId.PlaceCrate, 0.9f);
                            break;
                        case ItemType.ArmorCrate:
                            txt = $"ZIRH  %{e.Armor * 100f:0}"; LastInfo = "Zırh giydi!"; Say("Artık daha sağlamım!", true);
                            Sfx(SoundId.PlaceCrate, 0.9f);
                            break;
                        case ItemType.Potion:
                            txt = "+" + (int)AttackSimulation.PotionHeal + " CAN"; LastInfo = "İksir içti"; Say("Oh be, şifa!");
                            Effects.Burst(root, hero.transform.position + Vector3.up * 0.6f, new Color(0.4f, 1f, 0.5f), 16, 2f, 0.07f);
                            Sfx(SoundId.PotionDrink, 0.95f);
                            break;
                        case ItemType.SpeedBoots:
                            txt = $"HIZ x{e.Speed:0.0#}"; LastInfo = "Hız çizmesi giydi!"; Say("Rüzgâr gibiyim!", true);
                            Sfx(SoundId.PlaceCrate, 0.9f);
                            break;
                        case ItemType.Bomb:
                            txt = "BUM!"; LastInfo = "Bomba patladı!"; Say("Herkes yere!", true);
                            break;
                        default:
                            txt = Items.Get(s.Type).Name;
                            Sfx(SoundId.PlaceCrate, 0.9f);
                            break;
                    }
                    Effects.FloatText(root, hero.transform.position + Vector3.up * 1.9f, txt, col, 1.1f);
                    break;
                }
                case SimEventType.Bomb:
                    Effects.Burst(root, pos + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.15f), 26, 6f, 0.14f);
                    Effects.Burst(root, pos + Vector3.up * 0.3f, new Color(0.3f, 0.3f, 0.3f), 14, 3f, 0.18f);
                    Effects.Ring(root, pos, new Color(1f, 0.6f, 0.2f, 0.8f), 2.5f);
                    Sfx(SoundId.BombExplode, 1.0f);
                    hitstopTimer = 0.07f;
                    Shake(1.1f);
                    break;
                case SimEventType.TrapTriggered:
                    traps[e.Index].transform.localScale = Vector3.one;
                    heroAnim?.Hurt();
                    Effects.Burst(root, pos + Vector3.up * 0.2f, new Color(0.8f, 0.8f, 0.85f), 8, 3f, 0.07f);
                    Effects.FloatText(root, pos + Vector3.up * 1.6f, "TUZAK! -" + Mathf.RoundToInt(e.Amount), new Color(1f, 0.3f, 0.25f));
                    Sfx(SoundId.TrapSnap, 0.95f);
                    hitstopTimer = 0.05f;
                    Shake(0.45f);
                    LastInfo = "Gizli tuzağa bastı!";
                    Say(Pick(TrapLines), true);
                    break;
                case SimEventType.HeroDied:
                    heroAnim?.Die();
                    Effects.Burst(root, pos + Vector3.up * 0.5f, new Color(0.3f, 0.5f, 0.95f), 18, 4f);
                    Sfx(SoundId.HeroDie, 0.95f);
                    LastInfo = "Kahraman düştü!";
                    Shake(0.6f);
                    if (!Quiet && Speed < 10f)
                    {
                        // ölüm anı: ağır çekim, kamera yaklaşır, dünya griye döner
                        slowmoTimer = 1.5f;
                        if (cam) cam.Follow(hero.transform, 1f, 0.42f);
                        GameApp.SetGrade(true); graded = true;
                        Effects.FloatText(root, hero.transform.position + Vector3.up * 2.2f, "DÜŞTÜ", new Color(0.85f, 0.85f, 0.9f), 1.8f);
                    }
                    break;
                case SimEventType.ReachedExit:
                    Effects.Burst(root, pos + Vector3.up * 0.8f, new Color(0.6f, 0.95f, 1f), 30, 5f, 0.1f);
                    Effects.Ring(root, pos, new Color(0.6f, 0.95f, 1f, 0.8f), 2f);
                    Sfx(SoundId.PortalEnter, 1.0f, 0f);
                    LastInfo = "Çıkışa ulaştı!";
                    if (heroBar) heroBar.gameObject.SetActive(false);
                    var portalPos = view.ExitPortal ? view.ExitPortal.position + Vector3.up * 0.55f : hero.transform.position;
                    heroExiting = true;
                    Juice.PortalSuck(hero, portalPos, Speed > 10f ? 0.05f : 0.6f);
                    if (!Quiet && Speed < 10f)
                    {
                        // zafer anı: kısa ağır çekim, portal parlar
                        slowmoTimer = 0.7f;
                        Juice.ScreenFlash(new Color(0.75f, 0.97f, 1f, 0.75f), 0.6f);
                        Effects.Ring(root, portalPos, new Color(1f, 0.9f, 0.5f, 0.9f), 3.2f);
                        Effects.FloatText(root, portalPos + Vector3.up * 1.4f, "ZAFER!", UiKit.Gold, 2f);
                        for (int sp = 0; sp < 16; sp++)
                        {
                            var vel = (UnityEngine.Random.insideUnitSphere + Vector3.up * 1.2f).normalized * 3f;
                            Effects.Spark(root, portalPos, new Color(0.7f, 0.95f, 1f), vel, 0.07f, 0.9f);
                        }
                        Shake(0.5f);
                    }
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

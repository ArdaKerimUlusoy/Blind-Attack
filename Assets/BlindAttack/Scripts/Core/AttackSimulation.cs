using System;
using System.Collections.Generic;

namespace BlindAttack.Core
{
    public enum SimEventType : byte
    {
        HeroAttack,     // kahraman vurdu (Index = düşman, Amount = hasar)
        EnemyAttack,    // düşman vurdu (Index = düşman, Amount = hasar)
        EnemyDied,      // Index = düşman
        Pickup,         // Index = destek listesindeki kutu/bayrak (Dmg/Armor/Speed = alındıktan sonraki değerler)
        Bomb,           // Index = destek kutusu (patlama)
        TrapTriggered,  // Index = tuzak, Amount = hasar
        HeroDied,
        ReachedExit,
        Timeout,
    }

    public struct SimEvent
    {
        public float Time;
        public SimEventType Type;
        public int Index;
        public float Amount;
        public Vec2 Pos;
        public float Dmg, Armor, Speed; // Pickup sonrası kahraman değerleri (Armor = hasar azaltma oranı 0..1)
    }

    public class SimFrame
    {
        public float Time;
        public Vec2 HeroPos;
        public float HeroHp;
        public Vec2[] EnemyPos;
        public float[] EnemyHp;
    }

    public class SimEnemyInfo
    {
        public ItemType Type;
        public Cell Home;
        public float MaxHp;
    }

    public class SimResult
    {
        public readonly List<SimFrame> Frames = new List<SimFrame>();
        public readonly List<SimEvent> Events = new List<SimEvent>();
        public readonly List<SimEnemyInfo> Enemies = new List<SimEnemyInfo>();
        public readonly List<Placement> Traps = new List<Placement>();
        public readonly List<Placement> Supplies = new List<Placement>();
        public RoutePlanner.Plan Plan;
        public Cell HeroStart;
        public float HeroMaxHp;
        public bool Reached, HeroDied;
        public float Duration;
        public float Progress;   // 0..1
        public int Score;
        public float FrameInterval;
    }

    /// <summary>
    /// Bir saldırının baştan sona simülasyonu. Unity'den bağımsız ve deterministiktir:
    /// iki bilgisayar aynı girdiyle aynı sonucu üretir, böylece ağdan sadece yerleştirmeleri göndermek yeterli.
    /// </summary>
    public static class AttackSimulation
    {
        public const float Dt = 0.05f;
        public const float MaxTime = 100f;
        const int FrameEvery = 2;

        // Kahraman temel değerleri
        public const float HeroHp = 100f;
        public const float HeroDamage = 10f;
        const float HeroInterval = 0.7f, HeroRange = 1.2f, HeroSpeed = 3.2f, HeroEngage = 3.0f;

        class Enemy
        {
            public ItemDef Def; public Vec2 Pos, Home; public float Hp, Cooldown, RepathTimer;
            public bool Aggro, Alive = true; public List<Cell> Path; public int PathIdx;
        }

        class Supply { public Placement P; public bool Taken; }
        class Trap { public Placement P; public bool Fired; }

        public static SimResult Run(GridMap map, IList<Placement> defenders, IList<Placement> supplies)
        {
            var res = new SimResult { HeroMaxHp = HeroHp, FrameInterval = Dt * FrameEvery };
            var pf = new Pathfinder(map);

            var enemies = new List<Enemy>();
            var traps = new List<Trap>();
            foreach (var d in defenders)
            {
                if (d.Type == ItemType.SpikeTrap) { traps.Add(new Trap { P = d }); res.Traps.Add(d); continue; }
                var def = Items.Get(d.Type);
                enemies.Add(new Enemy { Def = def, Pos = d.Cell.Center, Home = d.Cell.Center, Hp = def.Hp });
                res.Enemies.Add(new SimEnemyInfo { Type = d.Type, Home = d.Cell, MaxHp = def.Hp });
            }
            var sups = new List<Supply>();
            foreach (var s in supplies) { sups.Add(new Supply { P = s, Taken = !Items.IsWaypoint(s.Type) }); res.Supplies.Add(s); }
            var plan = RoutePlanner.Make(map, supplies, pf);
            res.Plan = plan;
            res.HeroStart = plan.Start;
            int planPos = 0;

            // Kahraman durumu
            Vec2 heroPos = plan.Start.Center;
            float hp = HeroHp, dmg = HeroDamage, speedMul = 1f, armorMul = 1f, heroCd = 0f;
            int waypoint = -2; // -2: seçilmedi, -1: çıkış, >=0: destek kutusu
            List<Cell> heroPath = null; int heroPathIdx = 0; bool needRepath = true;
            int bestSteps = Math.Max(0, map.StepsToExit(plan.Start));
            int totalSteps = Math.Max(1, bestSteps);

            float t = 0f; int tick = 0;
            void Ev(SimEventType type, int index, float amount, Vec2 pos) =>
                res.Events.Add(new SimEvent { Time = t, Type = type, Index = index, Amount = amount, Pos = pos });

            void Record()
            {
                var f = new SimFrame { Time = t, HeroPos = heroPos, HeroHp = hp, EnemyPos = new Vec2[enemies.Count], EnemyHp = new float[enemies.Count] };
                for (int i = 0; i < enemies.Count; i++) { f.EnemyPos[i] = enemies[i].Pos; f.EnemyHp[i] = enemies[i].Alive ? enemies[i].Hp : 0f; }
                res.Frames.Add(f);
            }

            void HurtHero(float amount) { hp -= amount * armorMul; }

            int PickWaypoint()
            {
                // önceden planlanmış sırada, henüz alınmamış ilk hedef
                while (planPos < plan.Order.Count && sups[plan.Order[planPos]].Taken) planPos++;
                return planPos < plan.Order.Count ? plan.Order[planPos] : -1;
            }

            Record();
            bool done = false;
            while (!done)
            {
                t += Dt; tick++;

                // ---------- Kahraman ----------
                heroCd -= Dt;
                Enemy target = null; int targetIdx = -1; float targetDist = float.MaxValue;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Alive) continue;
                    float d = Vec2.Dist(heroPos, e.Pos);
                    if (d <= HeroEngage && d < targetDist && map.HasWalkLine(heroPos, e.Pos)) { target = e; targetIdx = i; targetDist = d; }
                }

                float step = HeroSpeed * speedMul * Dt;
                if (target != null)
                {
                    if (targetDist <= HeroRange)
                    {
                        if (heroCd <= 0f)
                        {
                            heroCd = HeroInterval / speedMul;
                            target.Hp -= dmg;
                            Ev(SimEventType.HeroAttack, targetIdx, dmg, target.Pos);
                            if (target.Hp <= 0f) { target.Alive = false; Ev(SimEventType.EnemyDied, targetIdx, 0, target.Pos); }
                        }
                    }
                    else heroPos = MoveToward(heroPos, target.Pos, step);
                    needRepath = true;
                }
                else
                {
                    if (waypoint == -2 || (waypoint >= 0 && sups[waypoint].Taken))
                    {
                        waypoint = PickWaypoint();
                        needRepath = true;
                    }
                    Cell goal = waypoint >= 0 ? sups[waypoint].P.Cell : map.Exit;
                    if (needRepath)
                    {
                        heroPath = pf.FindPath(heroPos.ToCell(), goal) ?? new List<Cell>();
                        heroPathIdx = 0; needRepath = false;
                    }
                    if (heroPathIdx < heroPath.Count)
                    {
                        var c = heroPath[heroPathIdx].Center;
                        heroPos = MoveToward(heroPos, c, step);
                        if (Vec2.Dist(heroPos, c) < 0.01f) heroPathIdx++;
                    }
                    else heroPos = MoveToward(heroPos, goal.Center, step);
                }

                var heroCell = heroPos.ToCell();
                int steps = map.StepsToExit(heroCell);
                if (steps >= 0 && steps < bestSteps) bestSteps = steps;

                // Destek kutuları
                for (int i = 0; i < sups.Count; i++)
                {
                    var s = sups[i];
                    if (s.Taken || Vec2.Dist(heroPos, s.P.Cell.Center) > 0.45f) continue;
                    s.Taken = true;
                    switch (s.P.Type)
                    {
                        case ItemType.SwordCrate: dmg += 6f; break;
                        case ItemType.ArmorCrate: armorMul = Math.Max(0.4f, armorMul * 0.7f); break;
                        case ItemType.Potion: hp = Math.Min(HeroHp, hp + 45f); break;
                        case ItemType.SpeedBoots: speedMul *= 1.3f; break;
                        case ItemType.Bomb:
                            Ev(SimEventType.Bomb, i, 40f, s.P.Cell.Center);
                            for (int k = 0; k < enemies.Count; k++)
                            {
                                var e = enemies[k];
                                if (!e.Alive || Vec2.Dist(e.Pos, heroPos) > 2.5f) continue;
                                e.Hp -= 40f;
                                e.Aggro = true;
                                if (e.Hp <= 0f) { e.Alive = false; Ev(SimEventType.EnemyDied, k, 0, e.Pos); }
                            }
                            break;
                    }
                    res.Events.Add(new SimEvent { Time = t, Type = SimEventType.Pickup, Index = i, Pos = s.P.Cell.Center, Dmg = dmg, Armor = 1f - armorMul, Speed = speedMul });
                }

                // Tuzaklar
                for (int i = 0; i < traps.Count; i++)
                {
                    var tr = traps[i];
                    if (tr.Fired || tr.P.Cell != heroCell) continue;
                    tr.Fired = true;
                    float d = Items.Get(ItemType.SpikeTrap).Damage;
                    HurtHero(d);
                    Ev(SimEventType.TrapTriggered, i, d * armorMul, tr.P.Cell.Center);
                }

                // ---------- Düşmanlar ----------
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Alive) continue;
                    e.Cooldown -= Dt;
                    e.RepathTimer -= Dt;
                    float d = Vec2.Dist(e.Pos, heroPos);
                    bool los = d <= e.Def.Aggro * 2f && map.HasLineOfSight(e.Pos, heroPos);

                    if (!e.Aggro && d <= e.Def.Aggro && los) e.Aggro = true;

                    if (e.Aggro)
                    {
                        if (e.Def.Ranged)
                        {
                            if (d <= e.Def.Range && los)
                            {
                                if (e.Cooldown <= 0f) { e.Cooldown = e.Def.AttackInterval; HurtHero(e.Def.Damage); Ev(SimEventType.EnemyAttack, i, e.Def.Damage * armorMul, e.Pos); }
                            }
                            else if (d > e.Def.Aggro * 1.6f) e.Aggro = false;
                        }
                        else
                        {
                            if (Vec2.Dist(e.Pos, e.Home) > 8f) { e.Aggro = false; e.Path = null; }
                            else if (d <= e.Def.Range)
                            {
                                if (e.Cooldown <= 0f) { e.Cooldown = e.Def.AttackInterval; HurtHero(e.Def.Damage); Ev(SimEventType.EnemyAttack, i, e.Def.Damage * armorMul, e.Pos); }
                            }
                            else if (los && map.HasWalkLine(e.Pos, heroPos)) { e.Pos = MoveToward(e.Pos, heroPos, e.Def.Speed * Dt); e.Path = null; }
                            else
                            {
                                if (e.Path == null || e.RepathTimer <= 0f)
                                {
                                    e.Path = pf.FindPath(e.Pos.ToCell(), heroCell); e.PathIdx = 0; e.RepathTimer = 0.4f;
                                }
                                FollowPath(e, e.Def.Speed * Dt);
                            }
                        }
                    }
                    else if (e.Def.Speed > 0f && Vec2.Dist(e.Pos, e.Home) > 0.05f)
                    {
                        if (e.Path == null) { e.Path = pf.FindPath(e.Pos.ToCell(), e.Home.ToCell()); e.PathIdx = 0; }
                        if (e.Path == null || e.PathIdx >= e.Path.Count) e.Pos = MoveToward(e.Pos, e.Home, e.Def.Speed * Dt);
                        else FollowPath(e, e.Def.Speed * Dt);
                    }
                    else e.Path = null;
                }

                // ---------- Bitiş kontrolü ----------
                if (hp <= 0f) { hp = 0f; res.HeroDied = true; Ev(SimEventType.HeroDied, 0, 0, heroPos); done = true; }
                else if (Vec2.Dist(heroPos, map.Exit.Center) < 0.4f) { res.Reached = true; bestSteps = 0; Ev(SimEventType.ReachedExit, 0, 0, heroPos); done = true; }
                else if (t >= MaxTime) { Ev(SimEventType.Timeout, 0, 0, heroPos); done = true; }

                if (done || tick % FrameEvery == 0) Record();
            }

            res.Duration = t;
            res.Progress = res.Reached ? 1f : Math.Max(0f, Math.Min(0.99f, 1f - bestSteps / (float)totalSteps));
            res.Score = res.Reached ? 100 + (int)Math.Ceiling(hp) : (int)Math.Floor(res.Progress * 99f);
            return res;
        }

        static void FollowPath(Enemy e, float step)
        {
            if (e.Path == null || e.PathIdx >= e.Path.Count) return;
            var c = e.Path[e.PathIdx].Center;
            e.Pos = MoveToward(e.Pos, c, step);
            if (Vec2.Dist(e.Pos, c) < 0.01f) e.PathIdx++;
        }

        static Vec2 MoveToward(Vec2 from, Vec2 to, float step)
        {
            var d = to - from;
            float len = d.Length;
            if (len <= step || len < 1e-5f) return to;
            return from + d * (step / len);
        }
    }
}

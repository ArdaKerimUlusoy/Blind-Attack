using System.Collections.Generic;
using BlindAttack.Core;
using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>Bir saldırının özet istatistikleri (tur sonu ekranı ve başarımlar için). Simülasyon sonucundan çıkarılır.</summary>
    public class AttackStats
    {
        public bool Reached, Died;
        public int Score, Kills, Enemies, DamageDealt, DamageTaken, Crates, CratesTotal, Traps, FinalHp, BombKills;
        public float Time;
        public ItemType TopThreat = ItemType.Goblin;
        public int TopThreatDamage;

        public static AttackStats From(SimResult r)
        {
            var s = new AttackStats { Reached = r.Reached, Died = r.HeroDied, Score = r.Score, Enemies = r.Enemies.Count, Time = r.Duration };
            var threat = new Dictionary<ItemType, float>();
            float lastBombTime = -1f; int bombKillsNow = 0;
            foreach (var e in r.Events)
            {
                switch (e.Type)
                {
                    case SimEventType.HeroAttack:
                    case SimEventType.HeroCleave:
                        s.DamageDealt += Mathf.RoundToInt(e.Amount);
                        break;
                    case SimEventType.EnemyAttack:
                    {
                        s.DamageTaken += Mathf.RoundToInt(e.Amount);
                        var t = r.Enemies[e.Index].Type;
                        threat.TryGetValue(t, out float v); threat[t] = v + e.Amount;
                        break;
                    }
                    case SimEventType.TrapTriggered:
                    {
                        s.DamageTaken += Mathf.RoundToInt(e.Amount);
                        s.Traps++;
                        threat.TryGetValue(ItemType.SpikeTrap, out float v); threat[ItemType.SpikeTrap] = v + e.Amount;
                        break;
                    }
                    case SimEventType.EnemyDied:
                        s.Kills++;
                        if (Mathf.Abs(e.Time - lastBombTime) < 0.001f) { bombKillsNow++; s.BombKills = Mathf.Max(s.BombKills, bombKillsNow); }
                        break;
                    case SimEventType.Bomb:
                        lastBombTime = e.Time; bombKillsNow = 0;
                        break;
                    case SimEventType.Pickup:
                        if (r.Supplies[e.Index].Type != ItemType.Flag) s.Crates++;
                        break;
                }
            }
            foreach (var p in r.Supplies) if (p.Type != ItemType.Hero && p.Type != ItemType.Flag) s.CratesTotal++;
            foreach (var kv in threat) if (kv.Value > s.TopThreatDamage) { s.TopThreatDamage = Mathf.RoundToInt(kv.Value); s.TopThreat = kv.Key; }
            s.FinalHp = r.Frames.Count > 0 ? Mathf.CeilToInt(r.Frames[r.Frames.Count - 1].HeroHp) : 0;
            if (r.Reached && r.Frames.Count > 0) s.FinalHp = Mathf.Max(1, s.FinalHp);
            return s;
        }

        public string ResultText => Reached ? "<color=#4E8A2E><b>Çıkışa ulaştı!</b></color>" : Died ? "<color=#B5452F><b>Kahraman düştü</b></color>" : "<b>Süre doldu</b>";
    }

    /// <summary>Kalıcı başarımlar (bu bilgisayarda saklanır).</summary>
    public static class Achievements
    {
        public class Def { public string Id, Name, Desc; }

        public static readonly Def[] All =
        {
            new Def { Id = "ilk_cikis", Name = "İlk Adım", Desc = "Kahramanını ilk kez çıkışa ulaştır." },
            new Def { Id = "kusursuz", Name = "Kusursuz", Desc = "Hiç hasar almadan çıkışa ulaş." },
            new Def { Id = "kil_payi", Name = "Kıl Payı", Desc = "15 candan az canla çıkışa ulaş." },
            new Def { Id = "kasap", Name = "Kasap", Desc = "Tek saldırıda 8 düşman öldür." },
            new Def { Id = "bombaci", Name = "Bombacı", Desc = "Tek bombayla 2 düşmanı birden yok et." },
            new Def { Id = "hazine", Name = "Hazine Avcısı", Desc = "Tek saldırıda 6 kutu topla." },
            new Def { Id = "ruzgar", Name = "Rüzgâr Gibi", Desc = "Çıkışa 20 saniyeden kısa sürede ulaş." },
            new Def { Id = "duvar", Name = "Aşılmaz Duvar", Desc = "Rakibin kahramanı senin savunmanda düşsün." },
            new Def { Id = "tuzakci", Name = "Tuzakçı", Desc = "Rakip tek saldırıda 3 tuzağına bassın." },
            new Def { Id = "sampiyon", Name = "Şampiyon", Desc = "Bir maç kazan." },
            new Def { Id = "ezici", Name = "Ezici Üstünlük", Desc = "Bir maçı 3-0 kazan." },
        };

        const string Prefix = "BlindAttack.Ach.";

        public static bool Has(string id) => PlayerPrefs.GetInt(Prefix + id, 0) == 1;
        public static int UnlockedCount { get { int n = 0; foreach (var d in All) if (Has(d.Id)) n++; return n; } }

        static void Try(List<Def> got, string id, bool cond)
        {
            if (!cond || Has(id)) return;
            PlayerPrefs.SetInt(Prefix + id, 1);
            foreach (var d in All) if (d.Id == id) got.Add(d);
        }

        /// <summary>Kendi saldırımın sonucu.</summary>
        public static List<Def> CheckAttack(AttackStats s)
        {
            var got = new List<Def>();
            Try(got, "ilk_cikis", s.Reached);
            Try(got, "kusursuz", s.Reached && s.DamageTaken == 0);
            Try(got, "kil_payi", s.Reached && s.FinalHp < 15);
            Try(got, "kasap", s.Kills >= 8);
            Try(got, "bombaci", s.BombKills >= 2);
            Try(got, "hazine", s.Crates >= 6);
            Try(got, "ruzgar", s.Reached && s.Time < 20f);
            if (got.Count > 0) PlayerPrefs.Save();
            return got;
        }

        /// <summary>Rakibin benim savunmama yaptığı saldırı.</summary>
        public static List<Def> CheckDefense(AttackStats s)
        {
            var got = new List<Def>();
            Try(got, "duvar", s.Died);
            Try(got, "tuzakci", s.Traps >= 3);
            if (got.Count > 0) PlayerPrefs.Save();
            return got;
        }

        public static List<Def> CheckMatch(bool won, int myWins, int theirWins)
        {
            var got = new List<Def>();
            Try(got, "sampiyon", won);
            Try(got, "ezici", won && myWins >= 3 && theirWins == 0);
            if (got.Count > 0) PlayerPrefs.Save();
            return got;
        }
    }
}

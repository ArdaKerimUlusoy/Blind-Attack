using System;
using System.Collections.Generic;

namespace BlindAttack.Core
{
    /// <summary>Tek kişilik modda rakibin yerleştirmelerini yapan yapay zeka.
    /// Savunmada kahramanın her başlangıç noktasından geçmek zorunda kaldığı dar geçitleri bulur;
    /// saldırıda dengeli kutu seçer ve rotanın başına/ortasına dizer.</summary>
    public static class BotPlanner
    {
        /// <summary>Başlangıç bölgesindeki her hücreden çıkışa giden yollarda her hücrenin kaç kez geçildiği.</summary>
        public static Dictionary<Cell, int> PathTraffic(GridMap map)
        {
            var pf = new Pathfinder(map);
            var cnt = new Dictionary<Cell, int>();
            for (int x = 0; x < map.Width; x += 1)
                for (int y = 0; y < map.Height; y += 1)
                {
                    var st = new Cell(x, y);
                    if (!map.CanSpawnHero(st) || ((x + y) & 1) == 1) continue; // yarısı yeterli, daha hızlı
                    var p = pf.FindPath(st, map.Exit);
                    if (p == null) continue;
                    foreach (var c in p) { cnt.TryGetValue(c, out int k); cnt[c] = k + 1; }
                }
            return cnt;
        }

        public static List<Placement> PlanDefense(GridMap map, int seed)
        {
            var rng = new Random(seed);
            var list = new List<Placement>();
            var traffic = PathTraffic(map);

            // en yoğun geçilen hücreler (yerleştirmeye izin verilenler; biraz rastgelelikle, her maç aynı olmasın)
            var hot = new List<Cell>();
            var key = new Dictionary<Cell, double>();
            foreach (var kv in traffic)
            {
                if (map.StepsToExit(kv.Key) < MatchRules.MinDefenseStepsFromExit) continue;
                hot.Add(kv.Key);
                key[kv.Key] = kv.Value * (0.8 + rng.NextDouble() * 0.4);
            }
            hot.Sort((a, b) => key[b].CompareTo(key[a]));

            // savunma tarzı: her maç farklı bir karışım
            ItemType[][] styles =
            {
                new[] { ItemType.Ogre, ItemType.Goblin, ItemType.Archer, ItemType.Goblin },
                new[] { ItemType.Goblin, ItemType.Goblin, ItemType.Archer, ItemType.SpikeTrap },
                new[] { ItemType.Ogre, ItemType.Archer, ItemType.Goblin, ItemType.Goblin, ItemType.SpikeTrap },
                new[] { ItemType.Ogre, ItemType.SpikeTrap, ItemType.Goblin, ItemType.Archer },
            };
            var mix = styles[rng.Next(styles.Length)];
            // aday hücreler: dar geçitler ve hemen yanları, yoğunluk sırasıyla
            var cands = new List<Cell>();
            var seen = new HashSet<Cell>();
            for (int i = 0; i < hot.Count && i < 50; i++)
            {
                var c = hot[i];
                foreach (var n in new[] { c, new Cell(c.X + 1, c.Y), new Cell(c.X - 1, c.Y), new Cell(c.X, c.Y + 1), new Cell(c.X, c.Y - 1) })
                    if (seen.Add(n)) cands.Add(n);
            }
            int mi = 0, fails = 0;
            while (fails < mix.Length * 2 && list.Count < MatchRules.MaxDefenders && MatchRules.Spent(list) < MatchRules.DefenseBudget)
            {
                var type = mix[mi % mix.Length]; mi++;
                bool ok = false;
                int skip = rng.Next(3); // her seferinde en iyi hücreyi değil, ilk birkaçtan birini al
                foreach (var c in cands)
                {
                    if (MatchRules.CheckPlacement(map, list, type, c) != null) continue;
                    if (skip-- > 0) continue;
                    list.Add(new Placement(type, c)); ok = true; break;
                }
                fails = ok ? 0 : fails + 1;
            }
            return list;
        }

        public static List<Placement> PlanSupplies(GridMap map, int seed)
        {
            var rng = new Random(seed * 31 + 7);
            var list = new List<Placement>();
            // kahramanı başlangıç bölgesinde rastgele bir yere koy
            var zone = new List<Cell>();
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (map.CanSpawnHero(new Cell(x, y))) zone.Add(new Cell(x, y));
            var start = zone.Count > 0 ? zone[rng.Next(zone.Count)] : MatchRules.DefaultHeroCell(map);
            list.Add(new Placement(ItemType.Hero, start));

            var route = Route(map, start);
            // denge analizinde öne çıkan setler (aynı kutudan en fazla 2)
            ItemType[][] kits =
            {
                new[] { ItemType.SwordCrate, ItemType.SwordCrate, ItemType.ArmorCrate, ItemType.ArmorCrate, ItemType.SpeedBoots },
                new[] { ItemType.SwordCrate, ItemType.ArmorCrate, ItemType.ArmorCrate, ItemType.SpeedBoots, ItemType.SpeedBoots },
                new[] { ItemType.SwordCrate, ItemType.SwordCrate, ItemType.ArmorCrate, ItemType.ArmorCrate, ItemType.Potion },
                new[] { ItemType.SwordCrate, ItemType.SwordCrate, ItemType.Potion, ItemType.Potion, ItemType.Bomb },
                new[] { ItemType.SwordCrate, ItemType.ArmorCrate, ItemType.Potion, ItemType.Potion, ItemType.SpeedBoots, ItemType.SpeedBoots },
            };
            var kit = kits[rng.Next(kits.Length)];
            for (int i = 0; i < kit.Length; i++)
            {
                // güçlendirmeler rotanın başına, iksirler ortasına/sonuna doğru
                bool late = kit[i] == ItemType.Potion || kit[i] == ItemType.Bomb;
                for (int tries = 0; tries < 60; tries++)
                {
                    double f = late ? 0.3 + rng.NextDouble() * 0.45 : 0.05 + rng.NextDouble() * 0.3;
                    var c = route[Math.Min(route.Count - 1, (int)(f * route.Count))];
                    c = new Cell(c.X + rng.Next(-1, 2), c.Y + rng.Next(-1, 2));
                    if (MatchRules.CheckPlacement(map, list, kit[i], c) == null) { list.Add(new Placement(kit[i], c)); break; }
                }
            }
            // kalan altınla sığan bir kutu daha
            var extras = new[] { ItemType.Potion, ItemType.SpeedBoots, ItemType.Bomb };
            for (int tries = 0; tries < 90; tries++)
            {
                var t = extras[tries % extras.Length];
                if (MatchRules.Spent(list) + Items.Get(t).Cost > MatchRules.SupplyBudget) continue;
                var c = route[Math.Min(route.Count - 1, (int)((0.4 + rng.NextDouble() * 0.4) * route.Count))];
                if (MatchRules.CheckPlacement(map, list, t, c) == null) { list.Add(new Placement(t, c)); break; }
            }
            return list;
        }

        static List<Cell> Route(GridMap map, Cell from)
        {
            var path = new Pathfinder(map).FindPath(from, map.Exit) ?? new List<Cell>();
            if (path.Count > 0) path.RemoveAt(path.Count - 1); // çıkış hücresi hariç
            if (path.Count == 0) path.Add(from);
            return path;
        }
    }
}

using System;
using System.Collections.Generic;

namespace BlindAttack.Core
{
    /// <summary>Tek kişilik test modunda rakibin yerleştirmelerini yapan basit yapay zeka.</summary>
    public static class BotPlanner
    {
        public static List<Placement> PlanDefense(GridMap map, int seed)
        {
            var rng = new Random(seed);
            var list = new List<Placement>();
            var route = Route(map, MatchRules.DefaultHeroCell(map));
            var choices = new[] { ItemType.Goblin, ItemType.Goblin, ItemType.Archer, ItemType.Ogre, ItemType.SpikeTrap, ItemType.SpikeTrap };
            for (int attempt = 0; attempt < 600 && MatchRules.Spent(list) < MatchRules.DefenseBudget; attempt++)
            {
                var type = choices[rng.Next(choices.Length)];
                Cell cell;
                if (type == ItemType.SpikeTrap || rng.NextDouble() < 0.5)
                {
                    // önerilen rotanın ikinci yarısına ya da yanına (kahraman büyük ihtimalle oradan geçer)
                    var c = route[route.Count / 3 + rng.Next(Math.Max(1, route.Count - route.Count / 3))];
                    cell = new Cell(c.X + rng.Next(-2, 3), c.Y + rng.Next(-2, 3));
                }
                else cell = new Cell(rng.Next(map.Width), rng.Next(map.Height));
                if (MatchRules.CheckPlacement(map, list, type, cell) == null) list.Add(new Placement(type, cell));
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
            var types = new[] { ItemType.SwordCrate, ItemType.ArmorCrate, ItemType.Potion, ItemType.SpeedBoots, ItemType.Bomb };
            for (int attempt = 0; attempt < 300 && MatchRules.Spent(list) < MatchRules.SupplyBudget; attempt++)
            {
                var type = types[rng.Next(types.Length)];
                // rotanın ilk yarısına daha çok koy, kahraman erken güçlensin
                int idx = (int)(Math.Pow(rng.NextDouble(), 1.4) * route.Count);
                var cell = route[Math.Min(idx, route.Count - 1)];
                if (MatchRules.CheckPlacement(map, list, type, cell) == null) list.Add(new Placement(type, cell));
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

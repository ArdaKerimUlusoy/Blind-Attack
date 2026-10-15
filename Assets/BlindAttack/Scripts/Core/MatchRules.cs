using System.Collections.Generic;

namespace BlindAttack.Core
{
    /// <summary>Oyunun ayarlanabilir kuralları. Denge için buradaki sayılarla oynayın.</summary>
    public static class MatchRules
    {
        public const int Rounds = 3;
        public const int DefenseBudget = 28;
        public const int SupplyBudget = 14;
        public const float DefensePhaseSeconds = 50f;
        public const float SupplyPhaseSeconds = 45f;
        /// <summary>Savunma birimleri çıkışa en az bu kadar adım uzakta olmalı (kapıyı tamamen kapatmasınlar).</summary>
        public const int MinDefenseStepsFromExit = 2;
        public const int MaxDefenders = 18;
        public const int MaxSupplies = 8;   // kutular (bayraklar hariç)
        public const int MaxFlags = 3;

        public static int Budget(bool defense) => defense ? DefenseBudget : SupplyBudget;

        /// <summary>Bu hücreye bu tipte bir şey konabilir mi? Hata mesajı döner (null = uygun).
        /// Kahraman için: zaten konmuşsa, yeni yere taşınacağı varsayılır.</summary>
        public static string CheckPlacement(GridMap map, IList<Placement> existing, ItemType type, Cell cell)
        {
            var def = Items.Get(type);
            if (!map.InBounds(cell)) return "Harita dışı";
            var tile = map.Get(cell);
            if (tile == Tile.Exit) return "Çıkışa konamaz";
            if (!map.IsWalkable(cell)) return "Buraya konamaz";
            if (map.StepsToExit(cell) < 0) return "Ulaşılamayan yer";

            if (type == ItemType.Hero)
            {
                if (!map.CanSpawnHero(cell)) return "Kahraman sadece mavi başlangıç bölgesine konabilir";
            }
            else if (def.IsDefense && map.StepsToExit(cell) < MinDefenseStepsFromExit) return "Çıkışa çok yakın";

            int spent = 0, crates = 0, flags = 0, defenders = 0;
            foreach (var p in existing)
            {
                if (p.Cell == cell && p.Type != ItemType.Hero && type != ItemType.Hero) return "Dolu";
                if (p.Cell == cell && (p.Type == ItemType.Hero) != (type == ItemType.Hero)) return "Dolu";
                spent += Items.Get(p.Type).Cost;
                if (p.Type == ItemType.Flag) flags++;
                else if (Items.Get(p.Type).IsDefense) defenders++;
                else if (p.Type != ItemType.Hero) crates++;
            }
            if (spent + def.Cost > Budget(def.IsDefense)) return "Altın yetmiyor";
            if (def.IsDefense && defenders >= MaxDefenders) return "Sınıra ulaştın";
            if (type == ItemType.Flag && flags >= MaxFlags) return $"En fazla {MaxFlags} bayrak";
            if (!def.IsDefense && type != ItemType.Flag && type != ItemType.Hero && crates >= MaxSupplies) return "Sınıra ulaştın";
            return null;
        }

        public static int Spent(IList<Placement> list)
        {
            int s = 0;
            foreach (var p in list) s += Items.Get(p.Type).Cost;
            return s;
        }

        public static bool HasHero(IList<Placement> list)
        {
            foreach (var p in list) if (p.Type == ItemType.Hero) return true;
            return false;
        }

        /// <summary>Ağdan gelen listeyi kurallara göre temizler (hile/hata koruması).
        /// Destek listesinde kahraman yoksa önerilen başlangıca eklenir.</summary>
        public static List<Placement> Sanitize(GridMap map, IList<Placement> input, bool defense)
        {
            var result = new List<Placement>();
            foreach (var p in input)
            {
                if (Items.Get(p.Type).IsDefense != defense) continue;
                if (p.Type == ItemType.Hero && HasHero(result)) continue;
                if (CheckPlacement(map, result, p.Type, p.Cell) == null) result.Add(p);
            }
            if (!defense && !HasHero(result)) result.Insert(0, new Placement(ItemType.Hero, DefaultHeroCell(map)));
            return result;
        }

        public static Cell DefaultHeroCell(GridMap map) => map.CanSpawnHero(map.Start) ? map.Start : FarthestCell(map);

        static Cell FarthestCell(GridMap map)
        {
            Cell best = map.Start; int bestD = -1;
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    int d = map.StepsToExit(new Cell(x, y));
                    if (d > bestD) { bestD = d; best = new Cell(x, y); }
                }
            return best;
        }
    }
}

using System.Collections.Generic;

namespace BlindAttack.Core
{
    /// <summary>
    /// Kahramanın rotası: başlangıçtan en yakın hedefe (kutu ya da bayrak), oradan kalanların en yakınına...
    /// en sonunda çıkışa. Sıra saldırı başlamadan bir kez hesaplanır ve değişmez,
    /// böylece oyuncunun yerleştirme sırasında gördüğü rota ile saldırıdaki rota aynıdır.
    /// </summary>
    public static class RoutePlanner
    {
        public class Plan
        {
            public Cell Start;
            /// <summary>Ziyaret sırasıyla hedeflerin, verilen listedeki indeksleri.</summary>
            public readonly List<int> Order = new List<int>();
            /// <summary>Başlangıçtan çıkışa kadar tüm yol (hücreler, başlangıç dahil).</summary>
            public readonly List<Cell> FullPath = new List<Cell>();
        }

        public static Cell HeroStart(GridMap map, IList<Placement> supplies)
        {
            foreach (var p in supplies) if (p.Type == ItemType.Hero) return p.Cell;
            return MatchRules.DefaultHeroCell(map);
        }

        public static Plan Make(GridMap map, IList<Placement> supplies, Pathfinder pf = null)
        {
            pf ??= new Pathfinder(map);
            var plan = new Plan { Start = HeroStart(map, supplies) };
            var remaining = new List<int>();
            for (int i = 0; i < supplies.Count; i++)
                if (Items.IsWaypoint(supplies[i].Type)) remaining.Add(i);

            var cur = plan.Start;
            plan.FullPath.Add(cur);
            while (remaining.Count > 0)
            {
                int bestK = -1; float bestLen = float.PositiveInfinity;
                for (int k = 0; k < remaining.Count; k++)
                {
                    float len = pf.PathLength(cur, supplies[remaining[k]].Cell);
                    if (len < bestLen) { bestLen = len; bestK = k; }
                }
                if (bestK < 0) break; // ulaşılamayan hedefler atlanır
                int idx = remaining[bestK];
                remaining.RemoveAt(bestK);
                plan.Order.Add(idx);
                var seg = pf.FindPath(cur, supplies[idx].Cell);
                if (seg != null) plan.FullPath.AddRange(seg);
                cur = supplies[idx].Cell;
            }
            var last = pf.FindPath(cur, map.Exit);
            if (last != null) plan.FullPath.AddRange(last);
            return plan;
        }
    }
}

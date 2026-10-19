using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlindAttack.Core
{
    /// <summary>Haritaya yerleştirilebilen her şey.</summary>
    public enum ItemType : byte
    {
        // Savunma (kendi haritana koyarsın)
        Goblin = 0,
        Archer = 1,
        Ogre = 2,
        SpikeTrap = 3,
        // Destek (rakibin haritasına, kendi kahramanın için koyarsın)
        SwordCrate = 10,
        ArmorCrate = 11,
        Potion = 12,
        SpeedBoots = 13,
        Bomb = 14,
        Flag = 15,   // rota bayrağı: kahraman buraya da uğrar
        Hero = 20,   // kahramanın başlangıç noktası
    }

    public class ItemDef
    {
        public ItemType Type;
        public string Name;
        public string Description;
        public int Cost;
        public bool IsDefense;
        // Savaş değerleri (sadece düşmanlar için)
        public float Hp, Damage, AttackInterval, Range, Aggro, Speed;
        public bool Ranged;
    }

    public static class Items
    {
        public static readonly Dictionary<ItemType, ItemDef> All = new Dictionary<ItemType, ItemDef>();

        public static readonly ItemType[] DefenseTypes = { ItemType.Goblin, ItemType.Archer, ItemType.Ogre, ItemType.SpikeTrap };
        public static readonly ItemType[] SupplyTypes = { ItemType.Hero, ItemType.SwordCrate, ItemType.ArmorCrate, ItemType.Potion, ItemType.SpeedBoots, ItemType.Bomb, ItemType.Flag };

        /// <summary>Kahramanın uğradığı hedefler (kutular ve bayraklar).</summary>
        public static bool IsWaypoint(ItemType t) => t != ItemType.Hero && !Get(t).IsDefense;

        static Items()
        {
            Add(new ItemDef { Type = ItemType.Goblin, Name = "Goblin", Description = "Hızlı, ucuz yakın dövüşçü", Cost = 2, IsDefense = true,
                Hp = 35, Damage = 6, AttackInterval = 0.8f, Range = 1.1f, Aggro = 3.5f, Speed = 2.6f });
            Add(new ItemDef { Type = ItemType.Archer, Name = "Okçu", Description = "Yerinden kıpırdamaz, uzaktan vurur", Cost = 3, IsDefense = true,
                Hp = 30, Damage = 7, AttackInterval = 1.2f, Range = 4.5f, Aggro = 5f, Speed = 0f, Ranged = true });
            Add(new ItemDef { Type = ItemType.Ogre, Name = "Dev", Description = "Yavaş ama çok sert", Cost = 7, IsDefense = true,
                Hp = 95, Damage = 16, AttackInterval = 1.8f, Range = 1.3f, Aggro = 3f, Speed = 1.6f });
            Add(new ItemDef { Type = ItemType.SpikeTrap, Name = "Diken Tuzağı", Description = "Gizli. Üstüne basana 18 hasar", Cost = 1, IsDefense = true,
                Damage = 18 });

            Add(new ItemDef { Type = ItemType.Hero, Name = "Kahraman", Description = "Başlangıç noktası. Mavi bölgede istediğin yere koy", Cost = 0 });
            Add(new ItemDef { Type = ItemType.SwordCrate, Name = "Kılıç", Description = "Kılıç alev alır: +3 saldırı gücü (en fazla 2)", Cost = 3 });
            Add(new ItemDef { Type = ItemType.ArmorCrate, Name = "Zırh", Description = "Zırh giyer: alınan hasar %15 azalır (en fazla 2)", Cost = 3 });
            Add(new ItemDef { Type = ItemType.Potion, Name = "İksir", Description = "20 can doldurur (en fazla 2)", Cost = 2 });
            Add(new ItemDef { Type = ItemType.SpeedBoots, Name = "Çizme", Description = "Hız çizmesi: %20 daha hızlı yürür ve vurur (en fazla 2)", Cost = 2 });
            Add(new ItemDef { Type = ItemType.Bomb, Name = "Bomba", Description = "Alınca patlar: 2.5 kare içindeki düşmanlara 60 hasar (en fazla 2)", Cost = 3 });
            Add(new ItemDef { Type = ItemType.Flag, Name = "Bayrak", Description = "Ücretsiz rota noktası: kahraman buradan da geçer", Cost = 0 });
        }

        static void Add(ItemDef d) => All[d.Type] = d;
        public static ItemDef Get(ItemType t) => All[t];
    }

    public struct Placement
    {
        public ItemType Type;
        public Cell Cell;
        public Placement(ItemType t, Cell c) { Type = t; Cell = c; }
    }

    /// <summary>Yerleştirme listesini ağ üzerinden göndermek için metne çevirir: "tip,x,y;tip,x,y".</summary>
    public static class PlacementCodec
    {
        public static string Encode(IList<Placement> list)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append((int)list[i].Type).Append(',').Append(list[i].Cell.X).Append(',').Append(list[i].Cell.Y);
            }
            return sb.ToString();
        }

        public static List<Placement> Decode(string s)
        {
            var list = new List<Placement>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var part in s.Split(';'))
            {
                var p = part.Split(',');
                if (p.Length != 3) continue;
                if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int t)) continue;
                if (!int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)) continue;
                if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)) continue;
                if (!Items.All.ContainsKey((ItemType)t)) continue;
                list.Add(new Placement((ItemType)t, new Cell(x, y)));
            }
            return list;
        }
    }
}

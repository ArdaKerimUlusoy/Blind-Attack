using System.Collections.Generic;
using BlindAttack.Core;
using UnityEngine;

namespace BlindAttack.Game
{
    /// <summary>
    /// Birimlerin, kahramanın ve kutuların stilize 3B modelleri (prosedürel).
    /// Hand-painted modeller geldiğinde sadece bu dosyadaki Create* fonksiyonlarını prefab yükleyecek şekilde değiştirmek yeterli.
    /// </summary>
    public static class Visuals
    {
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Gen.Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Create(ItemType type)
        {
            switch (type)
            {
                case ItemType.Goblin: return Goblin();
                case ItemType.Archer: return Archer();
                case ItemType.Ogre: return Ogre();
                case ItemType.SpikeTrap: return Spikes();
                case ItemType.SwordCrate: return Crate(IconColor(type), "sword");
                case ItemType.ArmorCrate: return Crate(IconColor(type), "armor");
                case ItemType.Potion: return Potion();
                case ItemType.SpeedBoots: return Crate(IconColor(type), "boots");
                case ItemType.Bomb: return Bomb();
                case ItemType.Flag: return Flag();
                case ItemType.Hero: return HeroMarker();
            }
            return new GameObject(type.ToString());
        }

        /// <summary>Palette ve arayüzde kullanılan tanıtıcı renk.</summary>
        public static Color IconColor(ItemType t)
        {
            switch (t)
            {
                case ItemType.Goblin: return new Color(0.45f, 0.70f, 0.30f);
                case ItemType.Archer: return new Color(0.70f, 0.25f, 0.25f);
                case ItemType.Ogre: return new Color(0.55f, 0.45f, 0.65f);
                case ItemType.SpikeTrap: return new Color(0.55f, 0.55f, 0.58f);
                case ItemType.SwordCrate: return new Color(0.95f, 0.45f, 0.15f);
                case ItemType.ArmorCrate: return new Color(0.40f, 0.62f, 0.90f);
                case ItemType.Potion: return new Color(0.92f, 0.30f, 0.45f);
                case ItemType.SpeedBoots: return new Color(0.98f, 0.80f, 0.20f);
                case ItemType.Bomb: return new Color(0.25f, 0.25f, 0.28f);
                case ItemType.Flag: return new Color(0.20f, 0.80f, 0.85f);
                case ItemType.Hero: return new Color(0.22f, 0.45f, 0.85f);
            }
            return Color.white;
        }

        static GameObject Root(string name, out Transform body)
        {
            var root = new GameObject(name);
            body = new GameObject("Gövde").transform;
            body.SetParent(root.transform, false);
            return root;
        }

        static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        /// <summary>İki nokta arasında konik uzuv (kol, bacak, sopa).</summary>
        public static GameObject Limb(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material mat, float taper = 0.8f)
        {
            var dir = to - from;
            var go = Gen.MeshObj(parent, name, MeshGen.Cylinder(7, 0.5f, 0.5f * taper), mat, from, new Vector3(radius * 2f, dir.magnitude, radius * 2f));
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            return go;
        }

        static GameObject Ball(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, int variant = 0, Vector3 euler = default)
            => Gen.MeshObj(parent, name, MeshGen.Blob(variant), mat, pos, scale, euler);

        static void Eyes(Transform parent, float y, float z, float spread, float size, Material mat)
        {
            Part(PrimitiveType.Sphere, parent, new Vector3(spread, y, z), Vector3.one * size, mat);
            Part(PrimitiveType.Sphere, parent, new Vector3(-spread, y, z), Vector3.one * size, mat);
        }

        // ================= KAHRAMAN: genç şövalye =================
        public static GameObject Hero()
        {
            var root = Root("Kahraman", out var body);
            var rig = root.AddComponent<HeroRig>();
            var tunic = Art.Lit(new Color(0.20f, 0.36f, 0.72f), Art.ClothTex);
            var gold = Art.Lit(new Color(0.92f, 0.74f, 0.28f), null, 0.5f);
            var leather = Art.Lit(new Color(0.42f, 0.27f, 0.16f));
            var trousers = Art.Lit(new Color(0.30f, 0.24f, 0.22f));
            var skin = Art.Lit(Art.Skin);
            var dark = Art.Lit(new Color(0.15f, 0.12f, 0.10f));
            var cape = Art.Lit(new Color(0.72f, 0.16f, 0.18f), Art.ClothTex);
            var steel = Art.Lit(new Color(0.80f, 0.82f, 0.88f), null, 0.6f);
            var steelDark = Art.Lit(new Color(0.55f, 0.58f, 0.64f), null, 0.5f);
            var bootMat = Art.Glow(new Color(0.98f, 0.78f, 0.2f), 0.4f);
            var armorParts = new List<(GameObject, int)>();
            var bootParts = new List<GameObject>();

            // bacaklar (kalçadan döner) + deri çizme; zırhlı dizlik ve altın çizme bacağa bağlı
            Transform[] legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.085f : 0.085f;
                var leg = Pivot(body, i == 0 ? "Sol Bacak" : "Sağ Bacak", new Vector3(x, 0.36f, 0));
                legs[i] = leg;
                Limb(leg, "Bacak", Vector3.zero, new Vector3(x * 0.1f, -0.27f, 0), 0.06f, trousers);
                Ball(leg, "Çizme", new Vector3(x * 0.1f, -0.29f, 0.03f), new Vector3(0.14f, 0.14f, 0.2f), leather, 1);
                var greave = Gen.MeshObj(leg, "Dizlik", MeshGen.Cylinder(8, 0.5f, 0.42f), steel, new Vector3(x * 0.05f, -0.24f, 0.005f), new Vector3(0.15f, 0.15f, 0.15f));
                armorParts.Add((greave, 2));
                var gb = Gen.Group(leg, "Altın Çizme");
                Ball(gb, "Çizme", new Vector3(x * 0.1f, -0.275f, 0.03f), new Vector3(0.16f, 0.17f, 0.22f), bootMat, 2);
                Gen.MeshObj(gb, "Kanat", MeshGen.Cone(4), Art.Lit(Color.white), new Vector3(x * 0.9f, -0.22f, -0.04f), new Vector3(0.06f, 0.18f, 0.12f), new Vector3(0, 0, x > 0 ? -60 : 60));
                bootParts.Add(gb.gameObject);
            }

            // gövde: tunik, tabard, kemer, pelerin
            Ball(body, "Tunik", new Vector3(0, 0.53f, 0), new Vector3(0.37f, 0.4f, 0.29f), tunic, 4);
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.47f, 0.13f), new Vector3(0.2f, 0.32f, 0.03f), Art.Lit(new Color(0.95f, 0.92f, 0.82f), Art.ClothTex));
            Gen.MeshObj(body, "Arma", MeshGen.Disc(6), Art.Lit(new Color(0.20f, 0.36f, 0.72f)), new Vector3(0, 0.52f, 0.147f), new Vector3(0.1f, 1f, 0.12f), new Vector3(-90, 0, 0));
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.36f, 0), new Vector3(0.36f, 0.05f, 0.29f), leather);
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.36f, 0.15f), new Vector3(0.06f, 0.06f, 0.02f), gold);
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.6f, -0.15f), new Vector3(0.34f, 0.24f, 0.03f), cape, new Vector3(-6, 0, 0));
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.36f, -0.19f), new Vector3(0.40f, 0.3f, 0.03f), cape, new Vector3(-14, 0, 0));
            Ball(body, "Yaka", new Vector3(0, 0.72f, -0.02f), new Vector3(0.3f, 0.08f, 0.26f), cape, 3);

            // ---- ZIRH 1: göğüslük + üstünde armalı tabard + etek plakaları ----
            var cuirass = Gen.Group(body, "Göğüslük");
            Ball(cuirass, "Plaka", new Vector3(0, 0.54f, 0.005f), new Vector3(0.40f, 0.38f, 0.32f), steel, 5);
            Part(PrimitiveType.Cube, cuirass, new Vector3(0, 0.55f, 0.155f), new Vector3(0.018f, 0.22f, 0.02f), steelDark); // orta sırt çizgisi
            Gen.MeshObj(cuirass, "Etek", MeshGen.Tower(10), steelDark, new Vector3(0, 0.3f, 0), new Vector3(0.39f, 0.09f, 0.32f));
            Part(PrimitiveType.Cube, cuirass, new Vector3(0, 0.49f, 0.172f), new Vector3(0.18f, 0.3f, 0.02f), tunic);           // tabard (zırhın üstünde)
            Gen.MeshObj(cuirass, "Arma", MeshGen.Disc(6), gold, new Vector3(0, 0.53f, 0.183f), new Vector3(0.09f, 1f, 0.11f), new Vector3(-90, 0, 0));
            armorParts.Add((cuirass.gameObject, 1));
            // ---- ZIRH 2: altın kenarlar ----
            var trim = Gen.Group(body, "Altın Süs");
            Gen.MeshObj(trim, "Kemer", MeshGen.Tower(10), gold, new Vector3(0, 0.37f, 0), new Vector3(0.41f, 0.035f, 0.34f));
            Gen.MeshObj(trim, "Yaka", MeshGen.Tower(10), gold, new Vector3(0, 0.7f, 0), new Vector3(0.3f, 0.03f, 0.26f));
            armorParts.Add((trim.gameObject, 2));

            // sol kol (omuzdan döner); omuzluk, bilezik ve kalkan bu kola bağlı
            var armL = Pivot(body, "Sol Kol", new Vector3(-0.2f, 0.68f, 0));
            Limb(armL, "Kol", Vector3.zero, new Vector3(-0.05f, -0.24f, 0.04f), 0.05f, tunic);
            Part(PrimitiveType.Sphere, armL, new Vector3(-0.05f, -0.26f, 0.05f), Vector3.one * 0.08f, leather);
            armorParts.Add((Pauldron(armL, -1f, steel, steelDark), 1));
            armorParts.Add((Gen.MeshObj(armL, "Bilezik", MeshGen.Cylinder(8, 0.5f, 0.5f), steel, new Vector3(-0.04f, -0.22f, 0.035f), new Vector3(0.12f, 0.08f, 0.12f), new Vector3(10, 0, -12)), 2));
            var shield = Gen.Group(armL, "Kalkan");
            shield.localPosition = new Vector3(-0.09f, -0.2f, 0.06f);
            shield.localRotation = Quaternion.Euler(0, -75, 0);
            Ball(shield, "Kenar", Vector3.zero, new Vector3(0.32f, 0.38f, 0.05f), gold, 1);
            Ball(shield, "Yüz", new Vector3(0, 0, 0.012f), new Vector3(0.28f, 0.34f, 0.05f), Art.Lit(new Color(0.20f, 0.36f, 0.72f)), 2);
            Gen.MeshObj(shield, "Arma", MeshGen.Disc(6), gold, new Vector3(0, 0.02f, 0.04f), new Vector3(0.1f, 1, 0.12f), new Vector3(-90, 0, 0));
            armorParts.Add((shield.gameObject, 2));

            // kafa (boyundan döner); miğfer kafaya bağlı, yüzü kapatmaz
            var head = Pivot(body, "Kafa", new Vector3(0, 0.84f, 0));
            Part(PrimitiveType.Sphere, head, new Vector3(0, 0.08f, 0), Vector3.one * 0.32f, skin);
            Ball(head, "Saç", new Vector3(0, 0.17f, -0.04f), new Vector3(0.35f, 0.22f, 0.34f), Art.Lit(new Color(0.55f, 0.34f, 0.16f)), 3);
            Ball(head, "Perçem", new Vector3(0.07f, 0.16f, 0.12f), new Vector3(0.16f, 0.07f, 0.1f), Art.Lit(new Color(0.55f, 0.34f, 0.16f)), 2, new Vector3(0, 0, -20));
            Eyes(head, 0.09f, 0.145f, 0.06f, 0.045f, dark);
            Part(PrimitiveType.Sphere, head, new Vector3(0, 0.05f, 0.16f), Vector3.one * 0.04f, Art.Lit(Art.Skin * 0.95f));
            var helm = Gen.Group(head, "Miğfer");
            Part(PrimitiveType.Sphere, helm, new Vector3(0, 0.19f, -0.01f), new Vector3(0.35f, 0.2f, 0.35f), steel);
            Gen.MeshObj(helm, "Siperlik", MeshGen.Tower(12), steelDark, new Vector3(0, 0.125f, -0.01f), new Vector3(0.37f, 0.04f, 0.37f));
            Part(PrimitiveType.Cube, helm, new Vector3(0, 0.1f, 0.172f), new Vector3(0.028f, 0.09f, 0.02f), steel);           // burun koruması
            Part(PrimitiveType.Cube, helm, new Vector3(0.155f, 0.06f, 0.03f), new Vector3(0.025f, 0.11f, 0.1f), steel);        // yanak
            Part(PrimitiveType.Cube, helm, new Vector3(-0.155f, 0.06f, 0.03f), new Vector3(0.025f, 0.11f, 0.1f), steel);
            Gen.MeshObj(helm, "Sorguç", MeshGen.Cone(5), Art.Lit(new Color(0.85f, 0.2f, 0.2f)), new Vector3(0, 0.28f, -0.03f), new Vector3(0.07f, 0.2f, 0.07f), new Vector3(-25, 0, 0));
            armorParts.Add((helm.gameObject, 1));

            // sağ kol + kılıç (omuzdan birlikte döner)
            var weapon = Pivot(body, "Kılıç Kolu", new Vector3(0.2f, 0.68f, 0));
            Limb(weapon, "Kol", Vector3.zero, new Vector3(0.05f, -0.22f, 0.07f), 0.05f, tunic);
            Part(PrimitiveType.Sphere, weapon, new Vector3(0.05f, -0.24f, 0.08f), Vector3.one * 0.08f, leather);
            armorParts.Add((Pauldron(weapon, 1f, steel, steelDark), 1));
            armorParts.Add((Gen.MeshObj(weapon, "Bilezik", MeshGen.Cylinder(8, 0.5f, 0.5f), steel, new Vector3(0.04f, -0.2f, 0.06f), new Vector3(0.12f, 0.08f, 0.12f), new Vector3(-15, 0, 12)), 2));
            var sword = Pivot(weapon, "Kılıç", new Vector3(0.05f, -0.24f, 0.08f));
            sword.localRotation = Quaternion.Euler(35, 0, 0);
            var blade = Part(PrimitiveType.Cube, sword, new Vector3(0, 0.3f, 0), new Vector3(0.055f, 0.5f, 0.02f), Art.Lit(new Color(0.85f, 0.88f, 0.92f), null, 0.6f));
            Part(PrimitiveType.Cube, sword, new Vector3(0, 0.05f, 0), new Vector3(0.2f, 0.035f, 0.05f), gold);
            Part(PrimitiveType.Cube, sword, new Vector3(0, -0.02f, 0), new Vector3(0.035f, 0.09f, 0.035f), leather);
            Part(PrimitiveType.Sphere, sword, new Vector3(0, -0.07f, 0), Vector3.one * 0.05f, gold);
            var bladeBase = Pivot(sword, "Kabza Ucu", new Vector3(0, 0.06f, 0));
            var tip = Pivot(sword, "Uç", new Vector3(0, 0.55f, 0));

            // ayak altı halka (kahramanı kalabalıkta bulmak kolay olsun)
            var ring = Gen.MeshObj(root.transform, "Halka", MeshGen.Disc(20), Art.Transparent(new Color(0.3f, 0.6f, 1f, 0.45f)), new Vector3(0, 0.02f, 0), new Vector3(0.85f, 1, 0.85f));
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.AddComponent<DiscPulse>();

            var anim = root.AddComponent<UnitAnim>();
            anim.Init(body, weapon);
            anim.SetRig(legs[0], legs[1], armL, head);
            anim.StepRate = 10.5f; anim.LegSwing = 34f; anim.ArmSwing = 26f; anim.Bob = 0.045f; anim.Lean = 7f; anim.Roll = 3f;
            anim.Fidget = FidgetKind.Hero;
            rig.Init(anim, blade.GetComponent<MeshRenderer>(), bladeBase, tip);
            foreach (var (go, tier) in armorParts) rig.AddArmorPart(go, tier);
            foreach (var b in bootParts) rig.AddBootPart(b);
            return root;
        }

        /// <summary>Katmanlı omuz zırhı (kolun omuz pivotuna bağlı).</summary>
        static GameObject Pauldron(Transform arm, float side, Material steel, Material steelDark)
        {
            var p = Gen.Group(arm, "Omuzluk");
            p.localPosition = new Vector3(side * 0.02f, 0.02f, 0);
            Part(PrimitiveType.Sphere, p, Vector3.zero, new Vector3(0.21f, 0.13f, 0.22f), steel, new Vector3(0, 0, side * -18f));
            Part(PrimitiveType.Sphere, p, new Vector3(side * 0.03f, -0.06f, 0), new Vector3(0.17f, 0.09f, 0.19f), steelDark, new Vector3(0, 0, side * -28f));
            Part(PrimitiveType.Sphere, p, new Vector3(side * 0.05f, -0.11f, 0), new Vector3(0.13f, 0.07f, 0.15f), steel, new Vector3(0, 0, side * -36f));
            return p.gameObject;
        }

        /// <summary>Yerleştirme sırasında kahramanın başlangıç noktasını gösteren model.</summary>
        static GameObject HeroMarker()
        {
            var go = Hero();
            var rig = go.GetComponent<HeroRig>(); if (rig) rig.enabled = false;
            var arrow = Gen.MeshObj(go.transform, "Ok", MeshGen.Cone(4), Art.Glow(new Color(0.3f, 0.6f, 1f), 1.2f), new Vector3(0, 1.75f, 0), new Vector3(0.25f, 0.3f, 0.25f), new Vector3(180, 0, 0));
            arrow.AddComponent<Bobber>();
            return go;
        }

        // ================= GOBLİN: kambur, sivri kulaklı yağmacı =================
        static GameObject Goblin()
        {
            var root = Root("Goblin", out var body);
            var skin = Art.Lit(new Color(0.48f, 0.68f, 0.30f));
            var skinDark = Art.Lit(new Color(0.36f, 0.52f, 0.22f));
            var rag = Art.Lit(new Color(0.48f, 0.34f, 0.20f), Art.ClothTex);
            var rust = Art.Lit(new Color(0.55f, 0.42f, 0.32f), null, 0.35f);
            var eye = Art.Glow(new Color(1f, 0.85f, 0.2f), 1.2f);

            Transform[] legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.07f : 0.07f;
                var leg = Pivot(body, i == 0 ? "Sol Bacak" : "Sağ Bacak", new Vector3(x, 0.24f, 0));
                legs[i] = leg;
                Limb(leg, "Bacak", Vector3.zero, new Vector3(x * 0.3f, -0.19f, 0.02f), 0.045f, skinDark);
                Ball(leg, "Ayak", new Vector3(x * 0.3f, -0.2f, 0.05f), new Vector3(0.1f, 0.06f, 0.16f), skin, 1);
            }
            Ball(body, "Gövde", new Vector3(0, 0.34f, -0.02f), new Vector3(0.32f, 0.3f, 0.3f), rag, 2, new Vector3(18, 0, 0));
            Ball(body, "Göbek", new Vector3(0, 0.3f, 0.06f), new Vector3(0.22f, 0.2f, 0.18f), skin, 3);
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.22f, 0.02f), new Vector3(0.3f, 0.05f, 0.27f), Art.Lit(new Color(0.30f, 0.20f, 0.12f)));

            var head = Pivot(body, "Kafa", new Vector3(0, 0.52f, 0.05f));
            Ball(head, "Kafa", new Vector3(0, 0.08f, 0.01f), new Vector3(0.34f, 0.3f, 0.32f), skin, 5);
            Gen.MeshObj(head, "Kulak", MeshGen.Cone(4), skin, new Vector3(0.15f, 0.1f, -0.01f), new Vector3(0.1f, 0.34f, 0.05f), new Vector3(0, 10, -78));
            Gen.MeshObj(head, "Kulak", MeshGen.Cone(4), skin, new Vector3(-0.15f, 0.1f, -0.01f), new Vector3(0.1f, 0.34f, 0.05f), new Vector3(0, -10, 78));
            Gen.MeshObj(head, "Burun", MeshGen.Cone(5), skinDark, new Vector3(0, 0.07f, 0.16f), new Vector3(0.07f, 0.13f, 0.07f), new Vector3(80, 0, 0));
            Eyes(head, 0.12f, 0.14f, 0.075f, 0.065f, eye);
            Part(PrimitiveType.Cube, head, new Vector3(0, 0f, 0.14f), new Vector3(0.14f, 0.03f, 0.03f), Art.Lit(new Color(0.2f, 0.1f, 0.08f)));
            Gen.MeshObj(head, "Diş", MeshGen.Cone(4), Art.Lit(Color.white), new Vector3(0.04f, 0f, 0.15f), new Vector3(0.025f, 0.05f, 0.025f));
            Gen.MeshObj(head, "Diş", MeshGen.Cone(4), Art.Lit(Color.white), new Vector3(-0.04f, 0f, 0.15f), new Vector3(0.025f, 0.05f, 0.025f));
            Part(PrimitiveType.Sphere, head, new Vector3(0, 0.16f, 0f), new Vector3(0.3f, 0.18f, 0.3f), rust);
            Gen.MeshObj(head, "Tepe", MeshGen.Cone(5), rust, new Vector3(0, 0.23f, 0f), new Vector3(0.06f, 0.1f, 0.06f));

            var armL = Pivot(body, "Sol Kol", new Vector3(-0.15f, 0.42f, 0.02f));
            Limb(armL, "Kol", Vector3.zero, new Vector3(-0.05f, -0.2f, 0.06f), 0.04f, skin);
            Part(PrimitiveType.Sphere, armL, new Vector3(-0.05f, -0.22f, 0.07f), Vector3.one * 0.07f, skin);
            var pad = Gen.Group(armL, "Omuzluk");
            Ball(pad, "Plaka", new Vector3(0, 0.03f, 0), new Vector3(0.16f, 0.09f, 0.16f), rust, 4);
            for (int i = 0; i < 3; i++)
                Gen.MeshObj(pad, "Diken", MeshGen.Cone(4), Art.Lit(new Color(0.85f, 0.82f, 0.75f)), new Vector3(0.03f - i * 0.03f, 0.07f, -0.04f + i * 0.04f), new Vector3(0.035f, 0.08f, 0.035f), new Vector3(0, 0, 25));

            var weapon = Pivot(body, "Pala Kolu", new Vector3(0.15f, 0.42f, 0.02f));
            Limb(weapon, "Kol", Vector3.zero, new Vector3(0.05f, -0.18f, 0.08f), 0.04f, skin);
            Part(PrimitiveType.Sphere, weapon, new Vector3(0.05f, -0.2f, 0.09f), Vector3.one * 0.07f, skin);
            var blade = Pivot(weapon, "Pala", new Vector3(0.05f, -0.2f, 0.09f));
            blade.localRotation = Quaternion.Euler(40, 0, 0);
            Part(PrimitiveType.Cube, blade, new Vector3(0, 0.13f, 0), new Vector3(0.06f, 0.2f, 0.02f), rust);
            Part(PrimitiveType.Cube, blade, new Vector3(0, 0.27f, 0.025f), new Vector3(0.07f, 0.12f, 0.02f), rust, new Vector3(25, 0, 0));
            Part(PrimitiveType.Cube, blade, new Vector3(0, 0.0f, 0), new Vector3(0.03f, 0.08f, 0.03f), Art.Lit(new Color(0.3f, 0.2f, 0.12f)));

            var anim = root.AddComponent<UnitAnim>();
            anim.Init(body, weapon);
            anim.SetRig(legs[0], legs[1], armL, head);
            // kısa adımlı, sinsi koşuş
            anim.StepRate = 15f; anim.LegSwing = 42f; anim.ArmSwing = 30f; anim.Bob = 0.05f; anim.Lean = 14f; anim.Roll = 6f;
            anim.Styles = new[] { AttackStyle.QuickStab, AttackStyle.QuickSlash, AttackStyle.QuickStab };
            anim.Fidget = FidgetKind.Goblin;
            return root;
        }

        // ================= OKÇU: kukuletalı haydut, gözcü kulesinde =================
        static GameObject Archer()
        {
            var root = Root("Okçu", out var body);
            var wood = Art.Lit(Art.WoodLight, Art.WoodTex);
            var woodDark = Art.Lit(Art.Wood, Art.BarkTex);
            var cloak = Art.Lit(new Color(0.24f, 0.32f, 0.22f), Art.ClothTex);
            var cloakDark = Art.Lit(new Color(0.16f, 0.22f, 0.15f), Art.ClothTex);
            var leather = Art.Lit(new Color(0.45f, 0.30f, 0.18f));

            float top = 0.42f;
            foreach (var x in new[] { -0.3f, 0.3f })
                foreach (var z in new[] { -0.3f, 0.3f })
                    Limb(root.transform, "Direk", new Vector3(x * 1.1f, 0, z * 1.1f), new Vector3(x, top, z), 0.04f, woodDark, 0.9f);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0, top, 0), new Vector3(0.74f, 0.05f, 0.74f), wood);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0, top + 0.16f, 0.35f), new Vector3(0.72f, 0.04f, 0.04f), woodDark);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0.35f, top + 0.16f, 0), new Vector3(0.04f, 0.04f, 0.72f), woodDark);
            Part(PrimitiveType.Cube, root.transform, new Vector3(-0.35f, top + 0.16f, 0), new Vector3(0.04f, 0.04f, 0.72f), woodDark);
            Part(PrimitiveType.Cube, root.transform, new Vector3(-0.32f, top + 0.45f, -0.32f), new Vector3(0.03f, 0.9f, 0.03f), woodDark);
            var pennant = Pivot(root.transform, "Flama", new Vector3(-0.32f, top + 0.82f, -0.32f));
            Part(PrimitiveType.Cube, pennant, new Vector3(0.12f, 0, 0), new Vector3(0.24f, 0.1f, 0.01f), Art.Lit(new Color(0.7f, 0.18f, 0.18f), Art.ClothTex));
            pennant.gameObject.AddComponent<Waver>();

            body.localPosition = new Vector3(0, top, 0);
            Gen.MeshObj(body, "Pelerin", MeshGen.Cone(8), cloakDark, new Vector3(0, 0.02f, -0.02f), new Vector3(0.44f, 0.62f, 0.4f));
            Ball(body, "Gövde", new Vector3(0, 0.4f, 0.02f), new Vector3(0.28f, 0.3f, 0.24f), leather, 2);
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.33f, 0.03f), new Vector3(0.28f, 0.04f, 0.25f), Art.Lit(new Color(0.25f, 0.16f, 0.1f)));
            var quiver = Pivot(body, "Ok Kılıfı", new Vector3(0.08f, 0.48f, -0.14f));
            quiver.localRotation = Quaternion.Euler(-15, 0, -20);
            Gen.MeshObj(quiver, "Kılıf", MeshGen.Cylinder(6, 0.5f, 0.5f), leather, Vector3.zero, new Vector3(0.09f, 0.28f, 0.09f));
            for (int i = 0; i < 3; i++)
            {
                Part(PrimitiveType.Cube, quiver, new Vector3((i - 1) * 0.02f, 0.33f, 0), new Vector3(0.012f, 0.12f, 0.012f), woodDark);
                Part(PrimitiveType.Cube, quiver, new Vector3((i - 1) * 0.02f, 0.39f, 0), new Vector3(0.03f, 0.05f, 0.005f), Art.Lit(Color.white));
            }
            var head = Pivot(body, "Kafa", new Vector3(0, 0.6f, 0));
            Part(PrimitiveType.Sphere, head, new Vector3(0, 0.1f, 0), Vector3.one * 0.26f, Art.Lit(new Color(0.12f, 0.10f, 0.09f)));
            Gen.MeshObj(head, "Kukuleta", MeshGen.Cone(7), cloak, new Vector3(0, 0f, -0.04f), new Vector3(0.34f, 0.42f, 0.34f), new Vector3(-12, 0, 0));
            Eyes(head, 0.11f, 0.11f, 0.045f, 0.04f, Art.Glow(new Color(1f, 0.35f, 0.2f), 1.5f));
            Ball(body, "Omuz", new Vector3(0, 0.56f, -0.02f), new Vector3(0.36f, 0.1f, 0.3f), cloak, 3);
            var armL = Pivot(body, "Sol Kol", new Vector3(-0.15f, 0.52f, 0));
            Limb(armL, "Kol", Vector3.zero, new Vector3(0.03f, -0.1f, 0.18f), 0.04f, cloak);
            var weapon = Pivot(body, "Yay", new Vector3(0.12f, 0.5f, 0.14f));
            Limb(weapon, "Kol", new Vector3(0.03f, 0.02f, -0.14f), Vector3.zero, 0.04f, cloak);
            var bowMat = Art.Lit(new Color(0.42f, 0.26f, 0.14f));
            Part(PrimitiveType.Cube, weapon, new Vector3(0, 0, 0.04f), new Vector3(0.03f, 0.2f, 0.03f), bowMat);
            Part(PrimitiveType.Cube, weapon, new Vector3(0, 0.17f, 0.01f), new Vector3(0.025f, 0.18f, 0.025f), bowMat, new Vector3(-22, 0, 0));
            Part(PrimitiveType.Cube, weapon, new Vector3(0, -0.17f, 0.01f), new Vector3(0.025f, 0.18f, 0.025f), bowMat, new Vector3(22, 0, 0));
            Part(PrimitiveType.Cube, weapon, new Vector3(0, 0, -0.03f), new Vector3(0.006f, 0.5f, 0.006f), Art.Lit(new Color(0.95f, 0.92f, 0.85f)));
            var anim = root.AddComponent<UnitAnim>();
            anim.Init(body, weapon);
            anim.SetRig(null, null, armL, head);
            anim.Lean = 0f; anim.Bob = 0f; anim.Roll = 0f; anim.ArmSwing = 4f;
            anim.Styles = new[] { AttackStyle.BowShot };
            anim.Fidget = FidgetKind.Archer;
            return root;
        }

        // ================= DEV: dişli, boynuzlu ogre savaş beyi =================
        static GameObject Ogre()
        {
            var root = Root("Dev", out var body);
            var skin = Art.Lit(new Color(0.55f, 0.48f, 0.66f));
            var skinDark = Art.Lit(new Color(0.42f, 0.36f, 0.52f));
            var fur = Art.Lit(new Color(0.48f, 0.33f, 0.20f), Art.ClothTex);
            var iron = Art.Lit(new Color(0.42f, 0.42f, 0.45f), null, 0.45f);
            var bone = Art.Lit(new Color(0.95f, 0.90f, 0.78f));

            Transform[] legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.16f : 0.16f;
                var leg = Pivot(body, i == 0 ? "Sol Bacak" : "Sağ Bacak", new Vector3(x, 0.42f, 0));
                legs[i] = leg;
                Limb(leg, "Bacak", Vector3.zero, new Vector3(x * 0.1f, -0.33f, 0.02f), 0.11f, skinDark, 0.85f);
                Ball(leg, "Ayak", new Vector3(x * 0.1f, -0.36f, 0.06f), new Vector3(0.22f, 0.12f, 0.28f), skinDark, 1);
            }
            Ball(body, "Göbek", new Vector3(0, 0.62f, 0.04f), new Vector3(0.74f, 0.7f, 0.62f), skin, 2);
            Ball(body, "Göğüs", new Vector3(0, 0.88f, -0.02f), new Vector3(0.66f, 0.38f, 0.5f), skin, 3);
            Ball(body, "Kürk Peştamal", new Vector3(0, 0.38f, 0.02f), new Vector3(0.66f, 0.26f, 0.56f), fur, 4);
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.46f, 0.02f), new Vector3(0.62f, 0.07f, 0.54f), Art.Lit(new Color(0.3f, 0.2f, 0.12f)));
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.46f, 0.29f), new Vector3(0.12f, 0.1f, 0.03f), iron);

            var head = Pivot(body, "Kafa", new Vector3(0, 1.04f, 0.1f));
            Ball(head, "Kafa", new Vector3(0, 0.08f, 0.02f), new Vector3(0.32f, 0.28f, 0.3f), skin, 0);
            Ball(head, "Çene", new Vector3(0, 0f, 0.08f), new Vector3(0.3f, 0.16f, 0.24f), skinDark, 1);
            Gen.MeshObj(head, "Diş", MeshGen.Cone(5), bone, new Vector3(0.08f, 0.02f, 0.18f), new Vector3(0.05f, 0.11f, 0.05f));
            Gen.MeshObj(head, "Diş", MeshGen.Cone(5), bone, new Vector3(-0.08f, 0.02f, 0.18f), new Vector3(0.05f, 0.11f, 0.05f));
            Eyes(head, 0.12f, 0.15f, 0.07f, 0.06f, Art.Glow(new Color(1f, 0.3f, 0.15f), 1.4f));
            Part(PrimitiveType.Cube, head, new Vector3(0, 0.17f, 0.14f), new Vector3(0.22f, 0.04f, 0.04f), skinDark);
            Gen.MeshObj(head, "Boynuz", MeshGen.Cone(6), bone, new Vector3(0.13f, 0.18f, 0f), new Vector3(0.07f, 0.22f, 0.07f), new Vector3(-20, 0, -40));
            Gen.MeshObj(head, "Boynuz", MeshGen.Cone(6), bone, new Vector3(-0.13f, 0.18f, 0f), new Vector3(0.07f, 0.22f, 0.07f), new Vector3(-20, 0, 40));

            var armL = Pivot(body, "Sol Kol", new Vector3(-0.34f, 0.96f, 0));
            Limb(armL, "Kol", Vector3.zero, new Vector3(-0.08f, -0.4f, 0.08f), 0.1f, skin, 0.8f);
            Ball(armL, "El", new Vector3(-0.09f, -0.44f, 0.09f), Vector3.one * 0.16f, skinDark, 2);
            Part(PrimitiveType.Cube, armL, new Vector3(-0.06f, -0.3f, 0.06f), new Vector3(0.18f, 0.1f, 0.18f), Art.Lit(new Color(0.35f, 0.22f, 0.12f)), new Vector3(0, 0, 10));

            var weapon = Pivot(body, "Sopa Kolu", new Vector3(0.36f, 0.94f, 0.02f));
            Limb(weapon, "Kol", Vector3.zero, new Vector3(0.08f, -0.36f, 0.12f), 0.1f, skin, 0.8f);
            Ball(weapon, "El", new Vector3(0.08f, -0.4f, 0.13f), Vector3.one * 0.16f, skinDark, 3);
            var shoulder = Gen.Group(weapon, "Omuzluk");
            Ball(shoulder, "Plaka", new Vector3(-0.02f, 0.06f, -0.02f), new Vector3(0.3f, 0.18f, 0.3f), iron, 5);
            for (int i = 0; i < 3; i++)
                Gen.MeshObj(shoulder, "Diken", MeshGen.Cone(5), bone, new Vector3(-0.02f + (i - 1) * 0.08f, 0.14f, -0.02f), new Vector3(0.05f, 0.13f, 0.05f), new Vector3(0, 0, -10 + (i - 1) * -15));
            var club = Pivot(weapon, "Sopa", new Vector3(0.08f, -0.4f, 0.13f));
            club.localRotation = Quaternion.Euler(30, 0, 0);
            var log = Art.Lit(Art.Wood, Art.BarkTex);
            Gen.MeshObj(club, "Sap", MeshGen.Cylinder(7, 0.05f, 0.11f), log, new Vector3(0, -0.06f, 0), new Vector3(1, 0.62f, 1));
            Ball(club, "Baş", new Vector3(0, 0.56f, 0), new Vector3(0.24f, 0.26f, 0.24f), log, 4);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f;
                var q = Quaternion.Euler(0, a, 0);
                Gen.MeshObj(club, "Çivi", MeshGen.Cone(4), iron, new Vector3(0, 0.52f + (i % 2) * 0.08f, 0) + q * new Vector3(0.1f, 0, 0), new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0, a, -90));
            }
            var anim = root.AddComponent<UnitAnim>();
            anim.Init(body, weapon);
            anim.SetRig(legs[0], legs[1], armL, head);
            // ağır, yalpalayan yürüyüş
            anim.StepRate = 6.5f; anim.LegSwing = 24f; anim.ArmSwing = 16f; anim.Bob = 0.07f; anim.Lean = 5f; anim.Roll = 7f;
            anim.Styles = new[] { AttackStyle.HeavySmash };
            anim.Fidget = FidgetKind.Ogre;
            return root;
        }

        static GameObject Spikes()
        {
            var root = new GameObject("Diken Tuzağı");
            var wood = Art.Lit(Art.Wood, Art.BarkTex);
            // ahşap çerçeve, paslı demir şeritler, sivri kazıklar
            Part(PrimitiveType.Cube, root.transform, new Vector3(0, 0.025f, 0), new Vector3(0.78f, 0.05f, 0.78f), Art.Lit(new Color(0.35f, 0.26f, 0.18f)));
            foreach (var z in new[] { -0.36f, 0.36f })
                Part(PrimitiveType.Cube, root.transform, new Vector3(0, 0.05f, z), new Vector3(0.82f, 0.06f, 0.08f), wood);
            foreach (var x in new[] { -0.36f, 0.36f })
                Part(PrimitiveType.Cube, root.transform, new Vector3(x, 0.05f, 0), new Vector3(0.08f, 0.06f, 0.82f), wood);
            var metal = Art.Lit(new Color(0.62f, 0.60f, 0.62f), null, 0.5f);
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    Gen.MeshObj(root.transform, "Diken", MeshGen.Cone(4), metal, new Vector3(x * 0.2f, 0.05f, z * 0.2f), new Vector3(0.1f, 0.3f, 0.1f), new Vector3((x + z) * 4f, 0, 0));
            return root;
        }

        // ================= DESTEK: hazine sandıkları =================
        static GameObject Crate(Color band, string kind)
        {
            var root = new GameObject(kind == "sword" ? "Kılıç Sandığı" : kind == "armor" ? "Zırh Sandığı" : "Çizme Sandığı");
            var t = root.transform;
            var glow = Gen.MeshObj(t, "Işıltı", MeshGen.Disc(20), Art.Transparent(new Color(band.r, band.g, band.b, 0.35f)), new Vector3(0, 0.02f, 0), new Vector3(0.9f, 1, 0.9f));
            glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.AddComponent<DiscPulse>();
            var box = Pivot(t, "Sandık", Vector3.zero);
            box.localRotation = Quaternion.Euler(0, 20, 0);
            var wood = Art.Lit(Art.WoodLight, Art.WoodTex);
            var iron = Art.Lit(new Color(0.35f, 0.33f, 0.32f), null, 0.4f);
            var gold = Art.Lit(new Color(0.95f, 0.76f, 0.3f), null, 0.55f);
            Part(PrimitiveType.Cube, box, new Vector3(0, 0.15f, 0), new Vector3(0.5f, 0.3f, 0.34f), wood);
            Gen.MeshObj(box, "Kapak", MeshGen.HalfCylinder(8), wood, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.34f, 0.34f));
            foreach (var x in new[] { -0.17f, 0.17f })
            {
                Part(PrimitiveType.Cube, box, new Vector3(x, 0.15f, 0), new Vector3(0.05f, 0.31f, 0.35f), iron);
                Gen.MeshObj(box, "Şerit", MeshGen.HalfCylinder(8), iron, new Vector3(x, 0.3f, 0), new Vector3(0.05f, 0.36f, 0.36f));
            }
            Part(PrimitiveType.Cube, box, new Vector3(0, 0.27f, 0.175f), new Vector3(0.09f, 0.1f, 0.02f), gold);
            Part(PrimitiveType.Sphere, box, new Vector3(0, 0.16f, 0.175f), Vector3.one * 0.07f, Art.Glow(band, 1.4f));
            var icon = Pivot(t, "Simge", new Vector3(0, 0.82f, 0));
            switch (kind)
            {
                case "sword":
                    Part(PrimitiveType.Cube, icon, new Vector3(0, 0.05f, 0), new Vector3(0.07f, 0.42f, 0.03f), Art.Glow(new Color(1f, 0.6f, 0.2f), 1.2f), new Vector3(0, 0, 25));
                    Part(PrimitiveType.Cube, icon, new Vector3(0.07f, -0.13f, 0), new Vector3(0.2f, 0.04f, 0.05f), gold, new Vector3(0, 0, 25));
                    break;
                case "armor":
                    Ball(icon, "Göğüslük", Vector3.zero, new Vector3(0.3f, 0.3f, 0.16f), Art.Lit(new Color(0.78f, 0.82f, 0.9f), null, 0.6f), 5);
                    Ball(icon, "Omuz", new Vector3(0.17f, 0.12f, 0), new Vector3(0.14f, 0.1f, 0.14f), Art.Lit(new Color(0.78f, 0.82f, 0.9f), null, 0.6f), 1);
                    Ball(icon, "Omuz", new Vector3(-0.17f, 0.12f, 0), new Vector3(0.14f, 0.1f, 0.14f), Art.Lit(new Color(0.78f, 0.82f, 0.9f), null, 0.6f), 2);
                    break;
                case "boots":
                    var bm = Art.Glow(band, 0.5f);
                    Ball(icon, "Çizme", new Vector3(-0.06f, 0, 0), new Vector3(0.12f, 0.24f, 0.13f), bm, 1);
                    Ball(icon, "Burun", new Vector3(0.0f, -0.09f, 0), new Vector3(0.2f, 0.08f, 0.13f), bm, 2);
                    Gen.MeshObj(icon, "Kanat", MeshGen.Cone(4), Art.Lit(Color.white), new Vector3(-0.16f, 0.04f, 0), new Vector3(0.06f, 0.18f, 0.1f), new Vector3(0, 0, 60));
                    break;
            }
            icon.gameObject.AddComponent<Bobber>();
            return root;
        }

        static GameObject Potion()
        {
            var root = new GameObject("İksir");
            var t = root.transform;
            var c = IconColor(ItemType.Potion);
            var glow = Gen.MeshObj(t, "Işıltı", MeshGen.Disc(20), Art.Transparent(new Color(c.r, c.g, c.b, 0.35f)), new Vector3(0, 0.02f, 0), new Vector3(0.8f, 1, 0.8f));
            glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.AddComponent<DiscPulse>();
            var bottle = Pivot(t, "Şişe", Vector3.zero);
            // simyacı şişesi: yuvarlak gövde, uzun boyun, mantar tıpa, ip
            Part(PrimitiveType.Sphere, bottle, new Vector3(0, 0.24f, 0), Vector3.one * 0.38f, Art.Glow(c, 0.9f));
            Part(PrimitiveType.Sphere, bottle, new Vector3(0, 0.26f, 0), Vector3.one * 0.41f, Art.Transparent(new Color(0.85f, 0.95f, 1f, 0.25f)));
            Gen.MeshObj(bottle, "Boyun", MeshGen.Cylinder(8, 0.5f, 0.42f), Art.Transparent(new Color(0.85f, 0.95f, 1f, 0.45f)), new Vector3(0, 0.42f, 0), new Vector3(0.12f, 0.14f, 0.12f));
            Gen.MeshObj(bottle, "Tıpa", MeshGen.Cylinder(6, 0.5f, 0.45f), Art.Lit(new Color(0.62f, 0.45f, 0.28f)), new Vector3(0, 0.55f, 0), new Vector3(0.1f, 0.08f, 0.1f));
            Gen.MeshObj(bottle, "İp", MeshGen.Cylinder(8, 0.5f, 0.5f), Art.Lit(new Color(0.85f, 0.75f, 0.5f)), new Vector3(0, 0.47f, 0), new Vector3(0.13f, 0.02f, 0.13f));
            Part(PrimitiveType.Sphere, bottle, new Vector3(0.08f, 0.32f, 0.12f), Vector3.one * 0.07f, Art.Lit(Color.white, null, 0.9f));
            bottle.gameObject.AddComponent<Bobber>();
            return root;
        }

        static GameObject Bomb()
        {
            var root = new GameObject("Bomba");
            var t = Pivot(root.transform, "Bomba", Vector3.zero);
            var iron = Art.Lit(new Color(0.18f, 0.18f, 0.22f), null, 0.4f);
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.25f, 0), Vector3.one * 0.44f, iron);
            Gen.MeshObj(t, "Kuşak", MeshGen.Cylinder(10, 0.5f, 0.5f), Art.Lit(new Color(0.55f, 0.45f, 0.3f), null, 0.4f), new Vector3(0, 0.24f, 0), new Vector3(0.45f, 0.04f, 0.45f));
            Gen.MeshObj(t, "Ağız", MeshGen.Cylinder(6, 0.5f, 0.5f), iron, new Vector3(0, 0.44f, 0), new Vector3(0.12f, 0.06f, 0.12f));
            Part(PrimitiveType.Cylinder, t, new Vector3(0.04f, 0.53f, 0), new Vector3(0.025f, 0.06f, 0.025f), Art.Lit(new Color(0.8f, 0.7f, 0.5f)), new Vector3(0, 0, -25));
            Part(PrimitiveType.Sphere, t, new Vector3(0.08f, 0.6f, 0), Vector3.one * 0.08f, Art.Glow(new Color(1f, 0.6f, 0.1f), 3f));
            t.gameObject.AddComponent<Bobber>();
            return root;
        }

        static GameObject Flag()
        {
            var root = new GameObject("Sancak");
            var t = root.transform;
            var glow = Gen.MeshObj(t, "Işıltı", MeshGen.Disc(20), Art.Transparent(new Color(0.2f, 0.8f, 0.85f, 0.35f)), new Vector3(0, 0.02f, 0), new Vector3(0.85f, 1, 0.85f));
            glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.AddComponent<DiscPulse>();
            var wood = Art.Lit(Art.Wood, Art.BarkTex);
            Gen.MeshObj(t, "Taş", MeshGen.Rock(2), Art.Lit(Art.Stone, Art.StoneTex), new Vector3(0, 0.04f, 0), new Vector3(0.3f, 0.15f, 0.3f));
            Gen.MeshObj(t, "Direk", MeshGen.Cylinder(6, 0.5f, 0.4f), wood, Vector3.zero, new Vector3(0.05f, 1.05f, 0.05f));
            Part(PrimitiveType.Sphere, t, new Vector3(0, 1.07f, 0), Vector3.one * 0.07f, Art.Lit(new Color(0.95f, 0.76f, 0.3f), null, 0.5f));
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.95f, 0), new Vector3(0.36f, 0.025f, 0.025f), wood);
            // aşağı sarkan sancak, alt ucu çatallı
            var cloth = Pivot(t, "Kumaş", new Vector3(0, 0.94f, 0.02f));
            var flagCol = Art.Lit(IconColor(ItemType.Flag) * 0.85f, Art.ClothTex);
            Part(PrimitiveType.Cube, cloth, new Vector3(0, -0.17f, 0), new Vector3(0.3f, 0.34f, 0.015f), flagCol);
            Part(PrimitiveType.Cube, cloth, new Vector3(0.09f, -0.38f, 0), new Vector3(0.12f, 0.1f, 0.015f), flagCol);
            Part(PrimitiveType.Cube, cloth, new Vector3(-0.09f, -0.38f, 0), new Vector3(0.12f, 0.1f, 0.015f), flagCol);
            Gen.MeshObj(cloth, "Arma", MeshGen.Disc(5), Art.Lit(new Color(0.98f, 0.92f, 0.7f)), new Vector3(0, -0.17f, 0.01f), new Vector3(0.14f, 1, 0.14f), new Vector3(-90, 0, 0));
            cloth.gameObject.AddComponent<Waver>();
            return root;
        }

        /// <summary>Saydam "hayalet" kopya (yerleştirme önizlemesi).</summary>
        public static GameObject Ghost(ItemType type, Color tint)
        {
            var go = Create(type);
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
            var mat = Art.Transparent(tint);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            return go;
        }

        public static void SetTint(GameObject ghost, Color c)
        {
            var r = ghost.GetComponentInChildren<Renderer>();
            if (r) Art.SetColor(r.sharedMaterial, c);
        }
    }

    /// <summary>Zemindeki sihirli halkaların hafif nabız atması.</summary>
    public class DiscPulse : MonoBehaviour
    {
        Vector3 baseScale;
        float phase;
        void Start()
        {
            baseScale = transform.localScale;
            phase = (transform.position.x * 2.1f + transform.position.z * 1.9f) % (Mathf.PI * 2f);
        }
        void Update()
        {
            float p = Mathf.Sin(Time.time * 2.2f + phase) * 0.06f;
            transform.localScale = new Vector3(baseScale.x * (1f + p), baseScale.y, baseScale.z * (1f + p));
        }
    }

    /// <summary>Hafif yukarı-aşağı süzülme ve dönme.</summary>
    public class Bobber : MonoBehaviour
    {
        float phase;
        Vector3 basePos, baseScale;
        void Start()
        {
            basePos = transform.localPosition;
            baseScale = transform.localScale;
            phase = (transform.position.x * 1.7f + transform.position.z * 2.3f) % (Mathf.PI * 2f);
        }
        void Update()
        {
            float t = Time.time * 2.5f + phase;
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(t) * 0.06f + 0.05f);
            float scalePulse = 1f + Mathf.Sin(t * 1.5f) * 0.035f;
            transform.localScale = new Vector3(baseScale.x * scalePulse, baseScale.y, baseScale.z * scalePulse);
            transform.Rotate(0, 40f * Time.deltaTime, 0, Space.World);
        }
    }

    /// <summary>Bayrak dalgalanması.</summary>
    public class Waver : MonoBehaviour
    {
        void Update() => transform.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 4f + transform.position.x) * 20f, 0);
    }
}

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

        // ================= KAHRAMAN =================
        public static GameObject Hero()
        {
            var root = Root("Kahraman", out var body);
            var rig = root.AddComponent<HeroRig>();
            var tunic = Art.Lit(new Color(0.22f, 0.45f, 0.85f));
            var dark = Art.Lit(new Color(0.25f, 0.20f, 0.18f));
            var skin = Art.Lit(Art.Skin);

            // bacaklar ve (gizli) çizmeler
            Part(PrimitiveType.Cube, body, new Vector3(0.09f, 0.14f, 0), new Vector3(0.1f, 0.28f, 0.12f), dark);
            Part(PrimitiveType.Cube, body, new Vector3(-0.09f, 0.14f, 0), new Vector3(0.1f, 0.28f, 0.12f), dark);
            var boots = Pivot(body, "Çizmeler", Vector3.zero);
            var bootMat = Art.Glow(new Color(0.98f, 0.78f, 0.2f), 0.4f);
            foreach (var x in new[] { 0.09f, -0.09f })
            {
                Part(PrimitiveType.Cube, boots, new Vector3(x, 0.07f, 0.03f), new Vector3(0.14f, 0.15f, 0.2f), bootMat);
                Gen.MeshObj(boots, "Kanat", MeshGen.Cone(4), Art.Lit(Color.white), new Vector3(x * 1.9f, 0.13f, -0.04f), new Vector3(0.06f, 0.16f, 0.12f), new Vector3(0, 0, x > 0 ? -60 : 60));
            }

            // gövde
            Part(PrimitiveType.Capsule, body, new Vector3(0, 0.5f, 0), new Vector3(0.38f, 0.26f, 0.32f), tunic);
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.36f, 0), new Vector3(0.4f, 0.06f, 0.34f), Art.Lit(new Color(0.45f, 0.28f, 0.15f)));
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.55f, -0.17f), new Vector3(0.36f, 0.5f, 0.04f), Art.Lit(new Color(0.85f, 0.20f, 0.22f)), new Vector3(-10, 0, 0));

            // zırh (gizli)
            var armor = Pivot(body, "Zırh", Vector3.zero);
            var steel = Art.Lit(new Color(0.78f, 0.80f, 0.86f), null, 0.55f);
            Part(PrimitiveType.Cube, armor, new Vector3(0, 0.56f, 0.02f), new Vector3(0.36f, 0.3f, 0.3f), steel);
            Part(PrimitiveType.Sphere, armor, new Vector3(0.21f, 0.7f, 0), new Vector3(0.18f, 0.12f, 0.2f), steel);
            Part(PrimitiveType.Sphere, armor, new Vector3(-0.21f, 0.7f, 0), new Vector3(0.18f, 0.12f, 0.2f), steel);
            var helmet = Part(PrimitiveType.Sphere, armor, new Vector3(0, 1.0f, 0), new Vector3(0.36f, 0.26f, 0.36f), steel);
            Gen.MeshObj(armor, "Sorguç", MeshGen.Cone(5), Art.Lit(new Color(0.85f, 0.2f, 0.2f)), new Vector3(0, 1.1f, 0), new Vector3(0.06f, 0.18f, 0.06f));
            var shield = Pivot(body, "Kalkan", new Vector3(-0.24f, 0.5f, 0.06f));
            Gen.MeshObj(shield, "Kalkan", MeshGen.Cylinder(8, 0.5f, 0.5f), Art.Lit(new Color(0.85f, 0.65f, 0.2f), null, 0.5f), Vector3.zero, new Vector3(0.36f, 0.04f, 0.36f), new Vector3(0, 0, 90));
            Gen.MeshObj(shield, "Arma", MeshGen.Cylinder(6, 0.5f, 0.5f), Art.Lit(new Color(0.22f, 0.45f, 0.85f)), new Vector3(-0.02f, 0, 0), new Vector3(0.18f, 0.03f, 0.18f), new Vector3(0, 0, 90));

            // kafa
            Part(PrimitiveType.Sphere, body, new Vector3(0, 0.93f, 0), Vector3.one * 0.32f, skin);
            Gen.MeshObj(body, "Saç", MeshGen.Blob(3), Art.Lit(new Color(0.45f, 0.28f, 0.15f)), new Vector3(0, 1.02f, -0.03f), new Vector3(0.34f, 0.2f, 0.34f));
            Part(PrimitiveType.Sphere, body, new Vector3(0.06f, 0.95f, 0.14f), Vector3.one * 0.05f, dark);
            Part(PrimitiveType.Sphere, body, new Vector3(-0.06f, 0.95f, 0.14f), Vector3.one * 0.05f, dark);

            // kılıç
            var weapon = Pivot(body, "Kılıç", new Vector3(0.26f, 0.5f, 0.06f));
            var blade = Part(PrimitiveType.Cube, weapon, new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.5f, 0.025f), Art.Lit(new Color(0.85f, 0.88f, 0.92f), null, 0.6f));
            Part(PrimitiveType.Cube, weapon, new Vector3(0, 0.05f, 0), new Vector3(0.2f, 0.04f, 0.05f), Art.Lit(new Color(0.75f, 0.6f, 0.2f)));
            Part(PrimitiveType.Cube, weapon, new Vector3(0, -0.03f, 0), new Vector3(0.04f, 0.1f, 0.04f), Art.Lit(Art.Wood));
            var tip = Pivot(weapon, "Uç", new Vector3(0, 0.55f, 0));

            // ayak altı halka (kahramanı kalabalıkta bulmak kolay olsun)
            var ring = Gen.MeshObj(root.transform, "Halka", MeshGen.Disc(20), Art.Transparent(new Color(0.3f, 0.6f, 1f, 0.45f)), new Vector3(0, 0.02f, 0), new Vector3(0.85f, 1, 0.85f));
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var anim = root.AddComponent<UnitAnim>();
            anim.Init(body, weapon);
            rig.Init(anim, blade.GetComponent<MeshRenderer>(), tip, armor.gameObject, helmet, shield.gameObject, boots.gameObject, steel);
            return root;
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

        // ================= DÜŞMANLAR =================
        static GameObject Goblin()
        {
            var root = Root("Goblin", out var body);
            var green = Art.Lit(new Color(0.45f, 0.70f, 0.30f));
            Part(PrimitiveType.Capsule, body, new Vector3(0, 0.3f, 0), new Vector3(0.34f, 0.26f, 0.3f), Art.Lit(new Color(0.50f, 0.36f, 0.22f)));
            Part(PrimitiveType.Sphere, body, new Vector3(0, 0.7f, 0), Vector3.one * 0.36f, green);
            Gen.MeshObj(body, "Kulak", MeshGen.Cone(4), green, new Vector3(0.2f, 0.74f, 0), new Vector3(0.1f, 0.26f, 0.06f), new Vector3(0, 0, -70));
            Gen.MeshObj(body, "Kulak", MeshGen.Cone(4), green, new Vector3(-0.2f, 0.74f, 0), new Vector3(0.1f, 0.26f, 0.06f), new Vector3(0, 0, 70));
            var eye = Art.Glow(new Color(1f, 0.9f, 0.2f), 0.8f);
            Part(PrimitiveType.Sphere, body, new Vector3(0.07f, 0.74f, 0.16f), Vector3.one * 0.07f, eye);
            Part(PrimitiveType.Sphere, body, new Vector3(-0.07f, 0.74f, 0.16f), Vector3.one * 0.07f, eye);
            var weapon = Pivot(body, "Hançer", new Vector3(0.22f, 0.38f, 0.05f));
            Part(PrimitiveType.Cube, weapon, new Vector3(0, 0.12f, 0), new Vector3(0.05f, 0.26f, 0.03f), Art.Lit(new Color(0.7f, 0.7f, 0.72f), null, 0.5f));
            root.AddComponent<UnitAnim>().Init(body, weapon);
            return root;
        }

        static GameObject Archer()
        {
            var root = Root("Okçu", out var body);
            var red = Art.Lit(new Color(0.65f, 0.22f, 0.22f));
            Part(PrimitiveType.Capsule, body, new Vector3(0, 0.62f, 0), new Vector3(0.34f, 0.32f, 0.32f), red);
            Part(PrimitiveType.Sphere, body, new Vector3(0, 1.06f, 0), Vector3.one * 0.28f, Art.Lit(Art.Skin));
            Gen.MeshObj(body, "Kukuleta", MeshGen.Cone(6), Art.Lit(new Color(0.45f, 0.14f, 0.14f)), new Vector3(0, 1.0f, -0.02f), new Vector3(0.36f, 0.4f, 0.36f));
            var weapon = Pivot(body, "Yay", new Vector3(0.24f, 0.72f, 0.1f));
            var bow = Art.Lit(Art.Wood);
            Part(PrimitiveType.Cube, weapon, new Vector3(0, 0.16f, 0.04f), new Vector3(0.04f, 0.3f, 0.04f), bow, new Vector3(20, 0, 0));
            Part(PrimitiveType.Cube, weapon, new Vector3(0, -0.12f, 0.04f), new Vector3(0.04f, 0.3f, 0.04f), bow, new Vector3(-20, 0, 0));
            Part(PrimitiveType.Cube, weapon, new Vector3(0, 0.02f, -0.02f), new Vector3(0.01f, 0.56f, 0.01f), Art.Lit(Color.white));
            // ahşap gözcü platformu
            var wood = Art.Lit(Art.WoodLight, Art.WoodTex);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0, 0.2f, 0), new Vector3(0.7f, 0.06f, 0.7f), wood);
            foreach (var x in new[] { -0.3f, 0.3f })
                foreach (var z in new[] { -0.3f, 0.3f })
                    Part(PrimitiveType.Cube, root.transform, new Vector3(x, 0.1f, z), new Vector3(0.07f, 0.2f, 0.07f), Art.Lit(Art.Wood));
            body.localPosition = new Vector3(0, 0.0f, 0);
            root.AddComponent<UnitAnim>().Init(body, weapon);
            return root;
        }

        static GameObject Ogre()
        {
            var root = Root("Dev", out var body);
            var skin = Art.Lit(new Color(0.55f, 0.47f, 0.66f));
            Gen.MeshObj(body, "Gövde", MeshGen.Blob(2), skin, new Vector3(0, 0.62f, 0), new Vector3(0.8f, 0.85f, 0.7f));
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.36f, 0), new Vector3(0.74f, 0.2f, 0.62f), Art.Lit(new Color(0.45f, 0.30f, 0.18f)));
            Part(PrimitiveType.Sphere, body, new Vector3(0, 1.16f, 0.08f), Vector3.one * 0.36f, skin);
            var eye = Art.Glow(new Color(0.95f, 0.3f, 0.2f), 0.8f);
            Part(PrimitiveType.Sphere, body, new Vector3(0.08f, 1.2f, 0.24f), Vector3.one * 0.07f, eye);
            Part(PrimitiveType.Sphere, body, new Vector3(-0.08f, 1.2f, 0.24f), Vector3.one * 0.07f, eye);
            Gen.MeshObj(body, "Boynuz", MeshGen.Cone(5), Art.Lit(new Color(0.95f, 0.9f, 0.8f)), new Vector3(0.14f, 1.3f, 0.05f), new Vector3(0.08f, 0.2f, 0.08f), new Vector3(0, 0, -25));
            Gen.MeshObj(body, "Boynuz", MeshGen.Cone(5), Art.Lit(new Color(0.95f, 0.9f, 0.8f)), new Vector3(-0.14f, 1.3f, 0.05f), new Vector3(0.08f, 0.2f, 0.08f), new Vector3(0, 0, 25));
            var weapon = Pivot(body, "Sopa", new Vector3(0.44f, 0.7f, 0.1f));
            Gen.MeshObj(weapon, "Sap", MeshGen.Cylinder(6, 0.06f, 0.1f), Art.Lit(Art.Wood, Art.BarkTex), Vector3.zero, new Vector3(1, 0.6f, 1));
            Gen.MeshObj(weapon, "Baş", MeshGen.Rock(5), Art.Lit(Art.Wood, Art.BarkTex), new Vector3(0, 0.62f, 0), Vector3.one * 0.3f);
            root.AddComponent<UnitAnim>().Init(body, weapon);
            return root;
        }

        static GameObject Spikes()
        {
            var root = new GameObject("Diken Tuzağı");
            Gen.MeshObj(root.transform, "Taban", MeshGen.Cylinder(8, 0.42f, 0.4f), Art.Lit(Art.StoneDark, Art.StoneTex), Vector3.zero, new Vector3(1, 0.05f, 1));
            var metal = Art.Lit(new Color(0.75f, 0.76f, 0.80f), null, 0.5f);
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    Gen.MeshObj(root.transform, "Diken", MeshGen.Cone(4), metal, new Vector3(x * 0.22f, 0.04f, z * 0.22f), new Vector3(0.1f, 0.26f, 0.1f));
            return root;
        }

        // ================= DESTEK =================
        static GameObject Crate(Color band, string kind)
        {
            var root = new GameObject(kind == "sword" ? "Kılıç Kutusu" : kind == "armor" ? "Zırh Kutusu" : "Çizme Kutusu");
            var t = root.transform;
            var glow = Gen.MeshObj(t, "Işıltı", MeshGen.Disc(20), Art.Transparent(new Color(band.r, band.g, band.b, 0.35f)), new Vector3(0, 0.02f, 0), new Vector3(0.9f, 1, 0.9f));
            glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var box = Pivot(t, "Kutu", Vector3.zero);
            var wood = Art.Lit(Art.WoodLight, Art.WoodTex);
            var frame = Art.Lit(Art.Wood, Art.BarkTex);
            Part(PrimitiveType.Cube, box, new Vector3(0, 0.22f, 0), Vector3.one * 0.44f, wood);
            foreach (var x in new[] { -0.21f, 0.21f })
                foreach (var z in new[] { -0.21f, 0.21f })
                    Part(PrimitiveType.Cube, box, new Vector3(x, 0.22f, z), new Vector3(0.06f, 0.46f, 0.06f), frame);
            Part(PrimitiveType.Cube, box, new Vector3(0, 0.22f, 0), new Vector3(0.46f, 0.1f, 0.46f), Art.Lit(band));
            var icon = Pivot(t, "Simge", new Vector3(0, 0.75f, 0));
            switch (kind)
            {
                case "sword":
                    Part(PrimitiveType.Cube, icon, new Vector3(0, 0.05f, 0), new Vector3(0.07f, 0.42f, 0.03f), Art.Glow(new Color(1f, 0.6f, 0.2f), 1.2f), new Vector3(0, 0, 25));
                    Part(PrimitiveType.Cube, icon, new Vector3(0.07f, -0.13f, 0), new Vector3(0.2f, 0.04f, 0.05f), Art.Lit(new Color(0.75f, 0.6f, 0.2f)), new Vector3(0, 0, 25));
                    break;
                case "armor":
                    Part(PrimitiveType.Cube, icon, Vector3.zero, new Vector3(0.3f, 0.28f, 0.12f), Art.Lit(new Color(0.78f, 0.82f, 0.9f), null, 0.6f));
                    Part(PrimitiveType.Sphere, icon, new Vector3(0.17f, 0.12f, 0), new Vector3(0.14f, 0.1f, 0.14f), Art.Lit(new Color(0.78f, 0.82f, 0.9f), null, 0.6f));
                    Part(PrimitiveType.Sphere, icon, new Vector3(-0.17f, 0.12f, 0), new Vector3(0.14f, 0.1f, 0.14f), Art.Lit(new Color(0.78f, 0.82f, 0.9f), null, 0.6f));
                    break;
                case "boots":
                    var bm = Art.Glow(band, 0.5f);
                    Part(PrimitiveType.Cube, icon, new Vector3(-0.08f, 0, 0), new Vector3(0.1f, 0.22f, 0.12f), bm);
                    Part(PrimitiveType.Cube, icon, new Vector3(-0.03f, -0.08f, 0), new Vector3(0.2f, 0.07f, 0.12f), bm);
                    Gen.MeshObj(icon, "Kanat", MeshGen.Cone(4), Art.Lit(Color.white), new Vector3(-0.16f, 0.04f, 0), new Vector3(0.06f, 0.16f, 0.1f), new Vector3(0, 0, 60));
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
            Gen.MeshObj(t, "Işıltı", MeshGen.Disc(20), Art.Transparent(new Color(c.r, c.g, c.b, 0.35f)), new Vector3(0, 0.02f, 0), new Vector3(0.8f, 1, 0.8f))
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bottle = Pivot(t, "Şişe", Vector3.zero);
            Part(PrimitiveType.Sphere, bottle, new Vector3(0, 0.25f, 0), Vector3.one * 0.38f, Art.Glow(c, 0.9f));
            Part(PrimitiveType.Cylinder, bottle, new Vector3(0, 0.5f, 0), new Vector3(0.12f, 0.1f, 0.12f), Art.Lit(new Color(0.9f, 0.85f, 0.8f)));
            Part(PrimitiveType.Cylinder, bottle, new Vector3(0, 0.62f, 0), new Vector3(0.1f, 0.04f, 0.1f), Art.Lit(Art.Wood));
            Part(PrimitiveType.Sphere, bottle, new Vector3(0.08f, 0.32f, 0.12f), Vector3.one * 0.08f, Art.Lit(Color.white, null, 0.9f));
            bottle.gameObject.AddComponent<Bobber>();
            return root;
        }

        static GameObject Bomb()
        {
            var root = new GameObject("Bomba");
            var t = Pivot(root.transform, "Bomba", Vector3.zero);
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.25f, 0), Vector3.one * 0.44f, Art.Lit(new Color(0.18f, 0.18f, 0.22f), null, 0.4f));
            Part(PrimitiveType.Cylinder, t, new Vector3(0.06f, 0.5f, 0), new Vector3(0.04f, 0.08f, 0.04f), Art.Lit(new Color(0.8f, 0.7f, 0.5f)), new Vector3(0, 0, -20));
            Part(PrimitiveType.Sphere, t, new Vector3(0.1f, 0.6f, 0), Vector3.one * 0.08f, Art.Glow(new Color(1f, 0.6f, 0.1f), 3f));
            t.gameObject.AddComponent<Bobber>();
            return root;
        }

        static GameObject Flag()
        {
            var root = new GameObject("Bayrak");
            var t = root.transform;
            Gen.MeshObj(t, "Taş", MeshGen.Rock(2), Art.Lit(Art.Stone, Art.StoneTex), new Vector3(0, 0.04f, 0), new Vector3(0.3f, 0.15f, 0.3f));
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.5f, 0), new Vector3(0.04f, 0.5f, 0.04f), Art.Lit(Art.Wood));
            var cloth = Pivot(t, "Kumaş", new Vector3(0, 0.85f, 0));
            Part(PrimitiveType.Cube, cloth, new Vector3(0.17f, 0, 0), new Vector3(0.32f, 0.22f, 0.02f), Art.Glow(IconColor(ItemType.Flag), 0.4f));
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

    /// <summary>Hafif yukarı-aşağı süzülme ve dönme.</summary>
    public class Bobber : MonoBehaviour
    {
        float phase;
        Vector3 basePos;
        void Start() { basePos = transform.localPosition; phase = transform.position.x * 1.7f + transform.position.z * 2.3f; }
        void Update()
        {
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.2f + phase) * 0.05f + 0.05f);
            transform.Rotate(0, 35f * Time.deltaTime, 0, Space.World);
        }
    }

    /// <summary>Bayrak dalgalanması.</summary>
    public class Waver : MonoBehaviour
    {
        void Update() => transform.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 4f + transform.position.x) * 20f, 0);
    }

    /// <summary>Basit prosedürel animasyon: yürüme sallanması, vuruş, hasar alma, ölüm.</summary>
    public class UnitAnim : MonoBehaviour
    {
        Transform body, weapon;
        Quaternion weaponRest;
        Vector3 bodyBase;
        float attackT = 1f, hurtT = 1f, walkPhase, deadT = -1f;
        public bool Moving;
        public float SpeedMul = 1f;

        public void Init(Transform body, Transform weapon)
        {
            this.body = body; this.weapon = weapon;
            weaponRest = weapon ? weapon.localRotation : Quaternion.identity;
            bodyBase = body.localPosition;
        }

        public void Attack() => attackT = 0f;
        public void Hurt() => hurtT = 0f;
        public void Die() { if (deadT < 0f) deadT = 0f; }
        public bool IsDead => deadT >= 0f;

        void Update()
        {
            if (body == null) return;
            float dt = Time.deltaTime;
            if (deadT >= 0f)
            {
                deadT += dt;
                body.localRotation = Quaternion.Euler(Mathf.Min(90f, deadT * 300f), 0, 0);
                float s = Mathf.Clamp01(1.4f - deadT);
                transform.localScale = Vector3.one * s;
                if (s <= 0f) gameObject.SetActive(false);
                return;
            }
            if (Moving) walkPhase += dt * 14f * SpeedMul;
            float bob = Moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.08f : 0f;
            float tilt = Moving ? Mathf.Sin(walkPhase) * 6f : 0f;
            hurtT += dt;
            float squash = hurtT < 0.2f ? 1f - Mathf.Sin(hurtT / 0.2f * Mathf.PI) * 0.2f : 1f;
            body.localPosition = bodyBase + new Vector3(0, bob, 0);
            body.localRotation = Quaternion.Euler(Moving ? 6f : 0f, 0, tilt);
            body.localScale = new Vector3(2f - squash, squash, 2f - squash);

            if (weapon)
            {
                attackT += dt;
                float swing = attackT < 0.3f ? Mathf.Sin(attackT / 0.3f * Mathf.PI) * 85f : 0f;
                weapon.localRotation = weaponRest * Quaternion.Euler(swing, 0, 0);
            }
        }
    }
}

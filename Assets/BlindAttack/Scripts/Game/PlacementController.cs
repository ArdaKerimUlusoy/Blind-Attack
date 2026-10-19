using System;
using System.Collections.Generic;
using BlindAttack.Core;
using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlindAttack.Game
{
    /// <summary>Fare ile haritaya birim/kutu yerleştirme. Sol tık: koy, sağ tık: kaldır, 1-5: seç.</summary>
    public class PlacementController : MonoBehaviour
    {
        public bool Active { get; private set; }
        public bool Defense { get; private set; }
        public ItemType Selected { get; private set; }
        public readonly List<Placement> Placed = new List<Placement>();
        public string Hint { get; private set; } = "";
        public event Action Changed;

        MapView view;
        Camera cam;
        readonly Dictionary<Cell, GameObject> objects = new Dictionary<Cell, GameObject>();
        GameObject ghost;
        ItemType ghostType;
        ItemType[] palette;
        Vector2 rmbDownPos;
        Cell? rmbCandidateCell;
        // savunma yerleştirirken: düşmanın fark etme/atış menzili ve 5x5 bölge sınırı
        GameObject rangeRing, areaBox;
        Cell? areaCell; int areaCount = -1;
        Material areaOk, areaFull;

        public int Spent => MatchRules.Spent(Placed);
        public int Budget => MatchRules.Budget(Defense);
        public ItemType[] Palette => palette;

        public void Begin(MapView v, Camera c, bool defense, IList<Placement> initial)
        {
            view = v; cam = c; Defense = defense;
            palette = defense ? Items.DefenseTypes : Items.SupplyTypes;
            Selected = palette[0];
            Placed.Clear(); objects.Clear();
            if (initial != null)
                foreach (var p in MatchRules.Sanitize(view.Map, initial, defense)) Add(p, false);
            view.ShowOverlay(defense ? OverlayMode.Defense : OverlayMode.Supply);
            Active = true;
            Hint = "";
            Changed?.Invoke();
        }

        public void End()
        {
            Active = false;
            if (ghost) Destroy(ghost);
            HideRangeInfo();
            if (view) { view.SetHover(null, Color.white); view.ShowOverlay(OverlayMode.None); }
        }

        public void Select(ItemType t)
        {
            Selected = t;
            Changed?.Invoke();
        }

        /// <summary>Hepsini kaldırır (kahraman hariç: o yerinde kalır).</summary>
        public void ClearAll()
        {
            var keep = Placed.FindAll(p => p.Type == ItemType.Hero);
            foreach (var go in objects.Values) if (go) Destroy(go);
            objects.Clear();
            Placed.Clear();
            foreach (var k in keep) Add(k, false);
            Changed?.Invoke();
        }

        void Add(Placement p, bool fx = true)
        {
            if (p.Type == ItemType.Hero)
            {
                // tek kahraman: yenisi eskisinin yerini alır
                int old = Placed.FindIndex(x => x.Type == ItemType.Hero);
                if (old >= 0) Remove(Placed[old].Cell);
            }
            Placed.Add(p);
            var go = Visuals.Create(p.Type);
            go.transform.SetParent(view.UnitsRoot, false);
            go.transform.localPosition = MapView.CellToWorld(p.Cell);
            if (Items.Get(p.Type).IsDefense) go.transform.localRotation = Quaternion.Euler(0, 225f, 0);
            objects[p.Cell] = go;
            if (fx)
            {
                go.transform.localScale = new Vector3(0.4f, 1.6f, 0.4f);
                Tween.Scale(go.transform, new Vector3(1.22f, 0.78f, 1.22f), 0.12f, Ease.InQuad)
                    .OnComplete(() => Tween.Scale(go.transform, Vector3.one, 0.16f, Ease.OutBack));

                Effects.Burst(view.UnitsRoot, go.transform.position + Vector3.up * 0.2f, Visuals.IconColor(p.Type), 8, 2.5f, 0.07f);
                Effects.Ring(view.UnitsRoot, go.transform.position, Visuals.IconColor(p.Type), 0.7f);
                for (int s = 0; s < 4; s++)
                {
                    var vel = (UnityEngine.Random.insideUnitSphere + Vector3.up * 1.5f).normalized * 1.8f;
                    Effects.Spark(view.UnitsRoot, go.transform.position + Vector3.up * 0.25f, new Color(1f, 0.9f, 0.4f), vel, 0.05f, 0.4f);
                }

                switch (p.Type)
                {
                    case ItemType.Ogre: AudioManager.Play(SoundId.PlaceOgre); break;
                    case ItemType.SpikeTrap: AudioManager.Play(SoundId.PlaceTrap); break;
                    case ItemType.SwordCrate:
                    case ItemType.ArmorCrate:
                    case ItemType.SpeedBoots:
                    case ItemType.Bomb:
                    case ItemType.Potion:
                    case ItemType.Flag:
                        AudioManager.Play(SoundId.PlaceCrate);
                        break;
                    default:
                        AudioManager.Play(SoundId.PlaceUnit);
                        break;
                }
            }
        }

        bool Remove(Cell c)
        {
            int idx = Placed.FindIndex(p => p.Cell == c);
            if (idx < 0) return false;
            Placed.RemoveAt(idx);
            if (objects.TryGetValue(c, out var go) && go)
            {
                Effects.Burst(view.UnitsRoot, go.transform.position + Vector3.up * 0.2f, new Color(0.85f, 0.85f, 0.9f), 6, 2.0f, 0.06f);
                Destroy(go);
            }
            objects.Remove(c);
            AudioManager.Play(SoundId.Remove, 0.85f);
            return true;
        }

        void Update()
        {
            if (!Active || view == null || view.Map == null) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                for (int i = 0; i < palette.Length && i < 9; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) Select(palette[i]);
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (UiKit.PointerOverUi()) { ShowGhost(null, false); HideRangeInfo(); return; }

            if (!view.ScreenToCell(cam, mouse.position.ReadValue(), out var cell)) { ShowGhost(null, false); HideRangeInfo(); Hint = ""; return; }

            bool draggingCam = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            bool occupied = Placed.Exists(p => p.Cell == cell);
            string err = occupied ? null : MatchRules.CheckPlacement(view.Map, Placed, Selected, cell);
            ShowGhost(draggingCam || occupied ? (Cell?)null : cell, err == null);
            if (!draggingCam) view.SetHover(cell, occupied ? new Color(1f, 0.85f, 0.3f) : err == null ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.3f, 0.25f));
            else view.SetHover(null, Color.white);
            Hint = occupied ? "Sağ tık: kaldır   Sağ tık sürükle / Q-E: döndür" : err;
            if (Defense && !draggingCam)
            {
                var type = Selected;
                if (occupied) type = Placed.Find(p => p.Cell == cell).Type;
                ShowRangeInfo(cell, type, occupied || err == null);
            }
            else HideRangeInfo();

            if (mouse.leftButton.wasPressedThisFrame && !occupied && !draggingCam)
            {
                if (err == null) { Add(new Placement(Selected, cell)); Changed?.Invoke(); }
                else
                {
                    UiKit.Toast(err);
                    AudioManager.Play(SoundId.Error, 0.7f);
                    if (ghost) Tween.ShakeLocalPosition(ghost.transform, new Vector3(0.18f, 0, 0), 0.2f, 25);
                }
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                rmbDownPos = mouse.position.ReadValue();
                rmbCandidateCell = cell;
            }
            if (mouse.rightButton.wasReleasedThisFrame)
            {
                float dragDist = Vector2.Distance(mouse.position.ReadValue(), rmbDownPos);
                if (dragDist < 7f && rmbCandidateCell.HasValue && Remove(rmbCandidateCell.Value))
                    Changed?.Invoke();
                rmbCandidateCell = null;
            }
        }

        void HideRangeInfo()
        {
            if (rangeRing) rangeRing.SetActive(false);
            if (areaBox) areaBox.SetActive(false);
        }

        void ShowRangeInfo(Cell cell, ItemType type, bool valid)
        {
            var def = Items.Get(type);
            // menzil halkası (tuzakta yok)
            float radius = def.Ranged ? def.Range : def.Aggro;
            if (type != ItemType.SpikeTrap && radius > 0f)
            {
                if (rangeRing == null) rangeRing = Juice.RangeRing(view.transform, Color.red);
                rangeRing.SetActive(true);
                rangeRing.transform.localPosition = MapView.CellToWorld(cell);
                rangeRing.transform.localScale = new Vector3(radius, 1f, radius);
                Juice.SetRingColor(rangeRing, def.Ranged ? new Color(1f, 0.55f, 0.25f) : valid ? new Color(1f, 0.35f, 0.3f) : new Color(0.6f, 0.6f, 0.6f));
            }
            else if (rangeRing) rangeRing.SetActive(false);

            // bölge sınırı: etrafındaki 5x5 alanda kaç birim var?
            int r = MatchRules.DefenseAreaRadius, n = 0;
            foreach (var p in Placed)
                if (Items.Get(p.Type).IsDefense && p.Cell != cell && System.Math.Abs(p.Cell.X - cell.X) <= r && System.Math.Abs(p.Cell.Y - cell.Y) <= r) n++;
            if (areaBox == null || areaCell != cell || areaCount != n)
            {
                var cells = new List<Vector2Int>();
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        if (view.Map.IsWalkable(cell.X + dx, cell.Y + dy)) cells.Add(new Vector2Int(cell.X + dx, cell.Y + dy));
                bool full = n >= MatchRules.MaxDefendersInArea;
                if (areaOk == null)
                {
                    areaOk = Art.Transparent(new Color(1f, 0.95f, 0.6f, 0.10f));
                    areaFull = Art.Transparent(new Color(1f, 0.25f, 0.2f, 0.16f));
                }
                var mesh = MeshGen.CellQuads(cells, 0.018f, 0.02f);
                if (areaBox == null)
                {
                    areaBox = Gen.MeshObj(view.transform, "Bölge Sınırı", mesh, areaOk, Vector3.zero, Vector3.one);
                    areaBox.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    var mf = areaBox.GetComponent<MeshFilter>();
                    if (mf.sharedMesh) Destroy(mf.sharedMesh);
                    mf.sharedMesh = mesh;
                }
                areaBox.GetComponent<MeshRenderer>().sharedMaterial = full ? areaFull : areaOk;
                areaCell = cell; areaCount = n;
            }
            areaBox.SetActive(true);
        }

        void ShowGhost(Cell? cell, bool valid)
        {
            if (!cell.HasValue)
            {
                if (ghost) ghost.SetActive(false);
                if (!cell.HasValue && view) view.SetHover(null, Color.white);
                return;
            }
            if (ghost == null || ghostType != Selected)
            {
                if (ghost) Destroy(ghost);
                ghost = Visuals.Ghost(Selected, Color.white);
                ghost.transform.SetParent(view.transform, false);
                ghostType = Selected;
            }
            ghost.SetActive(true);
            ghost.transform.localPosition = MapView.CellToWorld(cell.Value);
            if (Items.Get(Selected).IsDefense) ghost.transform.localRotation = Quaternion.Euler(0, 225f, 0);
            Visuals.SetTint(ghost, valid ? new Color(0.5f, 1f, 0.5f, 0.55f) : new Color(1f, 0.35f, 0.3f, 0.5f));
        }
    }
}

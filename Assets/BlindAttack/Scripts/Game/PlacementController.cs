using System;
using System.Collections.Generic;
using BlindAttack.Core;
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
            if (fx) Effects.Burst(view.UnitsRoot, go.transform.position + Vector3.up * 0.2f, Visuals.IconColor(p.Type), 6, 2f, 0.06f);
        }

        bool Remove(Cell c)
        {
            int idx = Placed.FindIndex(p => p.Cell == c);
            if (idx < 0) return false;
            Placed.RemoveAt(idx);
            if (objects.TryGetValue(c, out var go) && go) Destroy(go);
            objects.Remove(c);
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
            if (UiKit.PointerOverUi()) { ShowGhost(null, false); return; }

            if (!view.ScreenToCell(cam, mouse.position.ReadValue(), out var cell)) { ShowGhost(null, false); Hint = ""; return; }

            bool occupied = Placed.Exists(p => p.Cell == cell);
            string err = occupied ? null : MatchRules.CheckPlacement(view.Map, Placed, Selected, cell);
            ShowGhost(occupied ? (Cell?)null : cell, err == null);
            view.SetHover(cell, occupied ? new Color(1f, 0.85f, 0.3f) : err == null ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.3f, 0.25f));
            Hint = occupied ? "Sağ tık: kaldır" : err;

            if (mouse.leftButton.wasPressedThisFrame && !occupied)
            {
                if (err == null) { Add(new Placement(Selected, cell)); Changed?.Invoke(); }
                else UiKit.Toast(err);
            }
            if (mouse.rightButton.wasPressedThisFrame && Remove(cell)) Changed?.Invoke();
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

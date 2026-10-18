using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BlindAttack.Game
{
    /// <summary>Koddan uGUI oluşturmak için küçük yardımcılar (sahne/prefab gerektirmez).</summary>
    public static class UiKit
    {
        public static readonly Color Parchment = new Color(0.95f, 0.89f, 0.75f, 0.97f);
        public static readonly Color ParchmentDark = new Color(0.86f, 0.77f, 0.60f, 1f);
        public static readonly Color Ink = new Color(0.23f, 0.16f, 0.10f);
        public static readonly Color WoodBtn = new Color(0.60f, 0.40f, 0.22f);
        public static readonly Color Green = new Color(0.42f, 0.64f, 0.28f);
        public static readonly Color Red = new Color(0.78f, 0.33f, 0.24f);
        public static readonly Color Blue = new Color(0.27f, 0.47f, 0.78f);
        public static readonly Color Gold = new Color(0.95f, 0.75f, 0.25f);
        public static readonly Color Outline = new Color(0.35f, 0.22f, 0.12f, 1f);

        public static Canvas Canvas { get; private set; }
        static Text toastText;
        static CanvasGroup toastGroup;

        public static Font Font => Effects.Font;

        public static void Init()
        {
            if (Canvas) return;
            var go = new GameObject("UI");
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            // bildirim (toast)
            var toast = Panel(Canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Color(0.2f, 0.12f, 0.07f, 0.9f));
            var rt = (RectTransform)toast.transform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 190);
            rt.sizeDelta = new Vector2(620, 60);
            toastGroup = toast.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastText = Label(toast.transform, "", 28, Color.white, TextAnchor.MiddleCenter);
            Stretch(toastText.rectTransform, 10);
        }

        public static void Toast(string msg, float seconds = 2f)
        {
            if (!toastText) return;
            toastText.text = msg;
            toastGroup.transform.SetAsLastSibling();
            var rt = (RectTransform)toastGroup.transform;
            rt.anchoredPosition = new Vector2(0, 180);
            Tween.StopAll(toastGroup);
            Tween.StopAll(rt);
            Tween.Alpha(toastGroup, 1f, 0.2f, Ease.OutQuad);
            Tween.UIAnchoredPosition(rt, new Vector2(0, 210), 0.22f, Ease.OutBack);
            Tween.Delay(seconds).OnComplete(() =>
            {
                if (toastGroup) Tween.Alpha(toastGroup, 0f, 0.35f, Ease.InQuad);
            });
            AudioManager.Play(SoundId.UiCopy, 0.6f);
        }

        public static bool PointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // ---------------- yapı taşları ----------------

        public static RectTransform Rect(GameObject go, Transform parent)
        {
            var rt = go.GetComponent<RectTransform>();
            if (!rt) rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        }

        public static GameObject Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, bool outline = false)
        {
            var go = new GameObject("Panel");
            var rt = Rect(go, parent);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = Art.RoundRect;
            img.type = Image.Type.Sliced;
            img.color = color;
            if (outline)
            {
                var o = go.AddComponent<UnityEngine.UI.Outline>();
                o.effectColor = Outline;
                o.effectDistance = new Vector2(3, -3);
                var s = go.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.35f);
                s.effectDistance = new Vector2(0, -8);
            }
            return go;
        }

        /// <summary>Sabit boyutlu, verilen noktaya hizalı panel.</summary>
        public static GameObject Box(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, bool outline = true)
        {
            var go = Panel(parent, anchor, anchor, color, outline);
            var rt = (RectTransform)go.transform;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text");
            var rt = Rect(go, parent);
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            rt.sizeDelta = new Vector2(400, size * 1.6f);
            return t;
        }

        public static Text Title(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = Label(parent, text, size, color, align, FontStyle.Bold);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.45f);
            sh.effectDistance = new Vector2(2, -3);
            return t;
        }

        public static Button Button(Transform parent, string label, Color color, Action onClick, int fontSize = 30)
        {
            var go = new GameObject("Button");
            Rect(go, parent).sizeDelta = new Vector2(320, 70);
            var img = go.AddComponent<Image>();
            img.sprite = Art.RoundRect;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var o = go.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = Outline;
            o.effectDistance = new Vector2(2, -2);
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.3f);
            sh.effectDistance = new Vector2(0, -5);
            var b = go.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = color;
            cb.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            cb.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            cb.selectedColor = color;
            cb.disabledColor = new Color(0.55f, 0.52f, 0.48f, 0.8f);
            b.colors = cb;
            b.targetGraphic = img;
            go.AddComponent<ButtonJuice>();
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Title(go.transform, label, fontSize, Color.white);
            Stretch(t.rectTransform, 6);
            return b;
        }

        public static InputField Input(Transform parent, string placeholder, int fontSize = 30)
        {
            var go = new GameObject("Input");
            Rect(go, parent).sizeDelta = new Vector2(320, 70);
            var img = go.AddComponent<Image>();
            img.sprite = Art.RoundRect;
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 0.98f, 0.92f);
            var o = go.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = Outline;
            o.effectDistance = new Vector2(2, -2);

            var text = Label(go.transform, "", fontSize, Ink, TextAnchor.MiddleCenter);
            text.supportRichText = false;
            Stretch(text.rectTransform, 12);
            var ph = Label(go.transform, placeholder, fontSize, new Color(Ink.r, Ink.g, Ink.b, 0.4f), TextAnchor.MiddleCenter, FontStyle.Italic);
            Stretch(ph.rectTransform, 12);

            var input = go.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = img;
            return input;
        }

        public static VerticalLayoutGroup VStack(GameObject go, float spacing, int pad, TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = false;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(GameObject go, float spacing, int pad, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childAlignment = align;
            h.childControlWidth = false; h.childControlHeight = false;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static void Size(Component c, float w, float h)
        {
            var rt = (RectTransform)c.transform;
            rt.sizeDelta = new Vector2(w, h);
            var le = c.GetComponent<LayoutElement>();
            if (!le) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h;
            le.minHeight = h;
        }
    }

    public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public bool DisableSound;
        Button btn;
        Vector3 baseScale = Vector3.one;

        void Awake()
        {
            btn = GetComponent<Button>();
            baseScale = transform.localScale;
        }

        void OnDisable()
        {
            transform.localScale = baseScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (btn != null && !btn.interactable) return;
            Tween.StopAll(transform);
            Tween.Scale(transform, baseScale * 1.045f, 0.12f, Ease.OutQuad);
            if (!DisableSound) AudioManager.Play(SoundId.UiHover, 0.35f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Tween.StopAll(transform);
            Tween.Scale(transform, baseScale, 0.15f, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (btn != null && !btn.interactable) return;
            Tween.StopAll(transform);
            Tween.Scale(transform, baseScale * 0.94f, 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (btn != null && !btn.interactable) return;
            Tween.StopAll(transform);
            Tween.Scale(transform, baseScale * 1.045f, 0.12f, Ease.OutBack);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (btn != null && !btn.interactable) return;
            if (!DisableSound) AudioManager.Play(SoundId.UiClick, 0.8f);
            Tween.PunchScale(transform, new Vector3(0.06f, -0.05f, 0f), 0.18f, 10);
        }
    }
}

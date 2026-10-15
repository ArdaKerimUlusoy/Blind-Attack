using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlindAttack.Game
{
    /// <summary>
    /// Haritayı, arayüz panellerinin ARASINDA kalan boşluğa sığdıran izometrik (ortografik) kamera.
    /// Fare tekerleği: yakınlaş. WASD / ok tuşları / orta tuşla sürükle: kaydır. Space: ortala.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class IsoCamera : MonoBehaviour
    {
        [Range(20f, 70f)] public float pitch = 36f;
        [Range(0f, 90f)] public float yaw = 45f;

        /// <summary>Ekranın üstünde ve altında arayüzün kapladığı piksel yükseklikleri (üst, alt).</summary>
        public Func<Vector2> UiInsets;

        Camera cam;
        Bounds target = new Bounds(new Vector3(15, 0.5f, 10), new Vector3(30, 1, 20));
        float zoom = 1f, shake;
        Vector3 pan, followOffset;
        Transform follow;
        float followWeight;
        Vector2 lastMouse;

        public Camera Cam => cam ? cam : (cam = GetComponent<Camera>());
        Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);

        void OnEnable()
        {
            if (!Cam.orthographic) Cam.orthographic = true;
            if (Cam.nearClipPlane != 0.1f) Cam.nearClipPlane = 0.1f;
            if (Cam.farClipPlane != 300f) Cam.farClipPlane = 300f;
            if (!Application.isPlaying)
            {
                var map = FindAnyObjectByType<MapView>();
                if (map) target = map.WorldBounds;
                Apply(true);
            }
        }

        public void Frame(Bounds b)
        {
            target = b;
            zoom = 1f;
            pan = Vector3.zero;
            followOffset = Vector3.zero;
            follow = null;
            Apply(true);
        }

        /// <summary>Saldırı izlerken kahramanı hafifçe takip et.</summary>
        public void Follow(Transform t, float weight, float autoZoom = 1f)
        {
            follow = t; followWeight = weight;
            zoom = t != null ? autoZoom : 1f;
            pan = Vector3.zero;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            HandleInput();
            Apply(false);
        }

        void HandleInput()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;
            var right = Rotation * Vector3.right; right.y = 0; right.Normalize();
            var fwd = Rotation * Vector3.forward; fwd.y = 0; fwd.Normalize();

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !UiKit.PointerOverUi())
                    zoom = Mathf.Clamp(zoom * (scroll > 0 ? 0.88f : 1.12f), 0.35f, 1.25f);

                var mp = mouse.position.ReadValue();
                if (mouse.middleButton.isPressed)
                {
                    var d = mp - lastMouse;
                    float unitsPerPixel = Cam.orthographicSize * 2f / Mathf.Max(1, Screen.height);
                    pan -= (right * d.x + fwd * d.y / Mathf.Sin(pitch * Mathf.Deg2Rad)) * unitsPerPixel;
                }
                lastMouse = mp;
            }
            if (kb != null)
            {
                var move = Vector3.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move += fwd;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move -= fwd;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += right;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= right;
                pan += move * dt * 14f * zoom;
                if (kb.spaceKey.wasPressedThisFrame) { pan = Vector3.zero; zoom = 1f; }
            }
            // çok uzaklaşmasın
            var ext = target.extents;
            pan.x = Mathf.Clamp(pan.x, -ext.x, ext.x);
            pan.z = Mathf.Clamp(pan.z, -ext.z, ext.z);
        }

        void Apply(bool instant)
        {
            if (Cam == null) return;
            var rot = Rotation;
            if (Application.isPlaying || Quaternion.Angle(transform.rotation, rot) > 0.01f) transform.rotation = rot;
            var right = rot * Vector3.right; var up = rot * Vector3.up; var forward = rot * Vector3.forward;

            // sınırların kamera düzlemindeki izdüşümü
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            var c = target.center; var e = target.extents;
            for (int i = 0; i < 8; i++)
            {
                var p = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                float x = Vector3.Dot(p - c, right), y = Vector3.Dot(p - c, up);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            float mapW = maxX - minX, mapH = maxY - minY;

            // arayüzün kapladığı yer
            float sh = Mathf.Max(1, Screen.height);
            var ins = Application.isPlaying && UiInsets != null ? UiInsets() : Vector2.zero;
            float topFrac = Mathf.Clamp01(ins.x / sh), botFrac = Mathf.Clamp01(ins.y / sh);
            float availFrac = Mathf.Max(0.3f, 1f - topFrac - botFrac);

            const float margin = 1.06f;
            float size = Mathf.Max(mapH * 0.5f / availFrac, mapW * 0.5f / Mathf.Max(0.1f, Cam.aspect)) * margin;
            float targetSize = size * zoom;
            float newSize = instant ? targetSize : Mathf.Lerp(Cam.orthographicSize, targetSize, Time.unscaledDeltaTime * 8f);
            if (Application.isPlaying || Mathf.Abs(Cam.orthographicSize - newSize) > 0.001f) Cam.orthographicSize = newSize;

            // boş alanın ortası ekran ortasından ne kadar kaymış?
            float availCenter = botFrac + availFrac * 0.5f;       // 0..1 (alttan)
            float shiftWorld = (availCenter - 0.5f) * 2f * Cam.orthographicSize;
            // harita merkezi o noktaya gelsin: kamerayı ters yöne kaydır
            var focus = c + pan;
            if (follow != null && follow.gameObject.activeInHierarchy && Application.isPlaying)
            {
                float w = followWeight * Mathf.InverseLerp(1.1f, 0.4f, zoom) + followWeight * 0.2f;
                var desired = (follow.position - c) * Mathf.Clamp01(w);
                desired.y = 0;
                followOffset = Vector3.Lerp(followOffset, desired, Time.unscaledDeltaTime * 3f);
            }
            else followOffset = Vector3.Lerp(followOffset, Vector3.zero, instant ? 1f : Time.unscaledDeltaTime * 3f);
            focus += followOffset;

            var pos = focus - forward * 40f - up * shiftWorld;
            if (shake > 0f && Application.isPlaying)
            {
                var r = UnityEngine.Random.insideUnitCircle * shake * 0.25f;
                pos += right * r.x + up * r.y;
                shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * 2.5f);
            }
            // edit modunda gereksiz yere sahneyi "değişti" diye işaretlemesin
            if (Application.isPlaying || (transform.position - pos).sqrMagnitude > 1e-6f) transform.position = pos;
        }
    }
}

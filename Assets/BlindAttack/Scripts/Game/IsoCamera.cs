using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlindAttack.Game
{
    /// <summary>
    /// Tabletop Simulator tarzı serbest izometrik ve perspektif kamera.
    /// Sağ tık sürükle: serbest açı döndürme (Yaw & Pitch).
    /// Q & E: masa etrafında yatay döndürme.
    /// WASD / ok tuşları / orta tık sürükle: serbest zemin kaydırma (Pan).
    /// Fare tekerleği: derin yakınlaşma ve geniş uzaklaşma (Zoom).
    /// Space: masa merkezine ve varsayılan açıya sıfırlama.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class IsoCamera : MonoBehaviour
    {
        [Header("Açı ve Döndürme")]
        [Range(15f, 85f)] public float pitch = 38f;
        public float yaw = 45f;

        [Header("Tabletop Kontrolleri")]
        public float rotateSpeed = 0.22f;
        public float keyRotateSpeed = 95f;
        public float minPitch = 16f;
        public float maxPitch = 82f;
        public float minZoom = 0.18f;
        public float maxZoom = 2.40f;

        /// <summary>Ekranın üstünde ve altında arayüzün kapladığı piksel yükseklikleri (üst, alt).</summary>
        public Func<Vector2> UiInsets;

        Camera cam;
        Bounds target = new Bounds(new Vector3(15, 0.5f, 10), new Vector3(30, 1, 20));
        float zoom = 1f, targetZoom = 1f;
        float targetYaw = 45f, targetPitch = 38f;
        float shake;
        Vector3 pan, followOffset;
        Transform follow;
        float followWeight;
        Vector2 lastMouse;
        bool isDraggingRmb;

        public Camera Cam => cam ? cam : (cam = GetComponent<Camera>());
        public Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);

        void OnEnable()
        {
            if (Cam.orthographic) Cam.orthographic = false;
            Cam.fieldOfView = 34f;
            if (Cam.nearClipPlane != 0.1f) Cam.nearClipPlane = 0.1f;
            if (Cam.farClipPlane != 350f) Cam.farClipPlane = 350f;
            targetYaw = yaw;
            targetPitch = pitch;
            targetZoom = zoom;
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
            targetZoom = 1f;
            pan = Vector3.zero;
            followOffset = Vector3.zero;
            follow = null;
            intro = false;
            // menüde dönen kamera oyuna başlarken varsayılan açıya dönsün
            yaw = targetYaw = 45f; pitch = targetPitch = 38f;
            Apply(true);
        }

        /// <summary>Saldırı izlerken kahramanı hafifçe takip et.</summary>
        public void Follow(Transform t, float weight, float autoZoom = 1f)
        {
            follow = t; followWeight = weight;
            if (t != null) targetZoom = autoZoom;
            pan = Vector3.zero;
        }

        /// <summary>Ayarlardan kapatılabilir (baş ağrıtmasın).</summary>
        public static bool ShakeEnabled = true;
        public void Shake(float amount) { if (ShakeEnabled) shake = Mathf.Max(shake, amount); }
        public bool MenuMode = true;
        float menuTime;

        // ---- saldırı açılış sinematiği: çıkıştan haritanın üstüne, oradan kahramana süzülür ----
        bool intro;
        float introK;
        Vector3 introFrom, introTo;
        public bool InIntro => intro;

        public void BeginIntro(Vector3 fromWorld, Vector3 toWorld)
        {
            intro = true; introK = 0f;
            introFrom = fromWorld; introTo = toWorld;
            follow = null; followOffset = Vector3.zero;
        }

        /// <summary>0..1 ilerleme (oynatıcı kendi zamanıyla sürer, böylece duraklatma ve hız ayarına uyar).</summary>
        public void SetIntroProgress(float k) => introK = Mathf.Clamp01(k);

        public void EndIntro()
        {
            if (!intro) return;
            intro = false;
            followOffset = pan; pan = Vector3.zero;
            targetYaw = yaw; targetPitch = pitch; targetZoom = zoom;
        }

        void UpdateIntro()
        {
            float k = introK;
            var c = target.center;
            Vector3 p0 = introFrom - c, p2 = introTo - c;
            float u = k * k * (3f - 2f * k);
            // ikinci dereceden eğri: çıkış -> harita merkezi -> kahraman
            pan = (1f - u) * (1f - u) * p0 + u * u * p2;
            pan.y = 0f;
            float a = Mathf.Clamp01(k / 0.5f), b = Mathf.Clamp01((k - 0.5f) / 0.5f);
            float sa = a * a * (3f - 2f * a), sb = b * b * (3f - 2f * b);
            zoom = k < 0.5f ? Mathf.Lerp(0.5f, 1.05f, sa) : Mathf.Lerp(1.05f, 0.72f, sb);
            pitch = k < 0.5f ? Mathf.Lerp(24f, 52f, sa) : Mathf.Lerp(52f, 38f, sb);
            yaw = Mathf.Lerp(12f, 45f, u);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            HandleInput();
            Apply(false);
        }

        void HandleInput()
        {
            if (intro) { UpdateIntro(); return; }
            if (MenuMode)
            {
                // ana menü: harita etrafında yavaş tur, hafif yaklaşıp uzaklaşma
                menuTime += Time.unscaledDeltaTime * 0.25f;
                pan = new Vector3(Mathf.Sin(menuTime) * 1.6f, 0, Mathf.Cos(menuTime * 0.7f) * 1.1f);
                targetZoom = 0.92f + Mathf.Sin(menuTime * 0.6f) * 0.08f;
                zoom = Mathf.Lerp(zoom, targetZoom, Time.unscaledDeltaTime * 3f);
                targetYaw = yaw + Time.unscaledDeltaTime * 5f;
                yaw = targetYaw;
                pitch = Mathf.Lerp(pitch, 34f + Mathf.Sin(menuTime * 0.4f) * 4f, Time.unscaledDeltaTime * 2f);
                targetPitch = pitch;
                return;
            }

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;

            var rot = Rotation;
            var right = rot * Vector3.right; right.y = 0; right.Normalize();
            var fwd = rot * Vector3.forward; fwd.y = 0; fwd.Normalize();

            if (mouse != null)
            {
                // Zoom (fare tekerleği)
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !UiKit.PointerOverUi())
                {
                    float factor = scroll > 0 ? 0.82f : 1.20f;
                    targetZoom = Mathf.Clamp(targetZoom * factor, minZoom, maxZoom);
                }

                var mp = mouse.position.ReadValue();
                if (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                    lastMouse = mp;
                var delta = mp - lastMouse;

                // Sağ tıkla basıp sürükleme: Tabletop Simulator tarzı serbest kamera döndürme (Yaw & Pitch)
                if (mouse.rightButton.isPressed)
                {
                    if (mouse.rightButton.wasPressedThisFrame) isDraggingRmb = true;
                    if (isDraggingRmb && delta.sqrMagnitude > 0.001f)
                    {
                        targetYaw += delta.x * rotateSpeed;
                        targetPitch -= delta.y * rotateSpeed * 0.85f;
                        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
                    }
                }
                else isDraggingRmb = false;

                // Orta tuşla basıp sürükleme: harita zeminini kaydırma (Pan)
                if (mouse.middleButton.isPressed)
                {
                    float unitsPerPixel = 0.025f * Mathf.Clamp(zoom, 0.4f, 2.0f);
                    pan -= (right * delta.x + fwd * delta.y / Mathf.Max(0.15f, Mathf.Sin(pitch * Mathf.Deg2Rad))) * unitsPerPixel;
                }
                lastMouse = mp;
            }

            if (kb != null)
            {
                // Q & E: Tabletop Simulator klavye döndürmesi
                if (kb.qKey.isPressed) targetYaw -= keyRotateSpeed * dt;
                if (kb.eKey.isPressed) targetYaw += keyRotateSpeed * dt;

                // WASD / Ok tuşları: kameranın baktığı açıya göre zemin hareketi
                float speed = 14f * Mathf.Clamp(zoom, 0.35f, 2.0f);
                if (kb.shiftKey.isPressed) speed *= 1.8f;
                var move = Vector3.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move += fwd;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move -= fwd;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += right;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= right;
                pan += move * dt * speed;

                // Space tuşu: kamerayı merkez açı ve konuma sıfırla
                if (kb.spaceKey.wasPressedThisFrame)
                {
                    pan = Vector3.zero;
                    targetZoom = 1f;
                    targetYaw = 45f;
                    targetPitch = 38f;
                }
            }

            // Açı ve zoom yumuşatması
            yaw = Mathf.LerpAngle(yaw, targetYaw, dt * 14f);
            pitch = Mathf.Lerp(pitch, targetPitch, dt * 14f);
            zoom = Mathf.Lerp(zoom, targetZoom, dt * 10f);

            // Harita çevresinde serbest hareket sınırları (masanın dışını da görebilsin)
            var ext = target.extents;
            float maxPanX = ext.x * 1.35f;
            float maxPanZ = ext.z * 1.35f;
            pan.x = Mathf.Clamp(pan.x, -maxPanX, maxPanX);
            pan.z = Mathf.Clamp(pan.z, -maxPanZ, maxPanZ);
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

            float sh = Mathf.Max(1, Screen.height);
            var ins = Application.isPlaying && UiInsets != null ? UiInsets() : Vector2.zero;
            float topFrac = Mathf.Clamp01(ins.x / sh), botFrac = Mathf.Clamp01(ins.y / sh);
            float availFrac = Mathf.Max(0.3f, 1f - topFrac - botFrac);

            const float margin = 1.06f;
            float halfFovRad = Cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalfFov = Mathf.Max(0.01f, Mathf.Tan(halfFovRad));

            float size = Mathf.Max(mapH * 0.5f / availFrac, mapW * 0.5f / Mathf.Max(0.1f, Cam.aspect)) * margin;
            float targetSize = size * zoom;
            float currentDist = targetSize / tanHalfFov;

            float availCenter = botFrac + availFrac * 0.5f;
            float shiftWorld = (availCenter - 0.5f) * 2f * targetSize;

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

            var pos = focus - forward * currentDist - up * shiftWorld;
            if (shake > 0f && Application.isPlaying)
            {
                var r = UnityEngine.Random.insideUnitCircle * shake * 0.25f;
                pos += right * r.x + up * r.y;
                shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * 2.5f);
            }
            if (Application.isPlaying || (transform.position - pos).sqrMagnitude > 1e-6f) transform.position = pos;
        }
    }
}

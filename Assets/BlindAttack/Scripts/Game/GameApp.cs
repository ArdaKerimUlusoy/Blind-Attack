using System;
using System.Collections.Generic;
using BlindAttack.Match;
using BlindAttack.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlindAttack.Game
{
    public enum MapChoice { HerMactaDegisir = -1, NehirVadisi = 0, KaleBahcesi = 1, TasOcagi = 2 }

    /// <summary>
    /// Oyunun giriş noktası. Sahnedeki "Blind Attack" nesnesinde durur (yoksa kendiliğinden oluşur);
    /// sahnedeki Harita, Çevre ve kamerayı bulup kullanır, arayüzü ve ağ bağlantısını kurar.
    /// </summary>
    public class GameApp : MonoBehaviour
    {
        enum Mode { Menu, Solo, Host, Client }

        [Header("Maç")]
        [Tooltip("Maçta oynanacak harita. 'Her Macta Degisir' seçiliyse her yeni maç (rövanş dahil) bir öncekinden farklı haritada oynanır. Oda sahibinin seçimi geçerlidir.")]
        public MapChoice matchMap = MapChoice.HerMactaDegisir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameApp>() != null) return;
            new GameObject("Blind Attack").AddComponent<GameApp>();
        }

        Mode mode = Mode.Menu;
        GameUI ui;
        MapView view;
        IsoCamera cam;
        PlacementController placer;
        Playback playback;
        PlayerSession session;
        RoutePreview route;
        NetLayer net;
        MatchHost host;
        BotPlayer bot;
        bool matchStarted, connecting;
        string myName = "Oyuncu";
        readonly Queue<Action> inbox = new Queue<Action>();

        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 120;

            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }
            cam = camera.GetComponent<IsoCamera>();
            if (!cam) cam = camera.gameObject.AddComponent<IsoCamera>();
            ApplyLook(camera);

            view = FindAnyObjectByType<MapView>();
            if (!view) view = new GameObject("Harita").AddComponent<MapView>();
            if (!FindAnyObjectByType<EnvironmentView>()) new GameObject("Çevre").AddComponent<EnvironmentView>();

            placer = gameObject.AddComponent<PlacementController>();
            playback = gameObject.AddComponent<Playback>();
            session = gameObject.AddComponent<PlayerSession>();
            route = gameObject.AddComponent<RoutePreview>();

            ui = new GameUI();
            ui.Build();
            cam.UiInsets = ui.ScreenInsets;
            session.Init(ui, view, cam, placer, playback, route);

            ui.OnSolo = StartSolo;
            ui.OnHostOnline = n => HostOnline(n);
            ui.OnJoinOnline = (n, code) => JoinOnline(n, code);
            ui.OnLanHost = HostLan;
            ui.OnLanJoin = JoinLan;
            ui.OnCancel = () => BackToMenu("");
            ui.OnBackToMenu = () => BackToMenu("");
            ui.OnRematch = Rematch;
            ui.OnResume = () => SetPause(false);
            ui.OnQuitGame = QuitGame;
            ui.OnToggleFullscreen = () =>
            {
                bool fs = !Screen.fullScreen;
                Screen.fullScreenMode = fs ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Screen.fullScreen = fs;
                ui.SetFullscreenLabel(fs);
            };
            session.CanRematch = CanRematch;

            ShowMenuMap();
        }

        void ShowMenuMap()
        {
            view.Build(matchMap == MapChoice.HerMactaDegisir ? PeekNextMap() : (int)matchMap);
            cam.Frame(view.WorldBounds);
        }

        // ---------------- Harita rotasyonu ----------------
        const string LastMapKey = "BlindAttack.LastMap";

        int PeekNextMap()
        {
            int last = PlayerPrefs.GetInt(LastMapKey, -1);
            return last < 0 ? 0 : (last + 1) % Core.MapLibrary.Count;
        }

        /// <summary>Bu maçın haritası: sabit seçim ya da (varsayılan) bir öncekinden farklı, sıradaki harita.</summary>
        int NextMatchMap()
        {
            int map = matchMap == MapChoice.HerMactaDegisir ? PeekNextMap() : (int)matchMap;
            PlayerPrefs.SetInt(LastMapKey, map);
            PlayerPrefs.Save();
            return map;
        }

        // ---------------- Rövanş ----------------
        bool CanRematch() => host != null && (mode == Mode.Solo || (mode == Mode.Host && net != null && net.HasRemote));

        void Rematch()
        {
            if (!CanRematch()) { UiKit.Toast("Rakip ayrıldı, rövanş başlatılamıyor."); return; }
            inbox.Clear();
            host.StartMatch(Environment.TickCount, NextMatchMap());
        }

        // ---------------- ESC menüsü ----------------
        void TogglePause()
        {
            if (ui.CloseHelpIfOpen()) return;
            SetPause(!ui.PauseOpen);
        }

        void SetPause(bool open)
        {
            bool inMatch = mode != Mode.Menu;
            string note = mode == Mode.Solo ? "Oyun duraklatıldı." : inMatch ? "Online maçta oyun durmaz, süre işlemeye devam eder." : "";
            ui.ShowPause(open, inMatch, note, Screen.fullScreen);
            // sadece tek kişilik modda zamanı durdur
            Time.timeScale = open && mode == Mode.Solo ? 0f : 1f;
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Işık, ortam rengi ve kamera arka planı (editör sahne kurulumu da kullanır).</summary>
        public static void ApplyLook(Camera camera)
        {
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = EnvironmentView.FarGround;
            }

            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Güneş").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.80f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(50f, 20f, 0f);

            // sıcak, yumuşak ortam ışığı: gölgeler simsiyah olmasın (el boyaması hissi)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.85f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.70f, 0.60f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.40f, 0.32f);
            RenderSettings.fog = false;
        }

        // ================= Modlar =================

        void StartSolo(string name)
        {
            if (connecting) return;
            myName = name;
            ResetMatch();
            mode = Mode.Solo;
            int seed = Environment.TickCount;
            host = new MatchHost();
            bot = new BotPlayer(seed);
            host.Broadcast = m => { Post(() => session.Handle(m)); Post(() => bot?.Handle(m)); };
            bot.Send = m => Post(() => host?.OnPlayerMessage(1, m));
            session.Send = m => Post(() => host?.OnPlayerMessage(0, m));
            host.SetName(0, name);
            host.SetName(1, "Bot");
            session.Begin(0);
            host.StartMatch(seed, NextMatchMap());
            matchStarted = true;
        }

        async void HostOnline(string name)
        {
            if (connecting) return;
            myName = name;
            ResetMatch();
            connecting = true;
            ui.SetMenuStatus("Oda kuruluyor...", false);
            try
            {
                SetupHostNet();
                string code = await net.HostOnlineAsync();
                if (mode != Mode.Host) return; // bu arada iptal edildi
                ui.ShowWaiting(code, "Kodu arkadaşına gönder. Rakip bekleniyor...", true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                BackToMenu(NetLayer.Explain(e));
            }
            finally { connecting = false; }
        }

        async void JoinOnline(string name, string code)
        {
            if (connecting) return;
            if (string.IsNullOrWhiteSpace(code)) { ui.SetMenuStatus("Önce oda kodunu yaz.", true); return; }
            myName = name;
            ResetMatch();
            connecting = true;
            ui.ShowWaiting("", "Odaya bağlanılıyor...", false);
            try
            {
                SetupClientNet();
                await net.JoinOnlineAsync(code);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                BackToMenu(NetLayer.Explain(e));
            }
            finally { connecting = false; }
        }

        void HostLan(string name)
        {
            if (connecting) return;
            myName = name;
            ResetMatch();
            SetupHostNet();
            if (!net.StartLanHost()) { BackToMenu("LAN sunucusu başlatılamadı (port " + NetLayer.LanPort + " kullanımda olabilir)."); return; }
            ui.ShowWaiting(LocalIp(), "LAN odası açıldı. Rakip bu IP ile katılmalı.", true);
        }

        void JoinLan(string name, string ip)
        {
            if (connecting) return;
            myName = name;
            ResetMatch();
            SetupClientNet();
            if (!net.StartLanClient(ip)) { BackToMenu("Bağlantı başlatılamadı."); return; }
            ui.ShowWaiting("", "Bağlanılıyor: " + (string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip), false);
        }

        void SetupHostNet()
        {
            mode = Mode.Host;
            net = new NetLayer();
            host = new MatchHost();
            host.SetName(0, myName);
            host.Broadcast = m => { Post(() => session.Handle(m)); net?.SendToClient(m); };
            session.Send = m => Post(() => host?.OnPlayerMessage(0, m));
            net.RemoteConnected += () => Post(() => ui.ShowWaiting("", "Rakip bağlandı, oyun başlıyor...", false));
            net.MessageFromClient += m => Post(() =>
            {
                if (host == null) return;
                host.OnPlayerMessage(1, m);
                if (!matchStarted && m.StartsWith("HELLO"))
                {
                    matchStarted = true;
                    session.Begin(0);
                    host.StartMatch(Environment.TickCount, NextMatchMap());
                }
            });
            net.Disconnected += reason => Post(() => OnDisconnected(reason));
        }

        void SetupClientNet()
        {
            mode = Mode.Client;
            net = new NetLayer();
            session.Send = m => net?.SendToHost(m);
            net.RemoteConnected += () => Post(() =>
            {
                session.Begin(1);
                net?.SendToHost(Msg.Join("HELLO", myName.Replace("|", "")));
            });
            net.MessageFromHost += m => Post(() => session.Handle(m));
            net.Disconnected += reason => Post(() => OnDisconnected(reason));
        }

        void OnDisconnected(string reason)
        {
            // maç bittiyse sonuç ekranında kal, sadece bağlantıyı kapat
            if (session.IsFinished) { net?.Shutdown(); net = null; return; }
            BackToMenu(reason);
        }

        void ResetMatch()
        {
            inbox.Clear();
            session.Stop();
            if (net != null) { net.Shutdown(); net = null; }
            host = null; bot = null;
            matchStarted = false;
        }

        void BackToMenu(string status)
        {
            Time.timeScale = 1f;
            ui.ShowPause(false, false, "", Screen.fullScreen);
            ResetMatch();
            mode = Mode.Menu;
            connecting = false;
            ui.HidePlayback();
            ui.ShowMenu(status);
            ShowMenuMap();
        }

        void Post(Action a) => inbox.Enqueue(a);

        void Update()
        {
            // mesajları sırayla işle (iç içe çağrıları önler)
            int guard = 0;
            while (inbox.Count > 0 && guard++ < 200) inbox.Dequeue()?.Invoke();
            host?.Tick(Time.deltaTime);

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) TogglePause();
        }

        void OnApplicationQuit()
        {
            net?.Shutdown();
        }

        static string LocalIp()
        {
            try
            {
                foreach (var addr in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(addr))
                        return addr.ToString();
            }
            catch { }
            return "127.0.0.1";
        }
    }
}

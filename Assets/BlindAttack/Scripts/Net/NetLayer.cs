using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace BlindAttack.Net
{
    /// <summary>
    /// Netcode for GameObjects üzerinde çok basit bir mesajlaşma katmanı.
    /// Hiç NetworkObject kullanmıyoruz: iki oyuncu arasında sadece metin mesajları (named message) gidip geliyor.
    /// Oda sahibi (host) aynı zamanda oyuncu 1'dir.
    /// </summary>
    public class NetLayer
    {
        const string Channel = "BlindAttack";
        public const ushort LanPort = 7777;

        public event Action<string> MessageFromHost;   // istemcide: host'tan gelen mesaj
        public event Action<string> MessageFromClient; // host'ta: istemciden gelen mesaj
        public event Action RemoteConnected;           // host'ta: rakip bağlandı / istemcide: host'a bağlandık
        public event Action<string> Disconnected;

        public bool IsHost { get; private set; }
        public bool HasRemote => remoteClient != ulong.MaxValue;
        public bool IsRunning => manager != null && manager.IsListening;

        NetworkManager manager;
        ISession session;
        ulong remoteClient = ulong.MaxValue;
        bool handlerRegistered;

        // ---------------- Kurulum ----------------

        NetworkManager EnsureManager()
        {
            if (manager != null) return manager;
            var go = new GameObject("NetworkManager");
            var transport = go.AddComponent<UnityTransport>();
            manager = go.AddComponent<NetworkManager>();
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.SetSingleton(); // eski bir yönetici kapanıyor olsa bile bunu kullan
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.PlayerPrefab = null;
            manager.RunInBackground = true;

            manager.OnServerStarted += RegisterHandler;
            manager.OnClientStarted += RegisterHandler;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            return manager;
        }

        void RegisterHandler()
        {
            if (handlerRegistered || manager.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(Channel, OnNamedMessage);
            handlerRegistered = true;
        }

        void OnClientConnected(ulong clientId)
        {
            if (IsHost)
            {
                if (clientId == manager.LocalClientId) return;
                if (remoteClient != ulong.MaxValue)
                {
                    // oda dolu, üçüncü kişiyi at
                    manager.DisconnectClient(clientId);
                    return;
                }
                remoteClient = clientId;
                RemoteConnected?.Invoke();
            }
            else if (clientId == manager.LocalClientId)
            {
                RegisterHandler();
                RemoteConnected?.Invoke();
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (IsHost)
            {
                if (clientId != remoteClient) return;
                remoteClient = ulong.MaxValue;
                Disconnected?.Invoke("Rakip oyundan ayrıldı.");
            }
            else
            {
                string reason = manager != null && !string.IsNullOrEmpty(manager.DisconnectReason) ? manager.DisconnectReason : "Oda sahibiyle bağlantı koptu.";
                Disconnected?.Invoke(reason);
            }
        }

        void OnNamedMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string msg);
            if (IsHost) { if (sender == remoteClient) MessageFromClient?.Invoke(msg); }
            else MessageFromHost?.Invoke(msg);
        }

        // ---------------- Gönderme ----------------

        public void SendToClient(string msg) { if (IsHost && remoteClient != ulong.MaxValue) Send(remoteClient, msg); }
        public void SendToHost(string msg) { if (!IsHost) Send(NetworkManager.ServerClientId, msg); }

        void Send(ulong target, string msg)
        {
            if (!IsRunning || manager.CustomMessagingManager == null) return;
            var writer = new FastBufferWriter(256 + msg.Length * 2, Allocator.Temp, 1 << 20);
            try
            {
                writer.WriteValueSafe(msg);
                manager.CustomMessagingManager.SendNamedMessage(Channel, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            finally { writer.Dispose(); }
        }

        // ---------------- LAN / IP ----------------

        public bool StartLanHost()
        {
            var nm = EnsureManager();
            IsHost = true;
            ((UnityTransport)nm.NetworkConfig.NetworkTransport).SetConnectionData("0.0.0.0", LanPort, "0.0.0.0");
            return nm.StartHost();
        }

        public bool StartLanClient(string ip)
        {
            var nm = EnsureManager();
            IsHost = false;
            ((UnityTransport)nm.NetworkConfig.NetworkTransport).SetConnectionData(string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim(), LanPort);
            return nm.StartClient();
        }

        // ---------------- Online (Unity Relay, oda kodu) ----------------

        static async Task EnsureServicesAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                // Aynı bilgisayarda iki kopya açılırsa çakışmasın diye rastgele profil
                options.SetProfile("p" + UnityEngine.Random.Range(0, 1000000));
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>Oda kurar ve oda kodunu döner.</summary>
        public async Task<string> HostOnlineAsync()
        {
            await EnsureServicesAsync();
            EnsureManager();
            IsHost = true;
            var options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            RegisterHandler();
            return session.Code;
        }

        public async Task JoinOnlineAsync(string code)
        {
            await EnsureServicesAsync();
            EnsureManager();
            IsHost = false;
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
            RegisterHandler();
        }

        // ---------------- Kapatma ----------------

        public void Shutdown()
        {
            var s = session;
            var m = manager;
            session = null;
            manager = null;
            if (m != null)
            {
                if (handlerRegistered && m.CustomMessagingManager != null)
                    m.CustomMessagingManager.UnregisterNamedMessageHandler(Channel);
                m.OnServerStarted -= RegisterHandler;
                m.OnClientStarted -= RegisterHandler;
                m.OnClientConnectedCallback -= OnClientConnected;
                m.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            handlerRegistered = false;
            remoteClient = ulong.MaxValue;
            if (s != null) _ = LeaveThenDestroy(s, m);
            else DestroyManager(m);
        }

        static async Task LeaveThenDestroy(ISession s, NetworkManager m)
        {
            try { await s.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning("[BlindAttack] Oturumdan çıkış: " + e.Message); }
            DestroyManager(m);
        }

        static void DestroyManager(NetworkManager m)
        {
            if (m == null) return;
            if (m.IsListening) m.Shutdown();
            // her bağlantı için temiz bir NetworkManager oluşturuyoruz (Relay/LAN ayarları karışmasın)
            UnityEngine.Object.Destroy(m.gameObject);
        }

        /// <summary>Kullanıcıya gösterilecek anlaşılır hata mesajı.</summary>
        public static string Explain(Exception e)
        {
            string m = e.Message ?? "";
            if (m.Contains("project", StringComparison.OrdinalIgnoreCase) && (m.Contains("id", StringComparison.OrdinalIgnoreCase) || m.Contains("link", StringComparison.OrdinalIgnoreCase)))
                return "Proje Unity Cloud'a bağlı değil. Edit > Project Settings > Services'ten bağlayın (README'ye bakın) ya da LAN ile deneyin.";
            if (m.Contains("not found", StringComparison.OrdinalIgnoreCase) || m.Contains("404"))
                return "Oda bulunamadı. Kodu kontrol edin.";
            if (m.Contains("full", StringComparison.OrdinalIgnoreCase))
                return "Oda dolu.";
            return "Bağlantı hatası: " + m;
        }
    }
}

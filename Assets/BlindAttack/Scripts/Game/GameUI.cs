using System;
using System.Collections.Generic;
using BlindAttack.Core;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace BlindAttack.Game
{
    /// <summary>Tüm ekranlar (menü, bekleme, oyun içi HUD, tur/maç sonucu). Tamamen koddan kurulur.</summary>
    public class GameUI
    {
        // menü olayları
        public Action<string> OnHostOnline, OnSolo, OnLanHost;
        public Action<string, string> OnJoinOnline, OnLanJoin; // (isim, kod/ip)
        public Action OnCancel, OnReady, OnClearPlacements, OnBackToMenu, OnSkipPlayback, OnRematch;
        public Action OnResume, OnQuitGame, OnToggleFullscreen;
        public Action<float> OnSpeed;
        public Action<ItemType> OnSelectItem;

        GameObject menu, waiting, hud, palettePanel, playbackPanel, roundPanel, matchPanel, overlay;
        GameObject pauseRoot, pauseBox, helpBox, pauseMenuButton;
        GameObject territoryBanner;
        Text bannerTitle, bannerSub;
        CanvasGroup bannerGroup, vignetteGroup, menuGroup;
        Text pauseNote, fullscreenLabel, rematchNote;
        Button rematchButton;
        InputField nameInput, codeInput, ipInput;
        Text menuStatus, waitCode, waitInfo;
        Text phaseTitle, phaseSub, timerText, scoreText, mapText, goldText, hintText, overlayText;
        Text playLabel, playInfo, playStats, roundTitle, roundBody, matchTitle, matchBody;
        Image heroHpFill;
        Button readyButton, copyButton;
        Transform paletteRow;
        readonly List<RectTransform> topPanels = new List<RectTransform>();
        readonly List<(ItemType type, Image bg)> paletteButtons = new List<(ItemType, Image)>();
        int lastTimerSec = -1;
        int lastBudgetRemaining = -1;

        public string PlayerName => string.IsNullOrWhiteSpace(nameInput.text) ? "Oyuncu" : nameInput.text.Trim();

        public void Build()
        {
            UiKit.Init();
            var root = UiKit.Canvas.transform;
            BuildVignette(root);
            BuildMenu(root);
            BuildWaiting(root);
            BuildHud(root);
            BuildTerritoryBanner(root);
            BuildResults(root);
            BuildPause(root);
            ShowMenu("");
        }

        void BuildVignette(Transform root)
        {
            var go = new GameObject("TerritoryVignette");
            var rt = UiKit.Rect(go, root);
            UiKit.Stretch(rt);
            var img = go.AddComponent<Image>();
            const int sz = 32;
            var px = new Color[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float u = (x / (float)(sz - 1)) * 2f - 1f;
                    float v = (y / (float)(sz - 1)) * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                    float a = Mathf.SmoothStep(0f, 0.85f, Mathf.InverseLerp(0.5f, 1.15f, d));
                    px[y * sz + x] = new Color(0.78f, 0.32f, 0.16f, a);
                }
            var tex = Art.MakeTex(px, sz, sz, TextureWrapMode.Clamp);
            img.sprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
            img.raycastTarget = false;
            vignetteGroup = go.AddComponent<CanvasGroup>();
            vignetteGroup.alpha = 0f;
            vignetteGroup.blocksRaycasts = false;
        }

        void BuildTerritoryBanner(Transform root)
        {
            territoryBanner = UiKit.Box(root, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 130), UiKit.Parchment);
            territoryBanner.name = "TerritoryBanner";
            bannerGroup = territoryBanner.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            UiKit.VStack(territoryBanner, 6, 14);
            bannerTitle = UiKit.Title(territoryBanner.transform, "", 48, UiKit.WoodBtn);
            UiKit.Size(bannerTitle, 840, 56);
            bannerSub = UiKit.Label(territoryBanner.transform, "", 24, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Size(bannerSub, 840, 36);
            territoryBanner.SetActive(false);
        }

        // ================= MENÜ =================
        void BuildMenu(Transform root)
        {
            menu = new GameObject("Menu");
            UiKit.Stretch(UiKit.Rect(menu, root));
            menuGroup = menu.AddComponent<CanvasGroup>();

            var title = UiKit.Title(menu.transform, "BLIND ATTACK", 110, UiKit.Gold);
            var trt = title.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -60); trt.sizeDelta = new Vector2(1200, 140);
            title.GetComponent<Shadow>().effectDistance = new Vector2(4, -6);
            Tween.Custom(0f, 1f, 2.5f, k =>
            {
                if (trt) trt.anchoredPosition = new Vector2(0, -60f + Mathf.Sin(k * Mathf.PI * 2f) * 6f);
            }, cycles: -1, cycleMode: CycleMode.Restart);

            var sub = UiKit.Title(menu.transform, "Görmeden kur, körlemesine saldır!", 36, Color.white);
            var srt = sub.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0, -195); srt.sizeDelta = new Vector2(1200, 50);

            var box = UiKit.Box(menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(720, 700), UiKit.Parchment);
            UiKit.VStack(box, 16, 34);

            nameInput = UiKit.Input(box.transform, "Adın");
            UiKit.Size(nameInput, 640, 66);
            nameInput.characterLimit = 14;

            UiKit.Size(UiKit.Button(box.transform, "Oda Kur (Online)", UiKit.Green, () => OnHostOnline?.Invoke(PlayerName)), 640, 76);

            var joinRow = Row(box.transform, 640, 76);
            codeInput = UiKit.Input(joinRow, "Oda kodu");
            UiKit.Size(codeInput, 380, 76);
            codeInput.characterLimit = 12;
            UiKit.Size(UiKit.Button(joinRow, "Katıl", UiKit.Blue, () => OnJoinOnline?.Invoke(PlayerName, codeInput.text)), 244, 76);

            UiKit.Size(UiKit.Button(box.transform, "Bota Karşı Dene", UiKit.WoodBtn, () => OnSolo?.Invoke(PlayerName)), 640, 76);

            var lanLabel = UiKit.Label(box.transform, "Aynı ağda oynamak için (LAN)", 24, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.7f), TextAnchor.MiddleCenter);
            UiKit.Size(lanLabel, 640, 34);
            var lanRow = Row(box.transform, 640, 62);
            ipInput = UiKit.Input(lanRow, "IP (boş = bu PC)", 24);
            UiKit.Size(ipInput, 300, 62);
            UiKit.Size(UiKit.Button(lanRow, "LAN Kur", UiKit.WoodBtn, () => OnLanHost?.Invoke(PlayerName), 24), 160, 62);
            UiKit.Size(UiKit.Button(lanRow, "LAN Katıl", UiKit.WoodBtn, () => OnLanJoin?.Invoke(PlayerName, ipInput.text), 24), 160, 62);

            menuStatus = UiKit.Label(box.transform, "", 24, UiKit.Red, TextAnchor.MiddleCenter);
            UiKit.Size(menuStatus, 640, 70);
        }

        Transform Row(Transform parent, float w, float h)
        {
            var go = new GameObject("Row");
            UiKit.Rect(go, parent);
            var hs = UiKit.HStack(go, 16, 0);
            hs.childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(go.transform, w, h);
            return go.transform;
        }

        // ================= BEKLEME (oda kodu) =================
        void BuildWaiting(Transform root)
        {
            waiting = UiKit.Box(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 480), UiKit.Parchment);
            UiKit.VStack(waiting, 14, 36);
            UiKit.Size(UiKit.Label(waiting.transform, "Oda kodu", 30, UiKit.Ink, TextAnchor.MiddleCenter), 620, 40);
            waitCode = UiKit.Title(waiting.transform, "------", 84, UiKit.WoodBtn);
            UiKit.Size(waitCode, 620, 110);
            copyButton = UiKit.Button(waiting.transform, "Kodu Kopyala", UiKit.Blue, () => { GUIUtility.systemCopyBuffer = waitCode.text; UiKit.Toast("Kopyalandı"); });
            UiKit.Size(copyButton, 620, 66);
            waitInfo = UiKit.Label(waiting.transform, "Rakip bekleniyor...", 28, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Size(waitInfo, 620, 70);
            UiKit.Size(UiKit.Button(waiting.transform, "İptal", UiKit.Red, () => OnCancel?.Invoke()), 620, 66);
        }

        // ================= OYUN İÇİ =================
        void BuildHud(Transform root)
        {
            hud = new GameObject("HUD");
            UiKit.Stretch(UiKit.Rect(hud, root));

            // üst orta: aşama
            var top = UiKit.Box(hud.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(900, 100), UiKit.Parchment);
            topPanels.Add((RectTransform)top.transform);
            phaseTitle = UiKit.Title(top.transform, "", 36, UiKit.Ink);
            Place(phaseTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -8), new Vector2(-140, 46));
            phaseTitle.alignment = TextAnchor.MiddleLeft;
            phaseSub = UiKit.Label(top.transform, "", 22, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.85f));
            Place(phaseSub.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -52), new Vector2(-140, 42));
            var timerBox = UiKit.Panel(top.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), UiKit.WoodBtn);
            var trt = (RectTransform)timerBox.transform;
            trt.pivot = new Vector2(1, 0.5f); trt.anchoredPosition = new Vector2(-14, 0); trt.sizeDelta = new Vector2(100, 74);
            timerText = UiKit.Title(timerBox.transform, "", 40, Color.white);
            UiKit.Stretch(timerText.rectTransform);

            // sol üst: skor
            var score = UiKit.Box(hud.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(380, 100), UiKit.Parchment);
            topPanels.Add((RectTransform)score.transform);
            scoreText = UiKit.Label(score.transform, "", 24, UiKit.Ink);
            UiKit.Stretch(scoreText.rectTransform, 18);
            scoreText.supportRichText = true;

            // sağ üst: harita
            var mapBox = UiKit.Box(hud.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(380, 100), UiKit.Parchment);
            topPanels.Add((RectTransform)mapBox.transform);
            mapText = UiKit.Label(mapBox.transform, "", 24, UiKit.Ink, TextAnchor.MiddleRight);
            UiKit.Stretch(mapText.rectTransform, 18);
            mapText.supportRichText = true;

            // alt: palet
            palettePanel = UiKit.Box(hud.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(1560, 182), UiKit.Parchment);
            hintText = UiKit.Label(palettePanel.transform, "", 22, UiKit.Ink, TextAnchor.MiddleCenter);
            hintText.supportRichText = true;
            Place(hintText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -4), new Vector2(0, 34));
            var rowGo = new GameObject("PaletteRow");
            var rrt = UiKit.Rect(rowGo, palettePanel.transform);
            rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 1);
            rrt.offsetMin = new Vector2(18, 12); rrt.offsetMax = new Vector2(-330, -40);
            UiKit.HStack(rowGo, 12, 0, TextAnchor.MiddleLeft);
            paletteRow = rowGo.transform;

            goldText = UiKit.Title(palettePanel.transform, "", 30, UiKit.WoodBtn, TextAnchor.MiddleCenter);
            Place(goldText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-165, -62), new Vector2(300, 40), new Vector2(0.5f, 0.5f));
            readyButton = UiKit.Button(palettePanel.transform, "HAZIRIM", UiKit.Green, () => OnReady?.Invoke(), 30);
            Place((RectTransform)readyButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 18), new Vector2(200, 70), new Vector2(1, 0));
            var clear = UiKit.Button(palettePanel.transform, "Temizle", UiKit.Red, () => OnClearPlacements?.Invoke(), 24);
            Place((RectTransform)clear.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-232, 18), new Vector2(100, 70), new Vector2(1, 0));

            // alt: izleme çubuğu
            playbackPanel = UiKit.Box(hud.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(1300, 130), UiKit.Parchment);
            playLabel = UiKit.Title(playbackPanel.transform, "", 32, UiKit.Ink, TextAnchor.MiddleLeft);
            Place(playLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -12), new Vector2(560, 44), new Vector2(0, 1));
            var hpBg = UiKit.Panel(playbackPanel.transform, new Vector2(0, 0), new Vector2(0, 0), new Color(0.25f, 0.15f, 0.1f));
            Place((RectTransform)hpBg.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 22), new Vector2(420, 34), new Vector2(0, 0));
            var hpFillGo = UiKit.Panel(hpBg.transform, Vector2.zero, Vector2.one, new Color(0.35f, 0.8f, 0.35f));
            var hfr = (RectTransform)hpFillGo.transform; hfr.offsetMin = new Vector2(4, 4); hfr.offsetMax = new Vector2(-4, -4);
            hfr.pivot = new Vector2(0, 0.5f);
            heroHpFill = hpFillGo.GetComponent<Image>();
            playInfo = UiKit.Label(playbackPanel.transform, "", 24, UiKit.Ink);
            Place(playInfo.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(600, -16), new Vector2(380, 40), new Vector2(0, 1));
            playStats = UiKit.Label(playbackPanel.transform, "", 24, UiKit.Ink);
            playStats.supportRichText = true;
            Place(playStats.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(470, 18), new Vector2(520, 42), new Vector2(0, 0));
            var speedRow = new GameObject("Speed");
            var srt2 = UiKit.Rect(speedRow, playbackPanel.transform);
            srt2.anchorMin = srt2.anchorMax = new Vector2(1, 0.5f); srt2.pivot = new Vector2(1, 0.5f);
            srt2.anchoredPosition = new Vector2(-20, 0); srt2.sizeDelta = new Vector2(290, 70);
            UiKit.HStack(speedRow, 10, 0);
            UiKit.Size(UiKit.Button(speedRow.transform, "1x", UiKit.WoodBtn, () => OnSpeed?.Invoke(1f), 26), 80, 64);
            UiKit.Size(UiKit.Button(speedRow.transform, "2x", UiKit.WoodBtn, () => OnSpeed?.Invoke(2.5f), 26), 80, 64);
            UiKit.Size(UiKit.Button(speedRow.transform, "Atla", UiKit.Red, () => OnSkipPlayback?.Invoke(), 26), 100, 64);

            // bekleme yazısı
            overlay = UiKit.Box(hud.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -128), new Vector2(640, 96), new Color(0.2f, 0.12f, 0.07f, 0.85f));
            overlay.GetComponent<Image>().raycastTarget = false;
            overlayText = UiKit.Title(overlay.transform, "", 34, Color.white);
            UiKit.Stretch(overlayText.rectTransform, 10);
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // ================= SONUÇLAR =================
        void BuildResults(Transform root)
        {
            roundPanel = UiKit.Box(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(760, 380), UiKit.Parchment);
            UiKit.VStack(roundPanel, 12, 36);
            roundTitle = UiKit.Title(roundPanel.transform, "", 54, UiKit.WoodBtn);
            UiKit.Size(roundTitle, 680, 80);
            roundBody = UiKit.Label(roundPanel.transform, "", 30, UiKit.Ink, TextAnchor.MiddleCenter);
            roundBody.supportRichText = true;
            UiKit.Size(roundBody, 680, 220);

            matchPanel = UiKit.Box(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, 640), UiKit.Parchment);
            UiKit.VStack(matchPanel, 14, 40);
            matchTitle = UiKit.Title(matchPanel.transform, "", 90, UiKit.Gold);
            UiKit.Size(matchTitle, 700, 130);
            matchBody = UiKit.Label(matchPanel.transform, "", 32, UiKit.Ink, TextAnchor.MiddleCenter);
            matchBody.supportRichText = true;
            UiKit.Size(matchBody, 700, 200);
            rematchButton = UiKit.Button(matchPanel.transform, "Rövanş (sıradaki harita)", UiKit.Green, () => OnRematch?.Invoke());
            UiKit.Size(rematchButton, 700, 80);
            rematchNote = UiKit.Label(matchPanel.transform, "Oda sahibi rövanşı başlatabilir...", 24, UiKit.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.Size(rematchNote, 700, 40);
            UiKit.Size(UiKit.Button(matchPanel.transform, "Ana Menü", UiKit.WoodBtn, () => OnBackToMenu?.Invoke()), 700, 72);
        }

        // ================= ESC MENÜSÜ =================
        void BuildPause(Transform root)
        {
            pauseRoot = UiKit.Panel(root, Vector2.zero, Vector2.one, new Color(0.08f, 0.05f, 0.03f, 0.55f));
            pauseRoot.name = "ESC Menüsü";
            pauseRoot.GetComponent<Image>().sprite = null;

            pauseBox = UiKit.Box(pauseRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 640), UiKit.Parchment);
            UiKit.VStack(pauseBox, 16, 36);
            UiKit.Size(UiKit.Title(pauseBox.transform, "DURAKLATILDI", 56, UiKit.WoodBtn), 548, 80);
            pauseNote = UiKit.Label(pauseBox.transform, "", 22, UiKit.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.Size(pauseNote, 548, 34);
            UiKit.Size(UiKit.Button(pauseBox.transform, "Devam Et", UiKit.Green, () => OnResume?.Invoke()), 548, 72);
            UiKit.Size(UiKit.Button(pauseBox.transform, "Nasıl Oynanır?", UiKit.Blue, () => ShowHelp(true)), 548, 72);
            var fs = UiKit.Button(pauseBox.transform, "", UiKit.WoodBtn, () => OnToggleFullscreen?.Invoke());
            fullscreenLabel = fs.GetComponentInChildren<Text>();
            UiKit.Size(fs, 548, 72);
            var back = UiKit.Button(pauseBox.transform, "Ana Menüye Dön", UiKit.WoodBtn, () => OnBackToMenu?.Invoke());
            UiKit.Size(back, 548, 72);
            pauseMenuButton = back.gameObject;
            UiKit.Size(UiKit.Button(pauseBox.transform, "Oyundan Çık", UiKit.Red, () => OnQuitGame?.Invoke()), 548, 72);

            helpBox = UiKit.Box(pauseRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 820), UiKit.Parchment);
            UiKit.VStack(helpBox, 10, 36);
            UiKit.Size(UiKit.Title(helpBox.transform, "NASIL OYNANIR?", 50, UiKit.WoodBtn), 1100, 70);
            var help = UiKit.Label(helpBox.transform,
                "<b>Amaç:</b> Kahramanını rakibin savunduğu haritada çıkış kapısına ulaştırmak. İkiniz de aynı haritada oynarsınız.\n\n" +
                "<b>1. Savunma:</b> Haritaya Goblin, Okçu, Dev ve gizli Diken Tuzağı koy. Rakibin bunları <b>göremeyecek</b>.\n\n" +
                "<b>2. Saldırı hazırlığı:</b> Rakibin düşmanları gizli! Kahramanını mavi bölgeye koy, yoluna kutu ve bayrak bırak.\n" +
                "Kahraman <b>numaralı sırayla</b> en yakın hedefe gider, en son çıkışa koşar. Rota haritada noktalı çizgiyle görünür.\n\n" +
                "<b>Kutular:</b> Kılıç +6 saldırı (alev alır) · Zırh %30 az hasar · Çizme %30 hız · İksir +45 can · Bomba etrafa 40 hasar · Bayrak ücretsiz rota noktası\n\n" +
                "<b>3. Saldırı:</b> İki saldırı sırayla izlenir. Çıkışa ulaşan 100 + kalan can puan alır, ulaşamayan ne kadar yaklaştıysa o kadar.\n" +
                "Turu yüksek puanlı alır. 3 turda en çok turu alan maçı kazanır. Her maç farklı haritada oynanır.\n\n" +
                "<b>Kontroller:</b> Sol tık koy · Sağ tık kaldır · Sayı tuşları seç · WASD / orta tuş kaydır · Tekerlek yakınlaş · Space ortala · ESC menü",
                25, UiKit.Ink, TextAnchor.UpperLeft);
            help.supportRichText = true;
            UiKit.Size(help, 1100, 580);
            UiKit.Size(UiKit.Button(helpBox.transform, "Geri", UiKit.Green, () => ShowHelp(false)), 1100, 70);

            pauseRoot.SetActive(false);
        }

        void ShowHelp(bool show)
        {
            helpBox.SetActive(show);
            pauseBox.SetActive(!show);
        }

        public bool PauseOpen => pauseRoot.activeSelf;

        /// <summary>ESC menüsü. inMatch=false iken (ana menüde) "Ana Menüye Dön" gizlenir.</summary>
        public void ShowPause(bool show, bool inMatch, string note, bool fullscreen)
        {
            pauseRoot.SetActive(show);
            if (!show) return;
            pauseRoot.transform.SetAsLastSibling();
            ShowHelp(false);
            pauseMenuButton.SetActive(inMatch);
            pauseNote.text = note;
            SetFullscreenLabel(fullscreen);
        }

        /// <summary>ESC'ye basıldığında: yardım açıksa onu kapatır, true döner.</summary>
        public bool CloseHelpIfOpen()
        {
            if (!pauseRoot.activeSelf || !helpBox.activeSelf) return false;
            ShowHelp(false);
            return true;
        }

        public void SetFullscreenLabel(bool fullscreen) => fullscreenLabel.text = fullscreen ? "Pencere Modu" : "Tam Ekran";

        // ================= Ekran geçişleri =================
        void HideAll()
        {
            menu.SetActive(false); waiting.SetActive(false); hud.SetActive(false);
            roundPanel.SetActive(false); matchPanel.SetActive(false);
        }

        public void ShowMenu(string status)
        {
            HideAll();
            SetTerritory(false);
            menu.SetActive(true);
            if (menuGroup)
            {
                menuGroup.alpha = 1f;
                menu.transform.localScale = Vector3.one;
            }
            menuStatus.text = status;
            menuStatus.color = UiKit.Red;
        }

        public void SetMenuStatus(string status, bool error)
        {
            menuStatus.text = status;
            menuStatus.color = error ? UiKit.Red : UiKit.Ink;
        }

        public void ShowWaiting(string code, string info, bool showCode)
        {
            HideAll();
            waiting.SetActive(true);
            waitCode.text = code;
            waitCode.gameObject.SetActive(showCode);
            copyButton.gameObject.SetActive(showCode);
            waitInfo.text = info;
        }

        public void ShowHud()
        {
            HideAll();
            hud.SetActive(true);
            SetOverlay(null);
        }

        public void SetPhase(string title, string sub) { phaseTitle.text = title; phaseSub.text = sub; }

        public void SetTimer(float seconds)
        {
            if (seconds < 0f)
            {
                timerText.text = "";
                timerText.color = Color.white;
                lastTimerSec = -1;
                return;
            }
            int s = Mathf.CeilToInt(Mathf.Max(0, seconds));
            timerText.text = s.ToString();
            if (s <= 5 && s > 0 && s != lastTimerSec)
            {
                lastTimerSec = s;
                timerText.color = UiKit.Red;
                Tween.PunchScale(timerText.transform, new Vector3(0.35f, 0.35f, 0f), 0.25f);
                AudioManager.Play(SoundId.UrgentTick, 0.9f);
            }
            else if (s > 5)
            {
                timerText.color = Color.white;
                lastTimerSec = s;
            }
        }

        public void SetScore(string text) => scoreText.text = text;
        public void SetMapInfo(string text) => mapText.text = text;

        public void ShowTerritoryBanner(string title, string sub, bool hostile)
        {
            if (territoryBanner == null) return;
            territoryBanner.SetActive(true);
            territoryBanner.transform.SetAsLastSibling();
            var bgImg = territoryBanner.GetComponent<Image>();
            if (bgImg) bgImg.color = hostile ? new Color(0.24f, 0.08f, 0.06f, 0.95f) : UiKit.Parchment;
            bannerTitle.text = title;
            bannerTitle.color = hostile ? new Color(1f, 0.40f, 0.35f) : UiKit.Gold;
            bannerSub.text = sub;
            bannerSub.color = hostile ? new Color(1f, 0.88f, 0.85f, 0.9f) : UiKit.Ink;

            var rt = (RectTransform)territoryBanner.transform;
            rt.localScale = Vector3.one * 0.7f;
            Tween.StopAll(territoryBanner.transform);
            Tween.StopAll(bannerGroup);
            bannerGroup.alpha = 1f;
            Tween.Scale(territoryBanner.transform, Vector3.one, 0.35f, Ease.OutBack);
            AudioManager.Play(SoundId.BannerWhoosh, 0.8f);
            AudioManager.Play(SoundId.BannerGong, 0.65f);

            Tween.Delay(1.35f).OnComplete(() =>
            {
                if (bannerGroup) Tween.Alpha(bannerGroup, 0f, 0.32f, Ease.InQuad).OnComplete(() => territoryBanner.SetActive(false));
            });
        }

        public void SetTerritory(bool hostile)
        {
            if (vignetteGroup == null) return;
            Tween.StopAll(vignetteGroup);
            Tween.Alpha(vignetteGroup, hostile ? 0.22f : 0f, 0.45f, Ease.OutQuad);
        }

        public void SetOverlay(string text)
        {
            overlay.SetActive(!string.IsNullOrEmpty(text));
            overlayText.text = text ?? "";
        }

        public void ShowPalette(PlacementController pc)
        {
            palettePanel.SetActive(pc != null);
            playbackPanel.SetActive(false);
            if (pc == null) return;
            foreach (Transform c in paletteRow) UnityEngine.Object.Destroy(c.gameObject);
            paletteButtons.Clear();
            int i = 0;
            for (int pIdx = 0; pIdx < pc.Palette.Length; pIdx++)
            {
                var t = pc.Palette[pIdx];
                var def = Items.Get(t);
                var type = t;
                int key = ++i;
                var b = UiKit.Button(paletteRow, "", UiKit.ParchmentDark, () => OnSelectItem?.Invoke(type));
                UiKit.Size(b, pc.Palette.Length > 5 ? 152 : 230, 120);
                var label = b.GetComponentInChildren<Text>();
                label.text = def.Name;
                label.color = UiKit.Ink;
                label.fontSize = pc.Palette.Length > 5 ? 21 : 24;
                label.GetComponent<Shadow>().enabled = false;
                label.alignment = TextAnchor.UpperCenter;
                label.rectTransform.offsetMin = new Vector2(6, 6); label.rectTransform.offsetMax = new Vector2(-6, -60);
                var icon = new GameObject("Icon");
                var irt = UiKit.Rect(icon, b.transform);
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f); irt.pivot = new Vector2(0.5f, 1f);
                irt.anchoredPosition = new Vector2(0, -10); irt.sizeDelta = new Vector2(48, 48);
                var img = icon.AddComponent<Image>(); img.sprite = Art.Circle; img.color = Visuals.IconColor(t); img.raycastTarget = false;
                var keyT = UiKit.Label(icon.transform, key.ToString(), 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(keyT.rectTransform);
                var cost = UiKit.Label(b.transform, def.Cost == 0 ? "ücretsiz" : def.Cost + " altın", 20, UiKit.WoodBtn, TextAnchor.LowerCenter, FontStyle.Bold);
                cost.rectTransform.anchorMin = Vector2.zero; cost.rectTransform.anchorMax = new Vector2(1, 0);
                cost.rectTransform.pivot = new Vector2(0.5f, 0); cost.rectTransform.anchoredPosition = new Vector2(0, 8); cost.rectTransform.sizeDelta = new Vector2(0, 30);
                paletteButtons.Add((t, b.GetComponent<Image>()));

                b.transform.localScale = Vector3.zero;
                Tween.Scale(b.transform, Vector3.one, 0.22f, Ease.OutBack, startDelay: pIdx * 0.04f);
            }
            RefreshPalette(pc, false);
        }

        public void RefreshPalette(PlacementController pc, bool ready)
        {
            if (pc == null) return;
            foreach (var (type, bg) in paletteButtons)
            {
                var o = bg.GetComponent<UnityEngine.UI.Outline>();
                bool sel = type == pc.Selected;
                o.effectColor = sel ? UiKit.Gold : UiKit.Outline;
                o.effectDistance = sel ? new Vector2(5, -5) : new Vector2(2, -2);
                bg.transform.localScale = sel ? Vector3.one * 1.04f : Vector3.one;
            }
            int remaining = pc.Budget - pc.Spent;
            if (lastBudgetRemaining >= 0 && remaining != lastBudgetRemaining)
            {
                Tween.PunchScale(goldText.transform, new Vector3(0.18f, 0.18f, 0f), 0.2f);
            }
            lastBudgetRemaining = remaining;
            goldText.text = $"Altın: {remaining} / {pc.Budget}";
            readyButton.interactable = !ready;
            readyButton.GetComponentInChildren<Text>().text = ready ? "BEKLENİYOR" : "HAZIRIM";
        }

        public void SetHint(string text) => hintText.text = text;

        public void ShowPlayback(string label)
        {
            palettePanel.SetActive(false);
            playbackPanel.SetActive(true);
            playLabel.text = label;
            playInfo.text = "";
            heroHpFill.rectTransform.anchorMax = Vector2.one;
        }

        public void SetPlaybackStats(string text) => playStats.text = text;

        /// <summary>Arayüzün ekranın üstünde ve altında kapladığı piksel yükseklikleri (kamera haritayı aradaki boşluğa sığdırır).</summary>
        public Vector2 ScreenInsets()
        {
            if (hud == null || !hud.activeInHierarchy) return Vector2.zero;
            var corners = new Vector3[4];
            float top = 0f, bottom = 0f;
            foreach (var rt in topPanels)
            {
                if (!rt.gameObject.activeInHierarchy) continue;
                rt.GetWorldCorners(corners);
                top = Mathf.Max(top, Screen.height - corners[0].y);
            }
            foreach (var go in new[] { palettePanel, playbackPanel })
            {
                if (!go.activeInHierarchy) continue;
                ((RectTransform)go.transform).GetWorldCorners(corners);
                bottom = Mathf.Max(bottom, corners[1].y);
            }
            return new Vector2(top + 8f, bottom + 8f);
        }

        public void UpdatePlayback(float hpFraction, string info)
        {
            var rt = heroHpFill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(hpFraction), 1f);
            heroHpFill.color = Color.Lerp(new Color(0.85f, 0.25f, 0.2f), new Color(0.35f, 0.8f, 0.35f), hpFraction);
            playInfo.text = info;
        }

        public void HidePlayback() => playbackPanel.SetActive(false);

        public void ShowRound(string title, string body, bool won = true)
        {
            roundPanel.SetActive(true);
            roundPanel.transform.localScale = Vector3.one * 0.75f;
            Tween.Scale(roundPanel.transform, Vector3.one, 0.32f, Ease.OutBack);
            roundTitle.text = title;
            roundBody.text = body;
            AudioManager.Play(won ? SoundId.RoundWin : SoundId.RoundLoss);
        }

        public void HideRound() => roundPanel.SetActive(false);

        string matchBodyBase = "";

        public void ShowMatch(string title, Color color, bool canRematch, string body, bool won = true)
        {
            rematchButton.gameObject.SetActive(canRematch);
            rematchNote.gameObject.SetActive(!canRematch);
            roundPanel.SetActive(false);
            palettePanel.SetActive(false);
            playbackPanel.SetActive(false);
            SetOverlay(null);
            matchPanel.SetActive(true);
            matchPanel.transform.localScale = Vector3.one * 0.75f;
            Tween.Scale(matchPanel.transform, Vector3.one, 0.35f, Ease.OutBack);
            matchTitle.text = title;
            matchTitle.color = color;
            matchBodyBase = body;
            matchBody.text = body;
            AudioManager.Play(won ? SoundId.VictoryFanfare : SoundId.RoundLoss);
        }

        public void UpdateMatchCountdown(int secondsLeft)
        {
            if (matchBody != null && matchPanel != null && matchPanel.activeSelf)
                matchBody.text = matchBodyBase + $"\n\n<size=22><color=#D4AF37><b>{secondsLeft}</b> saniye sonra menüye dönülüyor...</color></size>";
        }
    }
}

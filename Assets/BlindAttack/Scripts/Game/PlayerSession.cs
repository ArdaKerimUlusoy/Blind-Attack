using System;
using System.Collections.Generic;
using BlindAttack.Core;
using BlindAttack.Match;
using UnityEngine;

namespace BlindAttack.Game
{
    public enum LocalPhase { None, Defense, Supply, Watching, RoundEnd, Finished }

    /// <summary>
    /// Bu bilgisayardaki oyuncunun gözünden maç: host'tan gelen mesajlara göre ekranları ve sahneyi yönetir,
    /// oyuncunun yerleştirmelerini host'a gönderir.
    /// </summary>
    public class PlayerSession : MonoBehaviour
    {
        public Action<string> Send;   // host'a mesaj
        public int Me { get; private set; }
        public bool IsFinished => phase == LocalPhase.Finished;
        /// <summary>Bu oyuncu rövanş başlatabilir mi (oda sahibi ya da tek kişilik mod).</summary>
        public Func<bool> CanRematch;

        GameUI ui;
        MapView view;
        IsoCamera cam;
        PlacementController placer;
        Playback playback;
        RoutePreview route;

        LocalPhase phase = LocalPhase.None;
        int round, rounds = MatchRules.Rounds;
        readonly int[] maps = new int[2];
        readonly string[] names = { "", "" };
        int[] wins = new int[2], totals = new int[2];
        float timer = -1f;
        bool submitted;
        List<Placement> lastDefense = new List<Placement>();
        List<Placement> lastSupply = new List<Placement>();
        int watchingAttacker, hostScore;

        public void Init(GameUI ui, MapView view, IsoCamera cam, PlacementController placer, Playback playback, RoutePreview route)
        {
            this.ui = ui; this.view = view; this.cam = cam; this.placer = placer; this.playback = playback; this.route = route;
            placer.Changed += () =>
            {
                ui.RefreshPalette(placer, submitted);
                if (phase == LocalPhase.Supply) route.Show(view, placer.Placed);
            };
            playback.Finished += OnPlaybackFinished;
            ui.OnReady = Submit;
            ui.OnClearPlacements = () => { if (!submitted) placer.ClearAll(); };
            ui.OnSelectItem = t => placer.Select(t);
            ui.OnSpeed = s => playback.Speed = s;
            ui.OnSkipPlayback = () => playback.SkipToEnd();
        }

        public void Begin(int me)
        {
            Me = me;
            phase = LocalPhase.None;
            wins = new int[2]; totals = new int[2];
            lastDefense.Clear(); lastSupply.Clear();
            ui.ShowHud();
            ui.ShowPalette(null);
            ui.HidePlayback();
            ui.SetPhase("Maç hazırlanıyor...", "");
            ui.SetTimer(-1);
            ui.SetOverlay("Rakip bekleniyor...");
        }

        public void Stop()
        {
            phase = LocalPhase.None;
            placer.End();
            playback.Stop();
            route.Clear();
        }

        string Them => names[1 - Me];

        public void Handle(string msg)
        {
            var p = Msg.Split(msg);
            switch (p[0])
            {
                case "START":
                    // yeni maç (rövanş dahil): her şeyi sıfırla
                    wins = new int[2]; totals = new int[2];
                    lastDefense.Clear(); lastSupply.Clear();
                    placer.End(); playback.Stop(); route.Clear();
                    phase = LocalPhase.None;
                    ui.ShowHud();
                    ui.ShowPalette(null);
                    ui.HidePlayback();
                    maps[0] = Msg.Int(p[1]); maps[1] = Msg.Int(p[2]);
                    UiKit.Toast("Harita: " + MapLibrary.Create(maps[0]).Name, 3f);
                    rounds = Msg.Int(p[3]);
                    names[0] = p.Length > 4 ? p[4] : "Oyuncu 1";
                    names[1] = p.Length > 5 ? p[5] : "Oyuncu 2";
                    ui.SetOverlay(null);
                    UpdateScore();
                    break;

                case "PHASE":
                    round = Msg.Int(p[2]);
                    float secs = Msg.Int(p[3]);
                    if (p[1] == "DEF") BeginDefense(secs);
                    else BeginSupply(secs);
                    break;

                case "ATTACK":
                    BeginWatch(Msg.Int(p[1]), Msg.Int(p[3]), p[4], p[5], Msg.Int(p[6]));
                    break;

                case "ROUND":
                {
                    int r = Msg.Int(p[1]);
                    int s0 = Msg.Int(p[2]), s1 = Msg.Int(p[3]);
                    wins[0] = Msg.Int(p[4]); wins[1] = Msg.Int(p[5]);
                    totals[0] = Msg.Int(p[6]); totals[1] = Msg.Int(p[7]);
                    phase = LocalPhase.RoundEnd;
                    timer = -1f;
                    ui.SetTimer(-1);
                    ui.HidePlayback();
                    ui.SetOverlay(null);
                    int mine = Me == 0 ? s0 : s1, theirs = Me == 0 ? s1 : s0;
                    string verdict = mine > theirs ? "<color=#4E8A2E><b>Bu turu sen aldın!</b></color>"
                                   : mine < theirs ? "<color=#B5452F><b>Bu turu rakip aldı.</b></color>"
                                   : "<b>Berabere!</b>";
                    ui.ShowRound($"TUR {r} SONUCU",
                        $"Senin saldırın: <b>{mine}</b> puan\n{Them}: <b>{theirs}</b> puan\n\n{verdict}\n<size=22>Çıkışa ulaşmak = 100 + kalan can. Ulaşamazsan ilerleme yüzdesi.</size>");
                    UpdateScore();
                    break;
                }

                case "MATCH":
                {
                    int winner = Msg.Int(p[1]);
                    wins[0] = Msg.Int(p[2]); wins[1] = Msg.Int(p[3]);
                    totals[0] = Msg.Int(p[4]); totals[1] = Msg.Int(p[5]);
                    phase = LocalPhase.Finished;
                    timer = -1f;
                    placer.End();
                    UpdateScore();
                    string title = winner == Me ? "KAZANDIN!" : winner < 0 ? "BERABERE" : "KAYBETTİN";
                    var color = winner == Me ? UiKit.Gold : winner < 0 ? UiKit.Blue : UiKit.Red;
                    ui.ShowMatch(title, color, CanRematch != null && CanRematch(),
                        $"Tur galibiyeti: <b>{wins[Me]}</b> - <b>{wins[1 - Me]}</b>\nToplam puan: <b>{totals[Me]}</b> - <b>{totals[1 - Me]}</b>\n\n<size=24>Rakip: {Them}</size>");
                    break;
                }
            }
        }

        void BeginDefense(float secs)
        {
            phase = LocalPhase.Defense;
            submitted = false;
            timer = secs;
            ui.HideRound();
            ui.SetOverlay(null);
            view.Build(maps[Me]);
            cam.Frame(view.WorldBounds);
            placer.Begin(view, cam.Cam, true, lastDefense);
            ui.ShowPalette(placer);
            ui.SetPhase($"TUR {round}/{rounds} · SAVUNMA", "Haritaya düşman ve gizli tuzak koy. Rakibin kahramanı bunları görmeden gelecek!");
            ui.SetMapInfo($"<b>{view.Map.Name}</b>\nSenin savunman");
            route.Clear();
        }

        void BeginSupply(float secs)
        {
            if (phase == LocalPhase.Defense && !submitted) Submit();
            phase = LocalPhase.Supply;
            submitted = false;
            timer = secs;
            ui.SetOverlay(null);
            view.Build(maps[1 - Me]);
            cam.Frame(view.WorldBounds);
            var initial = new List<Placement>(lastSupply);
            if (!MatchRules.HasHero(initial)) initial.Insert(0, new Placement(ItemType.Hero, MatchRules.DefaultHeroCell(view.Map)));
            placer.Begin(view, cam.Cam, false, initial);
            placer.Select(ItemType.Hero);
            ui.ShowPalette(placer);
            route.Show(view, placer.Placed);
            ui.SetPhase($"TUR {round}/{rounds} · SALDIRI HAZIRLIĞI", "Kahramanını mavi bölgeye koy, yoluna kutu ve bayrak bırak. Rakibin düşmanları gizli!");
            ui.SetMapInfo($"<b>{view.Map.Name}</b>\n{Them} savunuyor · <color=#B5452F>düşmanlar gizli</color>");
        }

        void BeginWatch(int attacker, int mapIndex, string defCsv, string supCsv, int score)
        {
            if ((phase == LocalPhase.Defense || phase == LocalPhase.Supply) && !submitted) Submit();
            placer.End();
            phase = LocalPhase.Watching;
            timer = -1f;
            ui.SetTimer(-1);
            ui.SetOverlay(null);
            ui.HideRound();
            watchingAttacker = attacker;
            hostScore = score;

            view.Build(mapIndex);
            cam.Frame(view.WorldBounds);
            var defenders = PlacementCodec.Decode(defCsv);
            var supplies = PlacementCodec.Decode(supCsv);
            // Simülasyonu burada da çalıştırıyoruz; deterministik olduğu için host'takiyle aynı sonucu verir
            var result = AttackSimulation.Run(view.Map, defenders, supplies);
            route.Show(view, supplies, 0.45f);
            if (result.Score != score) Debug.LogWarning($"[BlindAttack] Yerel simülasyon skoru ({result.Score}) host'tan farklı ({score}). Host skoru geçerli.");

            bool mine = attacker == Me;
            ui.SetPhase($"TUR {round}/{rounds} · SALDIRI",
                mine ? "Senin kahramanın rakibin haritasında! Neyle karşılaşacağını ilk kez görüyorsun." : $"{Them} senin haritana saldırıyor. Tuzakların işe yarayacak mı?");
            ui.SetMapInfo(mine ? $"<b>{view.Map.Name}</b>\n{Them} savunuyor" : $"<b>{view.Map.Name}</b>\nSen savunuyorsun");
            ui.ShowPlayback(mine ? "Senin saldırın" : $"{Them} saldırıyor");
            playback.Play(result, view, cam, !mine);
        }

        void OnPlaybackFinished()
        {
            if (phase != LocalPhase.Watching) return;
            Send?.Invoke(Msg.Join("DONE", round, watchingAttacker));
            bool mine = watchingAttacker == Me;
            ui.SetOverlay((mine ? "Saldırı puanın: " : "Rakibin puanı: ") + hostScore + (watchingAttacker == 0 ? "\nSıradaki saldırı hazırlanıyor..." : ""));
        }

        void Submit()
        {
            if (submitted) return;
            if (phase != LocalPhase.Defense && phase != LocalPhase.Supply) return;
            submitted = true;
            var list = new List<Placement>(placer.Placed);
            if (phase == LocalPhase.Defense)
            {
                lastDefense = list;
                Send?.Invoke(Msg.Join("DEF", round, PlacementCodec.Encode(list)));
            }
            else
            {
                lastSupply = list;
                Send?.Invoke(Msg.Join("SUP", round, PlacementCodec.Encode(list)));
            }
            ui.RefreshPalette(placer, true);
            ui.SetOverlay("Rakip bekleniyor...");
        }

        void UpdateScore()
        {
            ui.SetScore($"<b>{Shorten(names[Me])}</b> (sen): {wins[Me]} tur · {totals[Me]} puan\n<b>{Shorten(Them)}</b>: {wins[1 - Me]} tur · {totals[1 - Me]} puan");
        }

        static string Shorten(string s) => string.IsNullOrEmpty(s) ? "?" : (s.Length > 12 ? s.Substring(0, 12) : s);

        void Update()
        {
            if (phase == LocalPhase.Defense || phase == LocalPhase.Supply)
            {
                if (timer >= 0f)
                {
                    timer -= Time.deltaTime;
                    ui.SetTimer(timer);
                    if (timer <= 0f && !submitted) Submit();
                }
                if (!submitted)
                {
                    var def = Items.Get(placer.Selected);
                    string hint = string.IsNullOrEmpty(placer.Hint) ? $"<b>{def.Name}:</b> {def.Description}" : $"<color=#B5452F>{placer.Hint}</color>";
                    string help = phase == LocalPhase.Supply
                        ? "   ·   Kahraman numaralı sırayla en yakın hedefe gider, sonra çıkışa koşar"
                        : "   ·   Sol tık: koy   Sağ tık: kaldır   Sayı tuşları: seç";
                    ui.SetHint(hint + help);
                }
                else ui.SetHint("Yerleştirmen gönderildi.");
            }
            else if (phase == LocalPhase.Watching && playback.Running)
            {
                ui.UpdatePlayback(playback.HeroHpFraction, playback.LastInfo);
                ui.SetPlaybackStats(playback.StatsText);
            }
        }
    }
}

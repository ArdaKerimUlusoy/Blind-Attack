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
        public Action OnMatchFinishedReturn;
        float matchEndCountdown = -1f;
        int lastCountdownInt = -1;
        // bu turun iki saldırısının yerel simülasyon sonuçları (tur sonu istatistikleri için)
        readonly SimResult[] roundResults = new SimResult[2];

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
            matchEndCountdown = -1f;
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
                    roundResults[0] = roundResults[1] = null;
                    matchEndCountdown = -1f;
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
                    ui.SetTerritory(false);
                    int mine = Me == 0 ? s0 : s1, theirs = Me == 0 ? s1 : s0;
                    ui.EndCinematic();
                    var myStats = roundResults[Me] != null ? AttackStats.From(roundResults[Me]) : null;
                    var theirStats = roundResults[1 - Me] != null ? AttackStats.From(roundResults[1 - Me]) : null;
                    ui.ShowRoundStats(r, names[Me], Them, mine, theirs, myStats, theirStats);
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
                    matchEndCountdown = 7f;
                    lastCountdownInt = 7;
                    placer.End();
                    ui.SetTerritory(false);
                    UpdateScore();
                    string title = winner == Me ? "KAZANDIN!" : winner < 0 ? "BERABERE" : "KAYBETTİN";
                    var color = winner == Me ? UiKit.Gold : winner < 0 ? UiKit.Blue : UiKit.Red;
                    ui.ShowMatch(title, color, CanRematch != null && CanRematch(),
                        $"Tur galibiyeti: <b>{wins[Me]}</b> - <b>{wins[1 - Me]}</b>\nToplam puan: <b>{totals[Me]}</b> - <b>{totals[1 - Me]}</b>\n\n<size=24>Rakip: {Them}</size>",
                        winner == Me);
                    foreach (var a in Achievements.CheckMatch(winner == Me, wins[Me], wins[1 - Me])) ui.ShowAchievement(a.Name, a.Desc);
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
            roundResults[0] = roundResults[1] = null;
            view.Build(maps[Me], false);
            var env = FindAnyObjectByType<EnvironmentView>();
            if (env) env.Build(false);
            GameApp.ApplyLook(cam.Cam, false, maps[Me]);
            cam.Frame(view.WorldBounds);
            lastDefense.Clear();
            placer.Begin(view, cam.Cam, true, null);
            ui.ShowPalette(placer);
            ui.SetTerritory(false);
            ui.ShowTerritoryBanner("Senin Bölgen", "Savunmanı kur", false);
            ui.SetPhase($"TUR {round}/{rounds} · SAVUNMA", "Senin Bölgen: Düşman ve tuzakları yerleştir.");
            ui.SetMapInfo($"<b>{view.Map.Name}</b>\n<color=#4E8A2E><b>Senin Bölgen</b></color>");
            route.Clear();
        }

        void BeginSupply(float secs)
        {
            if (phase == LocalPhase.Defense && !submitted) Submit();
            phase = LocalPhase.Supply;
            submitted = false;
            timer = secs;
            ui.SetOverlay(null);
            view.Build(maps[1 - Me], true);
            var env = FindAnyObjectByType<EnvironmentView>();
            if (env) env.Build(true);
            GameApp.ApplyLook(cam.Cam, true, maps[1 - Me]);
            cam.Frame(view.WorldBounds);
            lastSupply.Clear();
            var initial = new List<Placement> { new Placement(ItemType.Hero, MatchRules.DefaultHeroCell(view.Map)) };
            placer.Begin(view, cam.Cam, false, initial);
            placer.Select(ItemType.Hero);
            ui.ShowPalette(placer);
            route.Show(view, placer.Placed);
            ui.SetTerritory(true);
            ui.ShowTerritoryBanner("Rakip Bölge", "Saldırı hazırlığı", true);
            ui.SetPhase($"TUR {round}/{rounds} · SALDIRI HAZIRLIĞI", "Rakip Bölge: Kahramanını ve kutularını yerleştir.");
            ui.SetMapInfo($"<b>{view.Map.Name}</b>\n<color=#B5452F><b>Rakip Bölge</b></color>");
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

            bool mine = attacker == Me;
            view.Build(mapIndex, mine);
            var env = FindAnyObjectByType<EnvironmentView>();
            if (env) env.Build(mine);
            GameApp.ApplyLook(cam.Cam, mine, mapIndex);
            cam.Frame(view.WorldBounds);

            var defenders = PlacementCodec.Decode(defCsv);
            var supplies = PlacementCodec.Decode(supCsv);
            var result = AttackSimulation.Run(view.Map, defenders, supplies);
            roundResults[attacker] = result;
            route.Show(view, supplies, 0.45f);
            if (result.Score != score) Debug.LogWarning($"[BlindAttack] Yerel simülasyon skoru ({result.Score}) host'tan farklı ({score}). Host skoru geçerli.");

            ui.SetTerritory(mine);
            ui.ShowCinematic($"TUR {round}/{rounds}", mine ? "Senin saldırın" : $"{Them} saldırıyor",
                $"{view.Map.Name}  ·  {Atmosphere.Name(Atmosphere.ForMap(mapIndex))}  ·  {(mine ? "Rakip Bölge" : "Senin Bölgen")}");
            ui.SetPhase($"TUR {round}/{rounds} · SALDIRI",
                mine ? "Rakip Bölge: Senin kahramanın ilerliyor." : $"Senin Bölgen: {Them} saldırıyor.");
            ui.SetMapInfo(mine ? $"<b>{view.Map.Name}</b>\n<color=#B5452F><b>Rakip Bölge</b></color>" : $"<b>{view.Map.Name}</b>\n<color=#4E8A2E><b>Senin Bölgen</b></color>");
            ui.ShowPlayback(mine ? "Senin saldırın" : $"{Them} saldırıyor");
            playback.Quiet = false;
            playback.Play(result, view, cam, !mine, true);
        }

        void OnPlaybackFinished()
        {
            if (phase != LocalPhase.Watching) return;
            Send?.Invoke(Msg.Join("DONE", round, watchingAttacker));
            bool mine = watchingAttacker == Me;
            var res = roundResults[watchingAttacker];
            if (res != null)
            {
                var st = AttackStats.From(res);
                foreach (var a in mine ? Achievements.CheckAttack(st) : Achievements.CheckDefense(st)) ui.ShowAchievement(a.Name, a.Desc);
            }
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
                        ? "   ·   Sol tık: koy   Sağ tık: kaldır   Sağ tık sürükle / Q-E: döndür   WASD: kaydır"
                        : "   ·   Bir bölgeye en fazla 3 birim · dar geçitlere yay   ·   Sol tık: koy   Sağ tık: kaldır";
                    ui.SetHint(hint + help);
                }
                else ui.SetHint("Yerleştirmen gönderildi.");
            }
            else if (phase == LocalPhase.Watching && playback.Running)
            {
                if (!playback.InIntro) ui.EndCinematic();
                ui.UpdatePlayback(playback.HeroHpFraction, playback.LastInfo);
                ui.SetPlaybackStats(playback.StatsText);
            }
            else if (phase == LocalPhase.Finished && matchEndCountdown > 0f)
            {
                matchEndCountdown -= Time.deltaTime;
                int secs = Mathf.CeilToInt(matchEndCountdown);
                if (secs != lastCountdownInt && secs >= 0)
                {
                    lastCountdownInt = secs;
                    ui.UpdateMatchCountdown(secs);
                }
                if (matchEndCountdown <= 0f)
                {
                    matchEndCountdown = -1f;
                    ui.HideRound();
                    OnMatchFinishedReturn?.Invoke();
                }
            }
        }
    }
}

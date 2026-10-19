using System;
using BlindAttack.Core;

namespace BlindAttack.Match
{
    /// <summary>
    /// Ağ mesajları. Hepsi '|' ile ayrılmış düz metindir.
    /// Host -> herkes:
    ///   START|map|map|rounds|name0|name1   (iki oyuncu aynı haritada)
    ///   PHASE|DEF|round|seconds   /  PHASE|SUP|round|seconds
    ///   ATTACK|attacker|round|mapIndex|defCsv|supCsv|score|reached
    ///   ROUND|round|score0|score1|wins0|wins1|total0|total1
    ///   MATCH|winner|wins0|wins1|total0|total1     (winner -1 = berabere)
    /// Oyuncu -> host:
    ///   HELLO|name   DEF|round|csv   SUP|round|csv   DONE|round|attacker
    /// </summary>
    public static class Msg
    {
        public static string Join(params object[] parts) => string.Join("|", parts);
        public static string[] Split(string msg) => msg.Split('|');
        public static int Int(string s) => int.TryParse(s, out var v) ? v : 0;
    }

    public enum HostState { Idle, Defense, Supply, Attacking, RoundEnd, Finished }

    /// <summary>
    /// Maçın hakemi. Sadece oda sahibinin (host) bilgisayarında çalışır.
    /// Unity'den bağımsızdır; zamanı Tick(dt) ile ilerletilir.
    /// </summary>
    public class MatchHost
    {
        public const float Grace = 3f;          // istemci gecikmesi payı
        public const float RoundEndSeconds = 10f;   // tur sonu istatistik ekranı

        public Action<string> Broadcast;        // tüm oyunculara (yerel + uzak) mesaj
        public HostState State { get; private set; } = HostState.Idle;

        readonly int[] maps = new int[2];
        readonly string[] names = { "Oyuncu 1", "Oyuncu 2" };
        readonly string[] def = new string[2], sup = new string[2];
        readonly bool[] defIn = new bool[2], supIn = new bool[2], done = new bool[2];
        readonly int[] wins = new int[2], total = new int[2], roundScore = new int[2];
        int round, attacker;
        float timer;

        public void SetName(int player, string name)
        {
            if (!string.IsNullOrWhiteSpace(name)) names[player] = name.Replace("|", "").Trim();
        }

        /// <param name="mapIndex">-1 ise rastgele harita seçilir. İki oyuncu da aynı haritayı kullanır.</param>
        public void StartMatch(int seed, int mapIndex = -1)
        {
            var rng = new Random(seed);
            maps[0] = maps[1] = mapIndex >= 0 ? mapIndex % MapLibrary.Count : rng.Next(MapLibrary.Count);
            wins[0] = wins[1] = total[0] = total[1] = 0;
            Broadcast?.Invoke(Msg.Join("START", maps[0], maps[1], MatchRules.Rounds, names[0], names[1]));
            BeginRound(1);
        }

        void BeginRound(int r)
        {
            round = r;
            defIn[0] = defIn[1] = supIn[0] = supIn[1] = false;
            def[0] = def[1] = sup[0] = sup[1] = "";
            State = HostState.Defense;
            timer = MatchRules.DefensePhaseSeconds + Grace;
            Broadcast?.Invoke(Msg.Join("PHASE", "DEF", round, (int)MatchRules.DefensePhaseSeconds));
        }

        public void OnPlayerMessage(int player, string msg)
        {
            if (player < 0 || player > 1) return;
            var p = Msg.Split(msg);
            switch (p[0])
            {
                case "HELLO":
                    if (p.Length > 1) SetName(player, p[1]);
                    break;
                case "DEF":
                    if (State == HostState.Defense && p.Length > 2 && Msg.Int(p[1]) == round)
                    { def[player] = p[2]; defIn[player] = true; }
                    break;
                case "SUP":
                    if (State == HostState.Supply && p.Length > 2 && Msg.Int(p[1]) == round)
                    { sup[player] = p[2]; supIn[player] = true; }
                    break;
                case "DONE":
                    if (State == HostState.Attacking && p.Length > 2 && Msg.Int(p[1]) == round && Msg.Int(p[2]) == attacker)
                        done[player] = true;
                    break;
            }
            Advance(false);
        }

        public void Tick(float dt)
        {
            if (State == HostState.Idle || State == HostState.Finished) return;
            timer -= dt;
            Advance(timer <= 0f);
        }

        void Advance(bool timeUp)
        {
            switch (State)
            {
                case HostState.Defense:
                    if (timeUp || (defIn[0] && defIn[1]))
                    {
                        State = HostState.Supply;
                        timer = MatchRules.SupplyPhaseSeconds + Grace;
                        Broadcast?.Invoke(Msg.Join("PHASE", "SUP", round, (int)MatchRules.SupplyPhaseSeconds));
                    }
                    break;
                case HostState.Supply:
                    if (timeUp || (supIn[0] && supIn[1])) StartAttack(0);
                    break;
                case HostState.Attacking:
                    if (timeUp || (done[0] && done[1]))
                    {
                        if (attacker == 0) StartAttack(1);
                        else EndRound();
                    }
                    break;
                case HostState.RoundEnd:
                    if (timeUp)
                    {
                        if (round >= MatchRules.Rounds) EndMatch();
                        else BeginRound(round + 1);
                    }
                    break;
            }
        }

        void StartAttack(int who)
        {
            attacker = who;
            int defender = 1 - who;
            var map = MapLibrary.Create(maps[defender]);
            var d = MatchRules.Sanitize(map, PlacementCodec.Decode(def[defender]), true);
            var s = MatchRules.Sanitize(map, PlacementCodec.Decode(sup[who]), false);
            var result = AttackSimulation.Run(map, d, s);
            roundScore[who] = result.Score;
            done[0] = done[1] = false;
            State = HostState.Attacking;
            // izleme süresi + bekleme payı (oyuncu 2x hızda izleyebilir ya da atlayabilir)
            timer = result.Duration + 20f;
            Broadcast?.Invoke(Msg.Join("ATTACK", who, round, maps[defender],
                PlacementCodec.Encode(d), PlacementCodec.Encode(s), result.Score, result.Reached ? 1 : 0));
        }

        void EndRound()
        {
            if (roundScore[0] > roundScore[1]) wins[0]++;
            else if (roundScore[1] > roundScore[0]) wins[1]++;
            total[0] += roundScore[0];
            total[1] += roundScore[1];
            State = HostState.RoundEnd;
            timer = RoundEndSeconds;
            Broadcast?.Invoke(Msg.Join("ROUND", round, roundScore[0], roundScore[1], wins[0], wins[1], total[0], total[1]));
        }

        void EndMatch()
        {
            int winner = wins[0] != wins[1] ? (wins[0] > wins[1] ? 0 : 1)
                       : total[0] != total[1] ? (total[0] > total[1] ? 0 : 1) : -1;
            State = HostState.Finished;
            Broadcast?.Invoke(Msg.Join("MATCH", winner, wins[0], wins[1], total[0], total[1]));
        }
    }

    /// <summary>Tek kişilik modda 2. oyuncunun yerini alan bot.</summary>
    public class BotPlayer
    {
        public Action<string> Send;
        int myMap, enemyMap, seed;

        public BotPlayer(int seed) { this.seed = seed; }

        public void Handle(string msg)
        {
            var p = Msg.Split(msg);
            switch (p[0])
            {
                case "START":
                    myMap = Msg.Int(p[2]); enemyMap = Msg.Int(p[1]);
                    break;
                case "PHASE":
                    int round = Msg.Int(p[2]);
                    if (p[1] == "DEF")
                        Send?.Invoke(Msg.Join("DEF", round, PlacementCodec.Encode(BotPlanner.PlanDefense(MapLibrary.Create(myMap), seed + round * 101))));
                    else
                        Send?.Invoke(Msg.Join("SUP", round, PlacementCodec.Encode(BotPlanner.PlanSupplies(MapLibrary.Create(enemyMap), seed + round * 211))));
                    break;
                case "ATTACK":
                    Send?.Invoke(Msg.Join("DONE", p[2], p[1]));
                    break;
            }
        }
    }
}
